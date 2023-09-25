// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    public abstract class MakeTypesInternalFixer : CodeFixProvider
    {
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            var codeAction = CodeAction.Create(
                MicrosoftCodeQualityAnalyzersResources.MakeTypesInternalCodeFixTitle,
                _ =>
                {
                    var newNode = MakeInternal(node);
                    var newRoot = root.ReplaceNode(node, newNode.WithTriviaFrom(node));

                    return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                },
                MicrosoftCodeQualityAnalyzersResources.MakeTypesInternalCodeFixTitle);
            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        protected abstract SyntaxNode MakeInternal(SyntaxNode node);

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(MakeTypesInternal<SymbolKind>.RuleId);
    }
}