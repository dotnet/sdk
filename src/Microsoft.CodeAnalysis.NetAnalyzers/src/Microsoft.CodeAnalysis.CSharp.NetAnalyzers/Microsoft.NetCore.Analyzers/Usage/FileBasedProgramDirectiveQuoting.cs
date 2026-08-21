// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.FileBasedPrograms;
using static Microsoft.DotNet.FileBasedPrograms.FileBasedProgramDirectiveValueHelpers;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage;

/// <summary>
/// Shared logic for detecting the deprecated unquoted-whitespace form of a file-based program
/// <c>#:</c> directive and for computing its quoted replacement. This mirrors (a conservative
/// subset of) the directive parser in <c>Microsoft.DotNet.FileBasedPrograms</c> and reuses that
/// parser's value-level primitives (see <see cref="FileBasedProgramDirectiveValueHelpers"/>) for
/// quoting, name validity, and metadata detection so the two cannot drift. It flags only directives
/// that the parser accepts as the legacy form and that have an unambiguous, semantics-preserving
/// quoted equivalent.
/// </summary>
internal static class FileBasedProgramDirectiveQuoting
{
    /// <summary>
    /// Extracts the directive kind (e.g. <c>property</c>) and its value text from a file-based program
    /// <c>#:</c> directive trivia. Returns <see langword="false"/> for any other trivia.
    /// </summary>
    public static bool TryParse(SyntaxTrivia trivia, out string kind, out string value, out string valueLeadingWhitespace)
    {
        kind = string.Empty;
        value = string.Empty;
        valueLeadingWhitespace = string.Empty;

        // '#:' directives are represented as directive trivia whose structure carries a single
        // string literal token holding the text after '#:'. Exclude the '#!' shebang explicitly.
        if (trivia.IsKind(SyntaxKind.ShebangDirectiveTrivia))
        {
            return false;
        }

        var structure = trivia.GetStructure();
        if (structure is null)
        {
            return false;
        }

        var content = structure.ChildTokens().FirstOrDefault(static token => token.IsKind(SyntaxKind.StringLiteralToken));
        if (!content.IsKind(SyntaxKind.StringLiteralToken))
        {
            return false;
        }

        var text = content.Text.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        var whitespaceIndex = IndexOfWhitespace(text);
        if (whitespaceIndex < 0)
        {
            kind = text;
            value = string.Empty;
        }
        else
        {
            kind = text.Substring(0, whitespaceIndex);
            var valueWithLeadingWhitespace = text.Substring(whitespaceIndex);
            value = valueWithLeadingWhitespace.TrimStart();
            valueLeadingWhitespace = valueWithLeadingWhitespace.Substring(0, valueWithLeadingWhitespace.Length - value.Length);
        }

        return true;
    }

    /// <summary>
    /// Returns whether the directive uses the deprecated unquoted-whitespace form.
    /// </summary>
    public static bool IsLegacyForm(string kind, string value)
    {
        // No value, or already quoted (quotes unambiguously mean the new form): nothing to flag.
        if (value.Length == 0 || value.IndexOf('"') >= 0)
        {
            return false;
        }

        var tokens = SplitWhitespace(value);

        // A single whitespace-separated token is unambiguous and never the legacy form.
        if (tokens.Length <= 1)
        {
            return false;
        }

        return kind switch
        {
            "property" or "sdk" or "include" or "exclude" => true,
            "package" or "project" or "ref" => !AllValidMetadata(tokens, start: 1),
            _ => false,
        };
    }

    /// <summary>
    /// Computes an equivalent quoted <paramref name="newValue"/> for a legacy directive.
    /// Returns <see langword="false"/> when the legacy form cannot be rewritten without changing
    /// its value.
    /// </summary>
    public static bool TryGetQuotedForm(string kind, string value, out string newValue)
    {
        newValue = value;
        if (!IsLegacyForm(kind, value))
        {
            return false;
        }

        switch (kind)
        {
            case "property":
                return TryQuoteAfterSeparator(value, out newValue);

            case "sdk":
            case "package":
                return TryQuoteVersion(value, out newValue);

            case "project":
            case "ref":
            case "include":
            case "exclude":
                newValue = QuoteIfNeeded(value);
                return true;

            default:
                return false;
        }
    }

    private static bool TryQuoteAfterSeparator(string value, out string newValue)
    {
        newValue = value;

        var separatorIndex = value.IndexOf('=');
        if (separatorIndex < 0)
        {
            return false;
        }

        var name = value.Substring(0, separatorIndex).TrimEnd();
        if (name.Length == 0 || ContainsDisallowedNameCharacter(name))
        {
            return false;
        }

        var valueAfterSeparator = value.Substring(separatorIndex + 1);
        var innerValue = valueAfterSeparator.TrimStart();
        var leadingWhitespace = valueAfterSeparator.Substring(0, valueAfterSeparator.Length - innerValue.Length);
        newValue = value.Substring(0, separatorIndex + 1) + leadingWhitespace + SymbolDisplay.FormatLiteral(innerValue, quote: true);
        return true;
    }

    private static bool TryQuoteVersion(string value, out string newValue)
    {
        newValue = value;

        var separatorIndex = value.IndexOf('@');
        if (separatorIndex < 0)
        {
            return false;
        }

        var name = value.Substring(0, separatorIndex).TrimEnd();
        if (name.Length == 0 || ContainsDisallowedNameCharacter(name))
        {
            return false;
        }

        // A version with internal whitespace is already invalid and is left alone.
        var valueAfterSeparator = value.Substring(separatorIndex + 1);
        var version = valueAfterSeparator.TrimStart();
        if (version.Length == 0 || IndexOfWhitespace(version) >= 0)
        {
            return false;
        }

        var leadingWhitespace = valueAfterSeparator.Substring(0, valueAfterSeparator.Length - version.Length);
        newValue = value.Substring(0, separatorIndex + 1) + leadingWhitespace + SymbolDisplay.FormatLiteral(version, quote: true);
        return true;
    }

    private static int IndexOfWhitespace(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static ImmutableArray<string> SplitWhitespace(string text)
    {
        var tokens = ImmutableArray.CreateBuilder<string>();
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                if (start >= 0)
                {
                    tokens.Add(text.Substring(start, i - start));
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }

        if (start >= 0)
        {
            tokens.Add(text.Substring(start));
        }

        return tokens.ToImmutable();
    }
}
