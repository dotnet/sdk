// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1820: Test for empty strings using string length
    /// </summary>
    public abstract class TestForEmptyStringsUsingStringLengthFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(TestForEmptyStringsUsingStringLengthAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
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

            FixResolution? resolution = TryGetFixResolution(binaryExpressionSyntax, model);

            if (resolution != null)
            {
                var methodInvocationAction = CodeAction.Create(MicrosoftNetCoreAnalyzersResources.TestForEmptyStringsUsingStringLengthMessage,
                    async ct => await ConvertToMethodInvocation(context, resolution).ConfigureAwait(false),
                    equivalenceKey: "TestForEmptyStringCorrectlyUsingIsNullOrEmpty");

                context.RegisterCodeFix(methodInvocationAction, context.Diagnostics);

                var stringLengthAction = CodeAction.Create(MicrosoftNetCoreAnalyzersResources.TestForEmptyStringsUsingStringLengthMessage,
                    async ct => await ConvertToStringLengthComparison(context, resolution).ConfigureAwait(false),
                    equivalenceKey: "TestForEmptyStringCorrectlyUsingStringLength");

                context.RegisterCodeFix(stringLengthAction, context.Diagnostics);
            }
        }

        private FixResolution? TryGetFixResolution(SyntaxNode binaryExpressionSyntax, SemanticModel model)
        {
            bool isEqualsOperator = IsEqualsOperator(binaryExpressionSyntax);
            SyntaxNode leftOperand = GetLeftOperand(binaryExpressionSyntax);
            SyntaxNode rightOperand = GetRightOperand(binaryExpressionSyntax);

            if (ContainsSystemStringEmpty(leftOperand, model))
            {
                return new FixResolution(binaryExpressionSyntax, rightOperand, isEqualsOperator);
            }

            if (ContainsSystemStringEmpty(rightOperand, model))
            {
                return new FixResolution(binaryExpressionSyntax, leftOperand, isEqualsOperator);
            }

            return null;
        }

        private static bool ContainsSystemStringEmpty(SyntaxNode expressionSyntax, SemanticModel model)
        {
            if (model.GetSymbolInfo(expressionSyntax).Symbol is IFieldSymbol fieldSymbol)
            {
                if (fieldSymbol.Type.SpecialType == SpecialType.System_String)
                {
                    return (fieldSymbol.IsReadOnly && fieldSymbol.Name == "Empty") ||
                        fieldSymbol.ConstantValue != null && ((string)fieldSymbol.ConstantValue).Length == 0;
                }
            }

            return false;
        }

        private static async Task<Document> ConvertToMethodInvocation(CodeFixContext context, FixResolution fixResolution)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);

            SyntaxNode typeNameSyntax = editor.Generator.TypeExpression(SpecialType.System_String);
            SyntaxNode nullOrEmptyMemberSyntax = editor.Generator.MemberAccessExpression(typeNameSyntax, "IsNullOrEmpty");
            SyntaxNode nullOrEmptyInvocationSyntax = editor.Generator.InvocationExpression(nullOrEmptyMemberSyntax, fixResolution.ComparisonOperand.WithoutTrailingTrivia());

            SyntaxNode replacementSyntax = fixResolution.UsesEqualsOperator ? nullOrEmptyInvocationSyntax : editor.Generator.LogicalNotExpression(nullOrEmptyInvocationSyntax);
            SyntaxNode replacementAnnotatedSyntax = replacementSyntax.WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(fixResolution.BinaryExpressionSyntax);

            editor.ReplaceNode(fixResolution.BinaryExpressionSyntax, replacementAnnotatedSyntax);

            return editor.GetChangedDocument();
        }

        private async Task<Document> ConvertToStringLengthComparison(CodeFixContext context, FixResolution fixResolution)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);
            SyntaxNode leftOperand = GetLeftOperand(fixResolution.BinaryExpressionSyntax);
            SyntaxNode rightOperand = GetRightOperand(fixResolution.BinaryExpressionSyntax);

            // Take the below example:
            //   if (f == String.Empty) ...
            // The comparison operand, f, will now become 'f.Length' and a the other operand will become '0'
            SyntaxNode zeroLengthSyntax = editor.Generator.LiteralExpression(0);
            if (leftOperand == fixResolution.ComparisonOperand)
            {
                leftOperand = editor.Generator.MemberAccessExpression(leftOperand, "Length");
                rightOperand = zeroLengthSyntax.WithTriviaFrom(rightOperand);
            }
            else
            {
                leftOperand = zeroLengthSyntax;
                rightOperand = editor.Generator.MemberAccessExpression(rightOperand.WithoutTrivia(), "Length");
            }

            SyntaxNode replacementSyntax = fixResolution.UsesEqualsOperator ?
                editor.Generator.ValueEqualsExpression(leftOperand, rightOperand) :
                editor.Generator.ValueNotEqualsExpression(leftOperand, rightOperand);

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
            public SyntaxNode ComparisonOperand { get; }
            public bool UsesEqualsOperator { get; }

            public FixResolution(SyntaxNode binaryExpressionSyntax, SyntaxNode comparisonOperand, bool usesEqualsOperator)
            {
                BinaryExpressionSyntax = binaryExpressionSyntax;
                ComparisonOperand = comparisonOperand;
                UsesEqualsOperator = usesEqualsOperator;
            }
        }
    }
}
