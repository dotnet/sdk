// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// RS0014: Do not use Enumerable methods on indexable collections. Instead use the collection directly
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer : CodeFixProvider
    {
        private const string FirstPropertyName = "First";
        private const string LastPropertyName = "Last";
        private const string CountPropertyName = "Count";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic == null)
            {
                return;
            }

            var methodPropertyKey = DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.MethodPropertyKey;
            // The fixer is only implemented for "Enumerable.First", "Enumerable.Last" and "Enumerable.Count"
            if (!diagnostic.Properties.TryGetValue(methodPropertyKey, out var method)
                || (method != FirstPropertyName && method != LastPropertyName && method != CountPropertyName))
            {
                return;
            }

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var invocationNode = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (invocationNode == null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel.GetOperation(invocationNode, context.CancellationToken) is not IInvocationOperation invocationOperation)
            {
                return;
            }

            var collectionSyntax = invocationOperation.GetInstanceSyntax();
            if (collectionSyntax == null)
            {
                return;
            }

            // Last and Count code fix need the Count property so we want to ensure it exists before registration
            if (method is LastPropertyName or CountPropertyName)
            {
                var typeSymbol = semanticModel.GetTypeInfo(collectionSyntax).Type;
                if (!typeSymbol.HasAnyCollectionCountProperty(WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation)))
                {
                    return;
                }
            }

            var title = MicrosoftNetCoreAnalyzersResources.UseIndexer;

            context.RegisterCodeFix(new MyCodeAction(title,
                                        ct => UseCollectionDirectly(context.Document, root, invocationNode, collectionSyntax, method),
                                        equivalenceKey: title),
                                    diagnostic);
        }

        private static Task<Document> UseCollectionDirectly(Document document, SyntaxNode root, SyntaxNode invocationNode, SyntaxNode collectionSyntax, string methodName)
        {
            var generator = SyntaxGenerator.GetGenerator(document);

            var elementAccessNode = GetReplacementNode(methodName, generator, collectionSyntax);
            if (elementAccessNode == null)
            {
                return Task.FromResult(document);
            }

            var newRoot = root.ReplaceNode(invocationNode, elementAccessNode.WithTrailingTrivia(invocationNode.GetTrailingTrivia()));
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private static SyntaxNode? GetReplacementNode(string methodName, SyntaxGenerator generator, SyntaxNode collectionSyntax)
        {
            var collectionSyntaxNoTrailingTrivia = collectionSyntax.WithoutTrailingTrivia();

            if (methodName == FirstPropertyName)
            {
                var zeroLiteral = generator.LiteralExpression(0);
                return generator.ElementAccessExpression(collectionSyntaxNoTrailingTrivia, zeroLiteral);
            }

            if (methodName == LastPropertyName)
            {
                // TODO: Handle C# 8 index expression (and vb.net equivalent if any)

                // TODO: Handle cases were `collectionSyntax` is an invocation. We would need to create some intermediate variable.
                var countMemberAccess = generator.MemberAccessExpression(collectionSyntaxNoTrailingTrivia, CountPropertyName);
                var oneLiteral = generator.LiteralExpression(1);

                // The SubstractExpression method will wrap left and right in parenthesis but those will be automatically removed later on
                var substraction = generator.SubtractExpression(countMemberAccess, oneLiteral);
                return generator.ElementAccessExpression(collectionSyntaxNoTrailingTrivia, substraction);
            }

            if (methodName == CountPropertyName)
            {
                return generator.MemberAccessExpression(collectionSyntaxNoTrailingTrivia, CountPropertyName);
            }

            Debug.Fail($"Unexpected method name '{methodName}' for {DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyAnalyzer.RuleId} code fix.");
            return null;
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
