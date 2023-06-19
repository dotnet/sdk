// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class DoNotGuardDictionaryRemoveByContainsKeyFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(DoNotGuardDictionaryRemoveByContainsKey.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.First();
            var conditionalOperationSpan = diagnostic.AdditionalLocations[0];
            var childLocation = diagnostic.AdditionalLocations[1];
            if (root.FindNode(conditionalOperationSpan.SourceSpan) is not SyntaxNode conditionalSyntax ||
                root.FindNode(childLocation.SourceSpan) is not SyntaxNode childStatementSyntax)
            {
                return;
            }

            // we only offer a fixer if 'Remove' is the _only_ statement
            if (!SyntaxSupportedByFixer(conditionalSyntax))
                return;

            context.RegisterCodeFix(CodeAction.Create(MicrosoftNetCoreAnalyzersResources.RemoveRedundantGuardCallCodeFixTitle, ct =>
                    Task.FromResult(ReplaceConditionWithChild(context.Document, root, conditionalSyntax, childStatementSyntax)), MicrosoftNetCoreAnalyzersResources.RemoveRedundantGuardCallCodeFixTitle),
                diagnostic);
        }

        protected abstract bool SyntaxSupportedByFixer(SyntaxNode conditionalSyntax);

        protected abstract Document ReplaceConditionWithChild(Document document, SyntaxNode root,
                                                              SyntaxNode conditionalOperationNode,
                                                              SyntaxNode childOperationNode);
    }
}
