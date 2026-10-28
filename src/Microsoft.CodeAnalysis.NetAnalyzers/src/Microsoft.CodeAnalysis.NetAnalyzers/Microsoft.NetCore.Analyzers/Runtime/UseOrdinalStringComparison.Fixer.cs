// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class UseOrdinalStringComparisonFixerBase : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseOrdinalStringComparisonAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (!await CanFixAsync(context.Document, root.FindNode(context.Span), context.CancellationToken).ConfigureAwait(false))
            {
                return;
            }

            string title = MicrosoftNetCoreAnalyzersResources.UseOrdinalStringComparisonTitle;
            RegisterCodeFix(context, title, title);
        }

        /// <summary>
        /// Reports whether the fix rewrites <paramref name="node"/>. The analyzer reports every unacceptable
        /// overload, including the ones no added argument can turn into an acceptable one, so registering
        /// without this would offer an action that leaves the document unchanged.
        /// </summary>
        private async Task<bool> CanFixAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            if (IsInArgumentContext(node))
            {
                return true;
            }

            if (!IsInIdentifierNameContext(node))
            {
                return false;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            return model.GetSymbolInfo(node, cancellationToken).Symbol is IMethodSymbol methodSymbol &&
                CanAddStringComparison(methodSymbol, model);
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (IsInArgumentContext(node))
            {
                // StringComparison.CurrentCulture => StringComparison.Ordinal
                // StringComparison.CurrentCultureIgnoreCase => StringComparison.OrdinalIgnoreCase
                FixArgument(node, editor);
                return;
            }

            // string.Equals(a, b) => string.Equals(a, b, StringComparison.Ordinal)
            // string.Compare(a, b) => string.Compare(a, b, StringComparison.Ordinal)
            if (!IsInIdentifierNameContext(node) || GetInvocation(node) is not SyntaxNode invocation)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (model.GetSymbolInfo(node, cancellationToken).Symbol is not IMethodSymbol methodSymbol ||
                !CanAddStringComparison(methodSymbol, model))
            {
                return;
            }

            // The new invocation carries over the original's arguments, so it has to be built from the
            // invocation as the fixes before this one left it rather than off the original tree.
            editor.ReplaceNode(
                invocation,
                (currentInvocation, generator) => AddArgument(
                    currentInvocation,
                    generator.Argument(CreateOrdinalMemberAccess(generator, model)).WithAdditionalAnnotations(Formatter.Annotation)));
        }

        protected abstract bool IsInArgumentContext(SyntaxNode node);
        protected abstract void FixArgument(SyntaxNode argument, SyntaxEditor editor);

        protected abstract bool IsInIdentifierNameContext(SyntaxNode node);

        /// <summary>
        /// Returns the invocation <paramref name="identifier"/> names, or <see langword="null"/> when there is none.
        /// </summary>
        protected abstract SyntaxNode? GetInvocation(SyntaxNode identifier);

        /// <summary>
        /// Appends <paramref name="argument"/> to <paramref name="invocation"/>'s argument list.
        /// </summary>
        protected abstract SyntaxNode AddArgument(SyntaxNode invocation, SyntaxNode argument);

        internal static SyntaxNode CreateOrdinalMemberAccess(SyntaxGenerator generator, SemanticModel model)
        {
            INamedTypeSymbol stringComparisonType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison)!;
            return generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(stringComparisonType),
                generator.IdentifierName(UseOrdinalStringComparisonAnalyzer.OrdinalText));
        }

        protected static bool CanAddStringComparison(IMethodSymbol methodSymbol, SemanticModel model)
        {
            if (model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemStringComparison) == null)
            {
                return false;
            }

            ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;
            switch (methodSymbol.Name)
            {
                case UseOrdinalStringComparisonAnalyzer.EqualsMethodName:
                    // can fix .Equals() with (string), (string, string)
                    switch (parameters.Length)
                    {
                        case 1:
                            return parameters[0].Type.SpecialType == SpecialType.System_String;
                        case 2:
                            return parameters[0].Type.SpecialType == SpecialType.System_String &&
                                parameters[1].Type.SpecialType == SpecialType.System_String;
                    }

                    break;
                case UseOrdinalStringComparisonAnalyzer.CompareMethodName:
                    // can fix .Compare() with (string, string), (string, int, string, int, int)
                    switch (parameters.Length)
                    {
                        case 2:
                            return parameters[0].Type.SpecialType == SpecialType.System_String &&
                                parameters[1].Type.SpecialType == SpecialType.System_String;
                        case 5:
                            return parameters[0].Type.SpecialType == SpecialType.System_String &&
                                parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                                parameters[2].Type.SpecialType == SpecialType.System_String &&
                                parameters[3].Type.SpecialType == SpecialType.System_Int32 &&
                                parameters[4].Type.SpecialType == SpecialType.System_Int32;
                    }

                    break;
            }

            return false;
        }
    }
}
