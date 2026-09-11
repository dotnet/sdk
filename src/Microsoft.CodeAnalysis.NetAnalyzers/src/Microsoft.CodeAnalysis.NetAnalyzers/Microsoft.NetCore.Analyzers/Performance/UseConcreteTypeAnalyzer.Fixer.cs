// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1859: Use concrete types when possible for improved performance.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseConcreteTypeFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(UseConcreteTypeAnalyzer.RuleId);

        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(_ => null, ApplyFixAsync);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics[0];
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (!diagnostic.Properties.ContainsKey(UseConcreteTypeAnalyzer.TargetTypeDocumentationIdKey) ||
                IsPartOfMultiVariableDeclaration(semanticModel, diagnostic, context.CancellationToken))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    MicrosoftNetCoreAnalyzersResources.UseConcreteTypeTitle,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        context.Document,
                        ImmutableArray.Create(diagnostic),
                        (document, diagnostic, editor, token) => ApplyFixAsync(document, diagnostic, editor, equivalenceKey: null, token),
                        cancellationToken),
                    nameof(MicrosoftNetCoreAnalyzersResources.UseConcreteTypeTitle)),
                diagnostic);
        }

        private static async Task ApplyFixAsync(
            Document document,
            Diagnostic diagnostic,
            SyntaxEditor editor,
            string? equivalenceKey,
            CancellationToken cancellationToken)
        {
            if (!diagnostic.Properties.TryGetValue(UseConcreteTypeAnalyzer.TargetTypeDocumentationIdKey, out var documentationId) ||
                documentationId is null)
            {
                return;
            }

            var declaration = editor.Generator.GetDeclaration(
                editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true));
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (IsPartOfMultiVariableDeclaration(semanticModel, diagnostic, cancellationToken))
            {
                return;
            }

            var targetType = DocumentationCommentId.GetFirstSymbolForReferenceId(
                documentationId,
                semanticModel.Compilation) as ITypeSymbol;
            if (declaration is null || targetType is null)
            {
                return;
            }

            if (diagnostic.Properties.TryGetValue(UseConcreteTypeAnalyzer.TargetTypeNullableAnnotationsKey, out var nullableAnnotations) &&
                nullableAnnotations is not null)
            {
                var annotationIndex = 0;
                targetType = ApplyNullableAnnotations(targetType, nullableAnnotations, ref annotationIndex, semanticModel.Compilation);
            }

            editor.ReplaceNode(
                declaration,
                (currentDeclaration, generator) =>
                    generator.WithType(currentDeclaration, generator.TypeExpression(targetType)));
        }

        private static bool IsPartOfMultiVariableDeclaration(
            SemanticModel semanticModel,
            Diagnostic diagnostic,
            CancellationToken cancellationToken)
        {
            var node = semanticModel.SyntaxTree.GetRoot(cancellationToken)
                .FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var symbol = node.AncestorsAndSelf()
                .Select(node => semanticModel.GetDeclaredSymbol(node, cancellationToken))
                .FirstOrDefault(symbol => symbol is not null);
            if (symbol is not IFieldSymbol and not ILocalSymbol ||
                symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken).Parent is not SyntaxNode parent)
            {
                return false;
            }

            return parent.ChildNodes()
                .Select(node => semanticModel.GetDeclaredSymbol(node, cancellationToken))
                .Count(candidate => candidate?.Kind == symbol.Kind) > 1;
        }

        private static ITypeSymbol ApplyNullableAnnotations(
            ITypeSymbol type,
            string nullableAnnotations,
            ref int annotationIndex,
            Compilation compilation)
        {
            if (annotationIndex >= nullableAnnotations.Length)
            {
                return type;
            }

            var annotation = (NullableAnnotation)(nullableAnnotations[annotationIndex++] - '0');
            type = type switch
            {
                IArrayTypeSymbol array => compilation.CreateArrayTypeSymbol(
                    ApplyNullableAnnotations(array.ElementType, nullableAnnotations, ref annotationIndex, compilation),
                    array.Rank),
                INamedTypeSymbol { TypeArguments.Length: > 0 } named => ApplyTypeArgumentNullableAnnotations(
                    named,
                    nullableAnnotations,
                    ref annotationIndex,
                    compilation),
                IPointerTypeSymbol pointer => compilation.CreatePointerTypeSymbol(
                    ApplyNullableAnnotations(pointer.PointedAtType, nullableAnnotations, ref annotationIndex, compilation)),
                _ => type,
            };
            return type.WithNullableAnnotation(annotation);
        }

        private static INamedTypeSymbol ApplyTypeArgumentNullableAnnotations(
            INamedTypeSymbol type,
            string nullableAnnotations,
            ref int annotationIndex,
            Compilation compilation)
        {
            var typeArguments = ImmutableArray.CreateBuilder<ITypeSymbol>(type.TypeArguments.Length);
            foreach (var typeArgument in type.TypeArguments)
            {
                typeArguments.Add(ApplyNullableAnnotations(typeArgument, nullableAnnotations, ref annotationIndex, compilation));
            }

            return type.ConstructedFrom.Construct(typeArguments.ToArray());
        }
    }
}
