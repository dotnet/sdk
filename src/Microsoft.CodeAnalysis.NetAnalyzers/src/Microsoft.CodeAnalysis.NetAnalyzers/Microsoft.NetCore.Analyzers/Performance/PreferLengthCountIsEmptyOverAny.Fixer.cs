// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class PreferLengthCountIsEmptyOverAnyFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferLengthCountIsEmptyOverAnyAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            foreach (var diagnostic in context.Diagnostics)
            {
                var (newRoot, codeFixTitle) = diagnostic.Properties[PreferLengthCountIsEmptyOverAnyAnalyzer.DiagnosticPropertyKey] switch
                {
                    PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText => (ReplaceAnyWithIsEmpty(root, node), MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverAnyCodeFixTitle),
                    PreferLengthCountIsEmptyOverAnyAnalyzer.LengthText => (ReplaceAnyWithLength(root, node), MicrosoftNetCoreAnalyzersResources.PreferLengthOverAnyCodeFixTitle),
                    PreferLengthCountIsEmptyOverAnyAnalyzer.CountText => (ReplaceAnyWithCount(root, node), MicrosoftNetCoreAnalyzersResources.PreferCountOverAnyCodeFixTitle),
                    _ => throw new NotSupportedException()
                };
                if (newRoot is null)
                {
                    continue;
                }

                var codeAction = CodeAction.Create(codeFixTitle, _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)), codeFixTitle);
                context.RegisterCodeFix(codeAction, diagnostic);
            }
        }

        protected abstract SyntaxNode? ReplaceAnyWithIsEmpty(SyntaxNode root, SyntaxNode node);
        protected abstract SyntaxNode? ReplaceAnyWithLength(SyntaxNode root, SyntaxNode node);
        protected abstract SyntaxNode? ReplaceAnyWithCount(SyntaxNode root, SyntaxNode node);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}