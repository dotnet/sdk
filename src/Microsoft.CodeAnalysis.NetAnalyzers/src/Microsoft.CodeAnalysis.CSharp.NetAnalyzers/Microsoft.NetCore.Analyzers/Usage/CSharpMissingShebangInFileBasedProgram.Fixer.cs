// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpMissingShebangInFileBasedProgramFixer : MissingShebangInFileBasedProgramFixer
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.MissingShebangInFileBasedProgramCodeFixTitle,
                async ct =>
                {
                    var options = await context.Document.GetOptionsAsync(ct).ConfigureAwait(false);
                    var eol = options.GetOption(FormattingOptions.NewLine, LanguageNames.CSharp);
                    var shebangTrivia = SyntaxFactory.ParseLeadingTrivia("#!/usr/bin/env dotnet" + eol);
                    var firstToken = root.GetFirstToken(includeZeroWidth: true);
                    var newFirstToken = firstToken.WithLeadingTrivia(shebangTrivia.AddRange(firstToken.LeadingTrivia));
                    var newRoot = root.ReplaceToken(firstToken, newFirstToken)
                        .WithAdditionalAnnotations(Formatter.Annotation);
                    return context.Document.WithSyntaxRoot(newRoot);
                },
                MicrosoftNetCoreAnalyzersResources.MissingShebangInFileBasedProgramCodeFixTitle);
            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }
    }
}
