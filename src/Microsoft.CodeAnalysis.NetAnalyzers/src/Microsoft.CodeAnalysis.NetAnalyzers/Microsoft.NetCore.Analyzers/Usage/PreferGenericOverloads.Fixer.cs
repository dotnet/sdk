// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.NetCore.Analyzers.Usage.PreferGenericOverloadsAnalyzer;

namespace Microsoft.NetCore.Analyzers.Usage
{
    /// <summary>
    /// CA2263: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsTitle"/>
    /// </summary>
    public abstract class PreferGenericOverloadsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(RuleId);

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

            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var operation = semanticModel.GetOperation(node, context.CancellationToken);

            if (operation is not IInvocationOperation invocation)
            {
                return;
            }

            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsCodeFixTitle,
                ct => ReplaceWithGenericCallAsync(context.Document, invocation, ct),
                nameof(MicrosoftNetCoreAnalyzersResources.PreferGenericOverloadsCodeFixTitle));

            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }

        protected abstract Task<Document> ReplaceWithGenericCallAsync(Document document, IInvocationOperation invocation, CancellationToken cancellationToken);
    }
}
