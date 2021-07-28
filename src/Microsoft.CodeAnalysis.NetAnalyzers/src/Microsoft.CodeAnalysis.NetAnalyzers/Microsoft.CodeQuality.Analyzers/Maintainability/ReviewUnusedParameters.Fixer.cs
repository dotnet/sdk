// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1801: Review unused parameters
    /// </summary>
    public abstract class ReviewUnusedParametersFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ReviewUnusedParametersAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics)
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftCodeQualityAnalyzersResources.RemoveUnusedParameterMessage,
                        async ct => await RemoveNodesAsync(context.Document, diagnostic, ct).ConfigureAwait(false),
                        equivalenceKey: MicrosoftCodeQualityAnalyzersResources.RemoveUnusedParameterMessage),
                    diagnostic);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets parameter declaration node for a given diagnostic node.
        /// Requires language specific implementation because
        /// diagnostics are reported on different syntax nodes for across languages.
        /// </summary>
        /// <param name="node">the diagnostic node</param>
        /// <returns>the parameter declaration node</returns>
        protected abstract SyntaxNode GetParameterDeclarationNode(SyntaxNode node);

        /// <summary>
        /// Checks if the node has a proper kind for a subnode for a object creation or an invocation node.
        /// Requires language specific implementation because node kinds checked are language specific.
        /// </summary>
        protected abstract bool CanContinuouslyLeadToObjectCreationOrInvocation(SyntaxNode node);

        private async Task<Solution> RemoveNodesAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;
            var pairs = await GetNodesToRemoveAsync(document, diagnostic, cancellationToken).ConfigureAwait(false);
            foreach (var group in pairs.GroupBy(p => p.Key))
            {
                DocumentEditor editor = await DocumentEditor.CreateAsync(solution.GetDocument(group.Key), cancellationToken).ConfigureAwait(false);
                // Start removing from bottom to top to keep spans of nodes that are removed later.
                foreach (var value in group.OrderByDescending(v => v.Value.SpanStart))
                {
                    editor.RemoveNode(value.Value);
                }

                solution = solution.WithDocumentSyntaxRoot(group.Key, editor.GetChangedRoot());
            }

            return solution;
        }

        private ImmutableArray<IArgumentOperation>? GetOperationArguments(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            // For a given reference symbol node, find an object creation parent or an invocation parent. Then, return arguments of the parent found.
            // For 'c(param1, param2)', the input node is 'c'. Then, we climb up to 'c(param1, param2)', and return [param1, param2].
            // For 'new A.B(param1, param2)', the input node is 'B'. Then, we climb up to 'new A.B(param1, param2)', and return [param1, param2].
            // For 'A.B.C(param1, param2)', the input node is 'C'. Then, we climb up to 'A.B.C(param1, param2)', and return [param1, param2].

            // Consider operations like A.B.C(0). We start from 'C' and need to get to '0'.
            // To achieve this, it is necessary to climb up to 'A.B.C'
            // and check that this is IObjectCreationOperation or IInvocationOperation.
            // Intermediate calls of GetOperation on 'B.C' and 'A.B.C.' return null.
            // GetOperation on 'A.B.C(0)' returns a non-null operation.
            // After that, it is possible to check its arguments.
            // Return null in any unexpected situation, e.g. inconsistent tree.
            while (node != null)
            {
                if (!CanContinuouslyLeadToObjectCreationOrInvocation(node))
                {
                    return null;
                }

                node = node.Parent;
                var operation = semanticModel.GetOperation(node, cancellationToken);
                var arguments = (operation as IObjectCreationOperation)?.Arguments ?? (operation as IInvocationOperation)?.Arguments;

                if (arguments.HasValue)
                {
                    return arguments.Value;
                }
            }

            return null;
        }

        private async Task<ImmutableArray<KeyValuePair<DocumentId, SyntaxNode>>> GetNodesToRemoveAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan);
            SyntaxNode declarationNode = GetParameterDeclarationNode(diagnosticNode);

            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            ISymbol parameterSymbol = editor.SemanticModel.GetDeclaredSymbol(declarationNode, cancellationToken);
            ISymbol methodDeclarationSymbol = parameterSymbol.ContainingSymbol;

            if (!IsSafeMethodToRemoveParameter(methodDeclarationSymbol))
            {
                // See https://github.com/dotnet/roslyn-analyzers/issues/1466
                return ImmutableArray<KeyValuePair<DocumentId, SyntaxNode>>.Empty;
            }

            var nodesToRemove = ImmutableArray.CreateBuilder<KeyValuePair<DocumentId, SyntaxNode>>();
            nodesToRemove.Add(new KeyValuePair<DocumentId, SyntaxNode>(document.Id, declarationNode));
            var referencedSymbols = await SymbolFinder.FindReferencesAsync(methodDeclarationSymbol, document.Project.Solution, cancellationToken).ConfigureAwait(false);

            foreach (var referencedSymbol in referencedSymbols)
            {
                if (referencedSymbol.Locations != null)
                {
                    foreach (var referenceLocation in referencedSymbol.Locations)
                    {
                        Location location = referenceLocation.Location;
                        var referenceRoot = await location.SourceTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                        var referencedSymbolNode = referenceRoot.FindNode(location.SourceSpan);
                        DocumentEditor localEditor = await DocumentEditor.CreateAsync(referenceLocation.Document, cancellationToken).ConfigureAwait(false);
                        var arguments = GetOperationArguments(referencedSymbolNode, localEditor.SemanticModel, cancellationToken);

                        if (arguments != null)
                        {
                            foreach (IArgumentOperation argument in arguments)
                            {
                                // The name comparison below looks fragile. However, symbol comparison does not work for Reduced Extension Methods. Need to consider more reliable options.
                                if (string.Equals(argument.Parameter.Name, parameterSymbol.Name, StringComparison.Ordinal) && argument.ArgumentKind == ArgumentKind.Explicit)
                                {
                                    nodesToRemove.Add(new KeyValuePair<DocumentId, SyntaxNode>(referenceLocation.Document.Id, referenceRoot.FindNode(argument.Syntax.GetLocation().SourceSpan)));
                                }
                            }
                        }
                    }
                }
            }

            return nodesToRemove.ToImmutable();
        }

        private static bool IsSafeMethodToRemoveParameter(ISymbol methodDeclarationSymbol)
        {
            switch (methodDeclarationSymbol.Kind)
            {
                // Should not fix removing unused property indexer.
                case SymbolKind.Property:
                    return false;
                case SymbolKind.Method:
                    var methodSymbol = (IMethodSymbol)methodDeclarationSymbol;
                    // Should not remove parameter for a conversion operator.
                    return methodSymbol.MethodKind != MethodKind.Conversion;
                default:
                    return true;
            }
        }

        private sealed class MyCodeAction : SolutionChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Solution>> createChangedSolution, string equivalenceKey)
                : base(title, createChangedSolution, equivalenceKey) { }
        }
    }
}
