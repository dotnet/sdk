// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2242: Test for NaN correctly
    /// </summary>
    public abstract class TestForNaNCorrectlyFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(TestForNaNCorrectlyAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (TryGetFixResolution(root.FindNode(context.Span), model, context.CancellationToken) is not null)
            {
                RegisterCodeFix(context, MicrosoftNetCoreAnalyzersResources.TestForNaNCorrectlyMessage, MicrosoftNetCoreAnalyzersResources.TestForNaNCorrectlyMessage);
            }
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (TryGetFixResolution(node, model, cancellationToken) is not FixResolution resolution)
            {
                return;
            }

            // The comparison operand is re-emitted, and `(a == float.NaN ? b : c) == float.NaN` diagnoses both
            // comparisons, so it is read off the node as an inner fix has already rewritten it.
            editor.ReplaceNode(resolution.BinaryExpressionSyntax, (currentNode, generator) =>
            {
                SyntaxNode comparisonOperand = resolution.NanIsLeftOperand ? GetRightOperand(currentNode) : GetLeftOperand(currentNode);
                SyntaxNode typeNameSyntax = generator.TypeExpression(resolution.FloatingSystemType);
                SyntaxNode nanMemberSyntax = generator.MemberAccessExpression(typeNameSyntax, "IsNaN");
                SyntaxNode nanMemberInvocationSyntax = generator.InvocationExpression(nanMemberSyntax, comparisonOperand);

                SyntaxNode replacementSyntax = resolution.UsesEqualsOperator ? nanMemberInvocationSyntax : generator.LogicalNotExpression(nanMemberInvocationSyntax);
                return replacementSyntax.WithAdditionalAnnotations(Formatter.Annotation);
            });
        }

        private FixResolution? TryGetFixResolution(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
        {
            SyntaxNode binaryExpressionSyntax = GetBinaryExpression(node);

            bool isEqualsOperator = IsEqualsOperator(binaryExpressionSyntax);
            if (!isEqualsOperator && !IsNotEqualsOperator(binaryExpressionSyntax))
            {
                return null;
            }

            ITypeSymbol? systemTypeLeft = TryGetSystemTypeForNanConstantExpression(GetLeftOperand(binaryExpressionSyntax), model, cancellationToken);
            if (systemTypeLeft != null)
            {
                return new FixResolution(binaryExpressionSyntax, systemTypeLeft, nanIsLeftOperand: true, isEqualsOperator);
            }

            ITypeSymbol? systemTypeRight = TryGetSystemTypeForNanConstantExpression(GetRightOperand(binaryExpressionSyntax), model, cancellationToken);
            if (systemTypeRight != null)
            {
                return new FixResolution(binaryExpressionSyntax, systemTypeRight, nanIsLeftOperand: false, isEqualsOperator);
            }

            return null;
        }

        private static ITypeSymbol? TryGetSystemTypeForNanConstantExpression(SyntaxNode expressionSyntax, SemanticModel model, CancellationToken cancellationToken)
        {
            var symbol = model.GetSymbolInfo(expressionSyntax, cancellationToken).Symbol;
            if (symbol is IFieldSymbol { HasConstantValue: true, Name: "NaN", Type: { SpecialType: SpecialType.System_Single or SpecialType.System_Double } type })
            {
                return type;
            }

            return null;
        }

        protected abstract SyntaxNode GetBinaryExpression(SyntaxNode node);
        protected abstract bool IsEqualsOperator(SyntaxNode node);
        protected abstract bool IsNotEqualsOperator(SyntaxNode node);
        protected abstract SyntaxNode GetLeftOperand(SyntaxNode binaryExpressionSyntax);
        protected abstract SyntaxNode GetRightOperand(SyntaxNode binaryExpressionSyntax);

        private sealed class FixResolution
        {
            public SyntaxNode BinaryExpressionSyntax { get; }
            public ITypeSymbol FloatingSystemType { get; }
            public bool NanIsLeftOperand { get; }
            public bool UsesEqualsOperator { get; }

            public FixResolution(SyntaxNode binaryExpressionSyntax, ITypeSymbol floatingSystemType, bool nanIsLeftOperand, bool usesEqualsOperator)
            {
                BinaryExpressionSyntax = binaryExpressionSyntax;
                FloatingSystemType = floatingSystemType;
                NanIsLeftOperand = nanIsLeftOperand;
                UsesEqualsOperator = usesEqualsOperator;
            }
        }
    }
}
