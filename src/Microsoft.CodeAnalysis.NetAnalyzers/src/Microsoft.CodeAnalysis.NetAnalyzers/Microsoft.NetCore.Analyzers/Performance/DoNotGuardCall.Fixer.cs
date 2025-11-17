// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1853: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.DoNotGuardDictionaryRemoveByContainsKeyTitle"/>
    /// CA1868: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.DoNotGuardSetAddOrRemoveByContainsTitle"/>
    /// </summary>
    public abstract class DoNotGuardCallFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            DoNotGuardCallAnalyzer.DoNotGuardDictionaryRemoveByContainsKeyRuleId,
            DoNotGuardCallAnalyzer.DoNotGuardSetAddOrRemoveByContainsRuleId);

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

            var diagnostic = context.Diagnostics[0];
            var conditionalLocation = diagnostic.AdditionalLocations[0];
            var childLocation = diagnostic.AdditionalLocations[1];

            if (root.FindNode(conditionalLocation.SourceSpan) is not SyntaxNode conditionalSyntax ||
                root.FindNode(childLocation.SourceSpan) is not SyntaxNode childStatementSyntax)
            {
                return;
            }

            if (!SyntaxSupportedByFixer(conditionalSyntax, childStatementSyntax))
            {
                return;
            }

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.RemoveRedundantGuardCallCodeFixTitle,
                ct => Task.FromResult(ReplaceConditionWithChild(context.Document, root, conditionalSyntax, childStatementSyntax)),
                diagnostic.Descriptor.Id);

            context.RegisterCodeFix(codeAction, diagnostic);
        }

        protected abstract bool SyntaxSupportedByFixer(SyntaxNode conditionalSyntax, SyntaxNode childStatementSyntax);

        protected abstract Document ReplaceConditionWithChild(Document document, SyntaxNode root,
                                                              SyntaxNode conditionalOperationNode,
                                                              SyntaxNode childOperationNode);
    }
}
