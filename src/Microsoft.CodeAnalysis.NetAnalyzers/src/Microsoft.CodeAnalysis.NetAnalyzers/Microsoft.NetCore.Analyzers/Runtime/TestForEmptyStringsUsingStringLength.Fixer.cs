// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA1820: Test for empty strings using string length
    /// </summary>
    public abstract class TestForEmptyStringsUsingStringLengthFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(TestForEmptyStringsUsingStringLengthAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            SyntaxNode expressionSyntax = GetExpression(node);

            if (!IsFixableBinaryExpression(expressionSyntax) && !IsFixableInvocationExpression(expressionSyntax))
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            FixResolution? resolution = TryGetFixResolution(expressionSyntax, model, context.CancellationToken);

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

        private FixResolution? TryGetFixResolution(SyntaxNode expressionSyntax, SemanticModel model, CancellationToken cancellationToken)
        {
            if (IsFixableBinaryExpression(expressionSyntax))
            {
                bool isEqualsOperator = IsEqualsOperator(expressionSyntax);
                SyntaxNode leftOperand = GetLeftOperand(expressionSyntax);
                SyntaxNode rightOperand = GetRightOperand(expressionSyntax);

                if (ContainsSystemStringEmpty(leftOperand, model, cancellationToken) || ContainsEmptyStringLiteral(leftOperand, model, cancellationToken))
                {
                    return new FixResolution(expressionSyntax, rightOperand, isEqualsOperator);
                }

                if (ContainsSystemStringEmpty(rightOperand, model, cancellationToken) || ContainsEmptyStringLiteral(rightOperand, model, cancellationToken))
                {
                    return new FixResolution(expressionSyntax, leftOperand, isEqualsOperator);
                }
            }
            else if (IsFixableInvocationExpression(expressionSyntax))
            {
                SyntaxNode? target = GetInvocationTarget(expressionSyntax);

                if (target == null)
                {
                    return null;
                }

                return new FixResolution(expressionSyntax, target, true);
            }

            return null;
        }

        private static bool ContainsSystemStringEmpty(SyntaxNode expressionSyntax, SemanticModel model, CancellationToken cancellationToken)
        {
            if (model.GetSymbolInfo(expressionSyntax, cancellationToken).Symbol is IFieldSymbol fieldSymbol)
            {
                if (fieldSymbol.Type.SpecialType == SpecialType.System_String)
                {
                    return fieldSymbol.IsReadOnly && fieldSymbol.Name == "Empty";
                }
            }

            return false;
        }

        private static async Task<Document> ConvertToMethodInvocation(CodeFixContext context, FixResolution fixResolution)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);

            SyntaxNode typeNameSyntax = editor.Generator.TypeExpression(SpecialType.System_String);
            SyntaxNode nullOrEmptyMemberSyntax = editor.Generator.MemberAccessExpression(typeNameSyntax, "IsNullOrEmpty");
            SyntaxNode nullOrEmptyInvocationSyntax = editor.Generator.InvocationExpression(nullOrEmptyMemberSyntax, fixResolution.Target.WithoutTrailingTrivia());

            SyntaxNode replacementSyntax = fixResolution.UsesEqualsOperator ? nullOrEmptyInvocationSyntax : editor.Generator.LogicalNotExpression(nullOrEmptyInvocationSyntax);
            SyntaxNode replacementAnnotatedSyntax = replacementSyntax.WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(fixResolution.ExpressionSyntax);

            editor.ReplaceNode(fixResolution.ExpressionSyntax, replacementAnnotatedSyntax);

            return editor.GetChangedDocument();
        }

        private async Task<Document> ConvertToStringLengthComparison(CodeFixContext context, FixResolution fixResolution)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);
            SyntaxNode leftOperand = GetLeftOperand(fixResolution.ExpressionSyntax);
            SyntaxNode rightOperand = GetRightOperand(fixResolution.ExpressionSyntax);

            // Take the below example:
            //   if (f == String.Empty) ...
            // The comparison operand, f, will now become 'f.Length' and a the other operand will become '0'
            SyntaxNode zeroLengthSyntax = editor.Generator.LiteralExpression(0);
            if (leftOperand == fixResolution.Target)
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

            editor.ReplaceNode(fixResolution.ExpressionSyntax, replacementAnnotatedSyntax);

            return editor.GetChangedDocument();
        }

        private static bool ContainsEmptyStringLiteral(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
            => model.GetConstantValue(node, cancellationToken) is Optional<object> optionalValue &&
            optionalValue.HasValue && optionalValue.Value is string value && value.Length == 0;

        protected abstract SyntaxNode GetExpression(SyntaxNode node);
        protected abstract bool IsFixableBinaryExpression(SyntaxNode node);
        protected abstract bool IsFixableInvocationExpression(SyntaxNode node);
        protected abstract bool IsEqualsOperator(SyntaxNode node);
        protected abstract bool IsNotEqualsOperator(SyntaxNode node);
        protected abstract SyntaxNode GetLeftOperand(SyntaxNode binaryExpressionSyntax);
        protected abstract SyntaxNode GetRightOperand(SyntaxNode binaryExpressionSyntax);
        protected abstract SyntaxNode? GetInvocationTarget(SyntaxNode node);

        private sealed class FixResolution
        {
            public SyntaxNode ExpressionSyntax { get; }
            public SyntaxNode Target { get; }
            public bool UsesEqualsOperator { get; }

            public FixResolution(SyntaxNode expressionSyntax, SyntaxNode target, bool usesEqualsOperator)
            {
                ExpressionSyntax = expressionSyntax;
                Target = target;
                UsesEqualsOperator = usesEqualsOperator;
            }
        }
    }
}
