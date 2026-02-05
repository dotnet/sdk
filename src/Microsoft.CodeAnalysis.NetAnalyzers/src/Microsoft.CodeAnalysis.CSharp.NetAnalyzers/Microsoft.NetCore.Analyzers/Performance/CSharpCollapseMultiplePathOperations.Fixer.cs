// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpCollapseMultiplePathOperationsFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(CollapseMultiplePathOperationsAnalyzer.RuleId);

        public override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var diagnostic = context.Diagnostics[0];
            var root = await document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is not InvocationExpressionSyntax invocation ||
                await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false) is not { } semanticModel ||
                semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.SystemIOPath) is not { } pathType)
            {
                return;
            }

            // Get the method name from diagnostic properties
            if (!diagnostic.Properties.TryGetValue(CollapseMultiplePathOperationsAnalyzer.MethodNameKey, out var methodName))
            {
                methodName = "Path";
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    string.Format(MicrosoftNetCoreAnalyzersResources.CollapseMultiplePathOperationsCodeFixTitle, methodName),
                    createChangedDocument: cancellationToken => CollapsePathOperationAsync(document, root, invocation, pathType, semanticModel, cancellationToken),
                    equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.CollapseMultiplePathOperationsCodeFixTitle)),
                diagnostic);
        }

        private static Task<Document> CollapsePathOperationAsync(Document document, SyntaxNode root, InvocationExpressionSyntax invocation, INamedTypeSymbol pathType, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            // Collect all arguments by recursively unwrapping nested Path.Combine/Join calls
            var allArguments = CollectAllArguments(invocation, pathType, semanticModel);

            // Create new argument list with all collected arguments
            var newArgumentList = SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(allArguments));

            // Create the new invocation with all arguments
            var newInvocation = invocation.WithArgumentList(newArgumentList)
                .WithTriviaFrom(invocation);

            var newRoot = root.ReplaceNode(invocation, newInvocation);

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private static ArgumentSyntax[] CollectAllArguments(InvocationExpressionSyntax invocation, INamedTypeSymbol pathType, SemanticModel semanticModel)
        {
            var arguments = ImmutableArray.CreateBuilder<ArgumentSyntax>();

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is InvocationExpressionSyntax nestedInvocation &&
                    IsPathCombineOrJoin(nestedInvocation, pathType, semanticModel, out var methodName) &&
                    IsPathCombineOrJoin(invocation, pathType, semanticModel, out var outerMethodName) &&
                    methodName == outerMethodName)
                {
                    // Recursively collect arguments from nested invocation
                    arguments.AddRange(CollectAllArguments(nestedInvocation, pathType, semanticModel));
                }
                else
                {
                    arguments.Add(argument);
                }
            }

            return arguments.ToArray();
        }

        private static bool IsPathCombineOrJoin(InvocationExpressionSyntax invocation, INamedTypeSymbol pathType, SemanticModel semanticModel, out string methodName)
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol methodSymbol &&
                SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, pathType) &&
                methodSymbol.Name is "Combine" or "Join")
            {
                methodName = methodSymbol.Name;
                return true;
            }

            methodName = string.Empty;
            return false;
        }
    }
}
