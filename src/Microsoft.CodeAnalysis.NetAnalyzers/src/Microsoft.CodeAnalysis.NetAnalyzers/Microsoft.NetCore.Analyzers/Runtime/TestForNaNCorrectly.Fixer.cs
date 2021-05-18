// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using System.Threading;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2242: Test for NaN correctly
    /// </summary>
    public abstract class TestForNaNCorrectlyFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(TestForNaNCorrectlyAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            SyntaxNode binaryExpressionSyntax = GetBinaryExpression(node);

            if (!IsEqualsOperator(binaryExpressionSyntax) && !IsNotEqualsOperator(binaryExpressionSyntax))
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            INamedTypeSymbol? systemSingleType = model.Compilation.GetSpecialType(SpecialType.System_Single);
            INamedTypeSymbol? systemDoubleType = model.Compilation.GetSpecialType(SpecialType.System_Double);

            if (systemSingleType == null || systemDoubleType == null)
            {
                return;
            }

            FixResolution? resolution = TryGetFixResolution(binaryExpressionSyntax, model, systemSingleType, systemDoubleType, context.CancellationToken);

            if (resolution != null)
            {
                var action = CodeAction.Create(MicrosoftNetCoreAnalyzersResources.TestForNaNCorrectlyMessage,
                    async ct => await ConvertToMethodInvocation(context, resolution).ConfigureAwait(false),
                    equivalenceKey: MicrosoftNetCoreAnalyzersResources.TestForNaNCorrectlyMessage);

                context.RegisterCodeFix(action, context.Diagnostics);
            }
        }

        private FixResolution? TryGetFixResolution(SyntaxNode binaryExpressionSyntax, SemanticModel model, INamedTypeSymbol systemSingleType, INamedTypeSymbol systemDoubleType, CancellationToken cancellationToken)
        {
            bool isEqualsOperator = IsEqualsOperator(binaryExpressionSyntax);
            SyntaxNode leftOperand = GetLeftOperand(binaryExpressionSyntax);
            SyntaxNode rightOperand = GetRightOperand(binaryExpressionSyntax);

            ITypeSymbol? systemTypeLeft = TryGetSystemTypeForNanConstantExpression(leftOperand, model, systemSingleType, systemDoubleType, cancellationToken);
            if (systemTypeLeft != null)
            {
                return new FixResolution(binaryExpressionSyntax, systemTypeLeft, rightOperand, isEqualsOperator);
            }

            ITypeSymbol? systemTypeRight = TryGetSystemTypeForNanConstantExpression(rightOperand, model, systemSingleType, systemDoubleType, cancellationToken);
            if (systemTypeRight != null)
            {
                return new FixResolution(binaryExpressionSyntax, systemTypeRight, leftOperand, isEqualsOperator);
            }

            return null;
        }

        private static ITypeSymbol? TryGetSystemTypeForNanConstantExpression(SyntaxNode expressionSyntax, SemanticModel model, INamedTypeSymbol systemSingleType, INamedTypeSymbol systemDoubleType, CancellationToken cancellationToken)
        {
            if (model.GetSymbolInfo(expressionSyntax, cancellationToken).Symbol is IFieldSymbol fieldSymbol)
            {
                if (fieldSymbol.Type.Equals(systemSingleType) || fieldSymbol.Type.Equals(systemDoubleType))
                {
                    if (fieldSymbol.HasConstantValue && fieldSymbol.Name == "NaN")
                    {
                        return fieldSymbol.Type;
                    }
                }
            }

            return null;
        }

        private static async Task<Document> ConvertToMethodInvocation(CodeFixContext context, FixResolution fixResolution)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);

            SyntaxNode typeNameSyntax = editor.Generator.TypeExpression(fixResolution.FloatingSystemType);
            SyntaxNode nanMemberSyntax = editor.Generator.MemberAccessExpression(typeNameSyntax, "IsNaN");
            SyntaxNode nanMemberInvocationSyntax = editor.Generator.InvocationExpression(nanMemberSyntax, fixResolution.ComparisonOperand);

            SyntaxNode replacementSyntax = fixResolution.UsesEqualsOperator ? nanMemberInvocationSyntax : editor.Generator.LogicalNotExpression(nanMemberInvocationSyntax);
            SyntaxNode replacementAnnotatedSyntax = replacementSyntax.WithAdditionalAnnotations(Formatter.Annotation);

            editor.ReplaceNode(fixResolution.BinaryExpressionSyntax, replacementAnnotatedSyntax);

            return editor.GetChangedDocument();
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
            public SyntaxNode ComparisonOperand { get; }
            public bool UsesEqualsOperator { get; }

            public FixResolution(SyntaxNode binaryExpressionSyntax, ITypeSymbol floatingSystemType, SyntaxNode comparisonOperand, bool usesEqualsOperator)
            {
                BinaryExpressionSyntax = binaryExpressionSyntax;
                FloatingSystemType = floatingSystemType;
                ComparisonOperand = comparisonOperand;
                UsesEqualsOperator = usesEqualsOperator;
            }
        }
    }
}
