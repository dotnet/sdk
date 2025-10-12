// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Microsoft.NetCore.Analyzers.Usage
{
    public abstract class DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer<TInvocationExpression> : CodeFixProvider
        where TInvocationExpression : SyntaxNode
    {
        protected const string HasValue = nameof(Nullable<int>.HasValue);
        protected const string ArgumentNullException = nameof(System.ArgumentNullException);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not TInvocationExpression invocation)
                {
                    continue;
                }

                if (diagnostic.Id == DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NonNullableValueRuleId && invocation.Parent is not null)
                {
                    var codeAction = CodeAction.Create(
                        MicrosoftNetCoreAnalyzersResources.DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullCodeFixTitle,
                        _ =>
                        {
                            var newRoot = root.RemoveNode(invocation.Parent, SyntaxRemoveOptions.KeepNoTrivia);
                            if (newRoot is null)
                            {
                                return Task.FromResult(context.Document);
                            }

                            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                        }, MicrosoftNetCoreAnalyzersResources.DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullCodeFixTitle);
                    context.RegisterCodeFix(codeAction, diagnostic);
                }
                else if (diagnostic.Id == DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NullableStructRuleId)
                {
                    var codeAction = CodeAction.Create(
                        MicrosoftNetCoreAnalyzersResources.DoNotPassNullableStructToArgumentNullExceptionThrowIfNullCodeFixTitle,
                        async ct =>
                        {
                            var newRoot = await GetNewRootForNullableStructAsync(context.Document, invocation, ct).ConfigureAwait(false);
                            if (newRoot is null)
                            {
                                return context.Document;
                            }

                            return context.Document.WithSyntaxRoot(newRoot);
                        }, MicrosoftNetCoreAnalyzersResources.DoNotPassNullableStructToArgumentNullExceptionThrowIfNullCodeFixTitle);
                    context.RegisterCodeFix(codeAction, diagnostic);
                }
            }
        }

        protected abstract Task<SyntaxNode> GetNewRootForNullableStructAsync(Document document, TInvocationExpression invocation, CancellationToken cancellationToken);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NonNullableValueRuleId,
            DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NullableStructRuleId
        );
    }
}
