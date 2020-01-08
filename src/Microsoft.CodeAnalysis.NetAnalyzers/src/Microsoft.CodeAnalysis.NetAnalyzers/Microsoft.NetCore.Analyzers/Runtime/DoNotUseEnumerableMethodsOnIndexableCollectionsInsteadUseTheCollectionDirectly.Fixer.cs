// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// RS0014: Do not use Enumerable methods on indexable collections. Instead use the collection directly
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                return Task.CompletedTask;
            }

            var methodPropertyKey = DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.MethodPropertyKey;
            // The fixer is only implemented for "Enumerable.First"
            if (!diagnostic.Properties.TryGetValue(methodPropertyKey, out var method) || method != "First")
            {
                return Task.CompletedTask;
            }

            var title = MicrosoftNetCoreAnalyzersResources.UseIndexer;

            context.RegisterCodeFix(new MyCodeAction(title,
                                        async ct => await UseCollectionDirectly(context.Document, context.Span, ct).ConfigureAwait(false),
                                        equivalenceKey: title),
                                    diagnostic);

            return Task.CompletedTask;
        }

        private async Task<Document> UseCollectionDirectly(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var invocationNode = root.FindNode(span, getInnermostNodeForTie: true);
            if (invocationNode == null)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (!(semanticModel.GetOperation(invocationNode, cancellationToken) is IInvocationOperation invocationOperation))
            {
                return document;
            }

            var collectionSyntax = invocationOperation.GetInstance();
            if (collectionSyntax == null)
            {
                return document;
            }

            var generator = SyntaxGenerator.GetGenerator(document);
            var indexNode = generator.LiteralExpression(0);
            var elementAccessNode = generator.ElementAccessExpression(collectionSyntax.WithoutTrailingTrivia(), indexNode)
                .WithTrailingTrivia(invocationNode.GetTrailingTrivia());

            var newRoot = root.ReplaceNode(invocationNode, elementAccessNode);
            return document.WithSyntaxRoot(newRoot);
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
