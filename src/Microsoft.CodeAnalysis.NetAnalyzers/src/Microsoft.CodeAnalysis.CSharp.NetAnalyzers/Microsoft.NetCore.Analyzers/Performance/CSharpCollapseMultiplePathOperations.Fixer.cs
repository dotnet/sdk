// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.NetCore.Analyzers;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpCollapseMultiplePathOperationsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(CollapseMultiplePathOperationsAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var diagnostic = context.Diagnostics[0];
            var root = await document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is not InvocationExpressionSyntax ||
                await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false) is not { } semanticModel ||
                WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation).GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOPath) is null)
            {
                return;
            }

            // Get the method name from diagnostic properties
            if (!diagnostic.Properties.TryGetValue(CollapseMultiplePathOperationsAnalyzer.MethodNameKey, out var methodName))
            {
                methodName = "Path";
            }

            RegisterCodeFix(
                context,
                string.Format(MicrosoftNetCoreAnalyzersResources.CollapseMultiplePathOperationsCodeFixTitle, methodName),
                nameof(MicrosoftNetCoreAnalyzersResources.CollapseMultiplePathOperationsCodeFixTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation ||
                await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } semanticModel ||
                WellKnownTypeProvider.GetOrCreate(semanticModel.Compilation).GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOPath) is not { } pathType)
            {
                return;
            }

            // Collect all arguments by recursively unwrapping nested Path.Combine/Join calls
            var allArguments = CollectAllArguments(invocation, pathType, semanticModel);

            foreach (var argument in allArguments)
            {
                editor.TrackNode(argument);
            }

            editor.ReplaceNode(invocation, (currentNode, _) =>
            {
                var current = (InvocationExpressionSyntax)currentNode;

                // Create new argument list with all collected arguments, as any fix inside them left them
                var newArgumentList = SyntaxFactory.ArgumentList(
                    SyntaxFactory.SeparatedList(allArguments.Select(argument => current.GetCurrentNode(argument) ?? argument)));

                return current.WithArgumentList(newArgumentList)
                    .WithTriviaFrom(current);
            });
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
