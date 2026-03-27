// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
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
                _ =>
                {
                    var eol = GetEndOfLine(root.SyntaxTree.GetText());
                    var shebangTrivia = SyntaxFactory.ParseLeadingTrivia("#!/usr/bin/env dotnet" + eol);
                    var firstToken = root.GetFirstToken(includeZeroWidth: true);
                    var newFirstToken = firstToken.WithLeadingTrivia(shebangTrivia.AddRange(firstToken.LeadingTrivia));
                    var newRoot = root.ReplaceToken(firstToken, newFirstToken);
                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                MicrosoftNetCoreAnalyzersResources.MissingShebangInFileBasedProgramCodeFixTitle);
            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }

        private static string GetEndOfLine(SourceText sourceText)
        {
            foreach (var line in sourceText.Lines)
            {
                if (line.End < line.EndIncludingLineBreak)
                {
                    return sourceText.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
                }
            }

            return Environment.NewLine;
        }
    }
}
