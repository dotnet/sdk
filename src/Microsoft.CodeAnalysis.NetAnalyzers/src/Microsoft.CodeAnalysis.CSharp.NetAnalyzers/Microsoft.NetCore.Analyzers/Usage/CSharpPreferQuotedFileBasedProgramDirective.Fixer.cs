// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class CSharpPreferQuotedFileBasedProgramDirectiveFixer : PreferQuotedFileBasedProgramDirectiveFixer
{
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var trivia = root.FindTrivia(context.Span.Start);
        if (!FileBasedProgramDirectiveQuoting.TryParse(trivia, out var kind, out var value, out _) ||
            !FileBasedProgramDirectiveQuoting.TryGetQuotedForm(kind, value, out _))
        {
            return;
        }

        RegisterCodeFix(
            context,
            MicrosoftNetCoreAnalyzersResources.PreferQuotedFileBasedProgramDirectiveCodeFixTitle,
            nameof(MicrosoftNetCoreAnalyzersResources.PreferQuotedFileBasedProgramDirectiveCodeFixTitle));
    }

    protected override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
    {
        var trivia = editor.OriginalRoot.FindTrivia(diagnostic.Location.SourceSpan.Start);
        if (!FileBasedProgramDirectiveQuoting.TryParse(trivia, out var kind, out var value, out var valueLeadingWhitespace) ||
            !FileBasedProgramDirectiveQuoting.TryGetQuotedForm(kind, value, out var newValue) ||
            trivia.GetStructure() is not { } structure ||
            SyntaxFactory.ParseLeadingTrivia("#:" + kind + valueLeadingWhitespace + newValue + "\n").FirstOrDefault().GetStructure() is not { } newStructure)
        {
            return Task.CompletedTask;
        }

        editor.ReplaceNode(structure, newStructure.WithTrailingTrivia(structure.GetTrailingTrivia()));
        return Task.CompletedTask;
    }
}
