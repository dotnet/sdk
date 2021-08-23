// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1822: Mark members as static
    /// </summary>
    public abstract class MarkMembersAsStaticFixer : CodeFixProvider
    {
        private static readonly SyntaxAnnotation s_annotationForFixedDeclaration = new();

        protected abstract IEnumerable<SyntaxNode>? GetTypeArguments(SyntaxNode node);
        protected abstract SyntaxNode? GetExpressionOfInvocation(SyntaxNode invocation);
        protected virtual SyntaxNode GetSyntaxNodeToReplace(IMemberReferenceOperation memberReference)
            => memberReference.Syntax;

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(MarkMembersAsStaticAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            if (node == null)
            {
                return;
            }

            context.RegisterCodeFix(
                new MarkMembersAsStaticAction(
                    MicrosoftCodeQualityAnalyzersResources.MarkMembersAsStaticCodeFix,
                    ct => MakeStaticAsync(context.Document, root, node, ct)),
                context.Diagnostics);
        }

        private async Task<Solution> MakeStaticAsync(Document document, SyntaxNode root, SyntaxNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Add syntax annotation to definition to be made static so we can update it after fixing references.
            document = document.WithSyntaxRoot(root.ReplaceNode(node, node.WithAdditionalAnnotations(s_annotationForFixedDeclaration)));
            var solution = document.Project.Solution;

            // Update references, if any.
            root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            node = root.GetAnnotatedNodes(s_annotationForFixedDeclaration).Single();
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var symbol = semanticModel.GetDeclaredSymbol(node, cancellationToken);
            if (symbol != null)
            {
                var (newSolution, allReferencesFixed) = await UpdateReferencesAsync(symbol, solution, cancellationToken).ConfigureAwait(false);
                solution = newSolution;

                if (!allReferencesFixed)
                {
                    // We could not fix all references, so add a warning annotation that users need to manually fix these.
                    document = await AddWarningAnnotationAsync(solution.GetDocument(document.Id)!, symbol, cancellationToken).ConfigureAwait(false);
                    solution = document.Project.Solution;
                }
            }

            // Update definition to add static modifier.
            document = solution.GetDocument(document.Id)!;
            root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            node = root.GetAnnotatedNodes(s_annotationForFixedDeclaration).Single();
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            var oldModifiersAndStatic = syntaxGenerator.GetModifiers(node).WithIsStatic(true);
            var newNode = syntaxGenerator.WithModifiers(node, oldModifiersAndStatic);
            return document.WithSyntaxRoot(root.ReplaceNode(node, newNode)).Project.Solution;
        }

        /// <summary>
        /// Returns the updated solution and a flag indicating if all references were fixed or not.
        /// </summary>
        private async Task<(Solution newSolution, bool allReferencesFixed)> UpdateReferencesAsync(ISymbol symbol, Solution solution, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var references = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellationToken).ConfigureAwait(false);

            // Filter out cascaded symbol references. For example, accessor references for property symbol.
            references = references.Where(r => symbol.Equals(r.Definition));

            if (!references.HasExactly(1))
            {
                return (newSolution: solution, allReferencesFixed: !references.Any());
            }

            var allReferencesFixed = true;

            // Group references by document and fix references in each document.
            foreach (var referenceLocationGroup in references.Single().Locations.GroupBy(r => r.Document))
            {
                // Get document in current solution
                var document = solution.GetDocument(referenceLocationGroup.Key.Id);

                // Skip references in projects with different language.
                // https://github.com/dotnet/roslyn-analyzers/issues/1986 tracks handling them.
                if (document == null ||
                    !document.Project.Language.Equals(symbol.Language, StringComparison.Ordinal))
                {
                    allReferencesFixed = false;
                    continue;
                }

                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                // Compute replacements
                var editor = new SyntaxEditor(root, solution.Workspace);
                foreach (var referenceLocation in referenceLocationGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var referenceNode = root.FindNode(referenceLocation.Location.SourceSpan, getInnermostNodeForTie: true);
                    if (referenceNode == null)
                    {
                        allReferencesFixed = false;
                        continue;
                    }

                    var operation = semanticModel.GetOperationWalkingUpParentChain(referenceNode, cancellationToken);
                    SyntaxNode? nodeToReplace = null;
                    switch (operation)
                    {
                        case IMemberReferenceOperation memberReference:
                            if (IsReplacableOperation(memberReference.Instance))
                            {
                                nodeToReplace = GetSyntaxNodeToReplace(memberReference);
                            }

                            break;

                        case IInvocationOperation invocation:
                            if (IsReplacableOperation(invocation.Instance))
                            {
                                nodeToReplace = GetExpressionOfInvocation(invocation.Syntax);
                            }

                            break;
                    }

                    if (nodeToReplace == null)
                    {
                        allReferencesFixed = false;
                        continue;
                    }

                    // Fetch the symbol for the node to replace - note that this might be
                    // different from the original symbol due to generic type arguments.
                    var symbolForNodeToReplace = GetSymbolForNodeToReplace(nodeToReplace, semanticModel);
                    if (symbolForNodeToReplace == null)
                    {
                        allReferencesFixed = false;
                        continue;
                    }

                    SyntaxNode memberName;
                    var typeArguments = GetTypeArguments(referenceNode);
                    memberName = typeArguments != null ?
                        editor.Generator.GenericName(symbolForNodeToReplace.Name, typeArguments) :
                        editor.Generator.IdentifierName(symbolForNodeToReplace.Name);

                    var newNode = editor.Generator.MemberAccessExpression(
                            expression: editor.Generator.TypeExpression(symbolForNodeToReplace.ContainingType),
                            memberName: memberName)
                        .WithLeadingTrivia(nodeToReplace.GetLeadingTrivia())
                        .WithTrailingTrivia(nodeToReplace.GetTrailingTrivia())
                        .WithAdditionalAnnotations(Formatter.Annotation);

                    editor.ReplaceNode(nodeToReplace, newNode);
                }

                document = document.WithSyntaxRoot(editor.GetChangedRoot());
                solution = document.Project.Solution;
            }

            return (solution, allReferencesFixed);

            // Local functions.
            static bool IsReplacableOperation(IOperation operation)
            {
                if (operation == null)
                {
                    // Null instance is replaceable. For example, null instance for a static field/property reference which is used to invoke an instance member, say "SomeType.StaticField.InstanceMethod();"
                    return true;
                }

                // We only replace reference operations whose removal cannot change semantics.
                switch (operation.Kind)
                {
                    case OperationKind.InstanceReference:
                    case OperationKind.ParameterReference:
                    case OperationKind.LocalReference:
                        return true;

                    case OperationKind.FieldReference:
                    case OperationKind.PropertyReference:
                        return IsReplacableOperation(((IMemberReferenceOperation)operation).Instance);

                    default:
                        return false;
                }
            }

            ISymbol? GetSymbolForNodeToReplace(SyntaxNode nodeToReplace, SemanticModel semanticModel)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(nodeToReplace, cancellationToken);
                var symbolForNodeToReplace = symbolInfo.Symbol;

                if (symbolForNodeToReplace == null &&
                    symbolInfo.CandidateReason == CandidateReason.StaticInstanceMismatch &&
                    symbolInfo.CandidateSymbols.Length == 1)
                {
                    return symbolInfo.CandidateSymbols[0];
                }

                return symbolForNodeToReplace;
            }
        }

        private static async Task<Document> AddWarningAnnotationAsync(Document document, ISymbol symbolFromEarlierSnapshot, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var fixedDeclaration = root.GetAnnotatedNodes(s_annotationForFixedDeclaration).Single();
            var annotation = WarningAnnotation.Create(string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.MarkMembersAsStaticCodeFix_WarningAnnotation, symbolFromEarlierSnapshot.Name));
            return document.WithSyntaxRoot(root.ReplaceNode(fixedDeclaration, fixedDeclaration.WithAdditionalAnnotations(annotation)));
        }

        private class MarkMembersAsStaticAction : SolutionChangeAction
        {
            public MarkMembersAsStaticAction(string title, Func<CancellationToken, Task<Solution>> createChangedSolution)
                : base(title, createChangedSolution, equivalenceKey: title)
            {
            }
        }
    }
}
