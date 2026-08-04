// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
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
            if (!FileBasedProgramDirectiveQuoting.TryParse(trivia, out var kind, out var value) ||
                !FileBasedProgramDirectiveQuoting.TryGetQuotedForm(kind, value, out var newValue))
            {
                return;
            }

            var triviaSpan = trivia.Span;
            var newDirectiveText = "#:" + kind + " " + newValue;

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.PreferQuotedFileBasedProgramDirectiveCodeFixTitle,
                async ct =>
                {
                    var text = await context.Document.GetTextAsync(ct).ConfigureAwait(false);
                    return context.Document.WithText(text.Replace(triviaSpan, newDirectiveText));
                },
                nameof(MicrosoftNetCoreAnalyzersResources.PreferQuotedFileBasedProgramDirectiveCodeFixTitle));
            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }
    }
}
