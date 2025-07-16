// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// A helper to perform edits of file-based app C# source files (e.g., updating the directives).
/// </summary>
internal sealed class FileBasedAppSourceEditor
{
    public SourceFile SourceFile
    {
        get;
        private set
        {
            field = value;

            // Make sure directives are reloaded next time they are accessed.
            Directives = default;
        }
    }

    public ImmutableArray<CSharpDirective> Directives
    {
        get
        {
            if (field.IsDefault)
            {
                field = LoadDirectives(SourceFile);
                Debug.Assert(!field.IsDefault);
            }

            return field;
        }
        private set
        {
            field = value;
        }
    }

    public required string NewLine { get; init; }

    private FileBasedAppSourceEditor() { }

    public static FileBasedAppSourceEditor Load(SourceFile sourceFile)
    {
        return new FileBasedAppSourceEditor
        {
            SourceFile = sourceFile,
            Directives = LoadDirectives(sourceFile),
            NewLine = GetNewLine(sourceFile.Text),
        };

        static string GetNewLine(SourceText text)
        {
            // Try to detect existing line endings.
            string firstLine = text.Lines is [{ } line, ..]
                ? text.ToString(line.SpanIncludingLineBreak)
                : string.Empty;
            return firstLine switch
            {
                [.., '\r', '\n'] => "\r\n",
                [.., '\n'] => "\n",
                [.., '\r'] => "\r",
                [.., '\u0085'] => "\u0085",
                [.., '\u2028'] => "\u2028",
                [.., '\u2029'] => "\u2029",
                _ => Environment.NewLine,
            };
        }
    }

    private static ImmutableArray<CSharpDirective> LoadDirectives(SourceFile sourceFile)
    {
        return VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: false, DiagnosticBag.Ignore());
    }

    public void Add(CSharpDirective directive)
    {
        TextSpan span = DetermineWhereToAdd(directive, out var append);
        string newText = append.Prefix + directive.ToString() + NewLine + append.Suffix;
        SourceFile = SourceFile.WithText(SourceFile.Text.Replace(span, newText: newText));
    }

    private TextSpan DetermineWhereToAdd(CSharpDirective directive, out (string? Prefix, string? Suffix) append)
    {
        append = default;

        // Find one that has the same kind and name.
        // If found, we will replace it with the new directive.
        if (directive is CSharpDirective.Named named &&
            Directives.OfType<CSharpDirective.Named>().FirstOrDefault(d => NamedDirectiveComparer.Instance.Equals(d, named)) is { } toReplace)
        {
            return toReplace.Info.Span;
        }

        // Find the last directive of the first group of directives of the same kind.
        // If found, we will insert the new directive after it.
        CSharpDirective? addAfer = null;
        foreach (var existingDirective in Directives)
        {
            if (existingDirective.GetType() == directive.GetType())
            {
                addAfer = existingDirective;
            }
            else if (addAfer != null)
            {
                break;
            }
        }

        if (addAfer != null)
        {
            return new TextSpan(start: addAfer.Info.Span.End, length: 0);
        }

        // Otherwise, we will add the directive to the top of the file.
        int start = 0;

        var tokenizer = VirtualProjectBuildingCommand.CreateTokenizer(SourceFile.Text);
        var result = tokenizer.ParseNextToken();
        var leadingTrivia = result.Token.LeadingTrivia;

        // If there is a comment at the top of the file, we add the directive after it
        // (the comment might be a license which should always stay at the top).
        int i = 0;
        bool anyComments = false;
        int trailingNewLines = 0;
        for (; i < leadingTrivia.Count; i++)
        {
            var trivia = leadingTrivia[i];
            bool isComment = IsComment(trivia);
            if (isComment)
            {
                anyComments = true;
                trailingNewLines = 0;
            }

            bool isEndOfLine = trivia.IsKind(SyntaxKind.EndOfLineTrivia);
            if (isEndOfLine)
            {
                trailingNewLines++;
            }

            if (!isComment && !isEndOfLine && !trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                break;
            }
        }

        if (!anyComments)
        {
            i = -1;
        }

        if (i > 0)
        {
            var lastCommentOrWhiteSpace = leadingTrivia[i - 1];

            // Add newline after the comment if there is not one already (can happen with block comments).
            if (!lastCommentOrWhiteSpace.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                append.Prefix += NewLine;
            }

            // Add a blank separating line between the comment and the directive (unless there is already one).
            if (trailingNewLines < 2)
            {
                append.Prefix += NewLine;
            }

            start = lastCommentOrWhiteSpace.FullSpan.End;
        }

        // Add a blank line after the directive unless there is already a blank line or another directive before the first C# token.
        if (!leadingTrivia.Skip(i).Any(static t => t.IsKind(SyntaxKind.EndOfLineTrivia) || t.IsDirective))
        {
            append.Suffix += NewLine;
        }

        return new TextSpan(start: start, length: 0);

        static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia
                or SyntaxKind.SingleLineDocumentationCommentTrivia or SyntaxKind.MultiLineDocumentationCommentTrivia;
        }
    }

    public void Remove(CSharpDirective directive)
    {
        var span = directive.Info.Span;
        var start = span.Start;
        var length = span.Length + DetermineTrailingLengthToRemove(directive);
        SourceFile = SourceFile.WithText(SourceFile.Text.Replace(start: start, length: length, newText: string.Empty));
    }

    private static int DetermineTrailingLengthToRemove(CSharpDirective directive)
    {
        // If there are blank lines both before and after the directive, remove the trailing white space.
        if (directive.Info.LeadingWhiteSpace.LineBreaks > 0 && directive.Info.TrailingWhiteSpace.LineBreaks > 0)
        {
            return directive.Info.TrailingWhiteSpace.TotalLength;
        }

        // If the directive (including leading white space) starts at the beginning of the file,
        // remove both the leading and trailing white space.
        var startBeforeWhiteSpace = directive.Info.Span.Start - directive.Info.LeadingWhiteSpace.TotalLength;
        if (startBeforeWhiteSpace == 0)
        {
            return directive.Info.LeadingWhiteSpace.TotalLength + directive.Info.TrailingWhiteSpace.TotalLength;
        }

        Debug.Assert(startBeforeWhiteSpace > 0);
        return 0;
    }
}
