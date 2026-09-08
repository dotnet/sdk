// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;
using System.Threading;
using Analyzer.Utilities;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1820: Test for empty strings using string length
    /// </summary>
    public abstract class TestForEmptyStringsUsingStringLengthFixer : CodeFixProvider
    {
        private const string TestForEmptyStringCorrectlyUsingIsNullOrEmpty = nameof(TestForEmptyStringCorrectlyUsingIsNullOrEmpty);
        private const string TestForEmptyStringCorrectlyUsingStringLength = nameof(TestForEmptyStringCorrectlyUsingStringLength);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(TestForEmptyStringsUsingStringLengthAnalyzer.RuleId);

        //  Two fixes are offered for the same diagnostic, so the equivalence key decides which one a
        //  fix-all applies. SyntaxEditorFixAllProvider does not filter on it.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            SyntaxNode expressionSyntax = GetExpression(node);

            if (!IsFixableBinaryExpression(expressionSyntax) && !IsFixableInvocationExpression(expressionSyntax))
            {
                return;
            }

            SemanticModel model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            FixResolution? resolution = TryGetFixResolution(expressionSyntax, model, context.CancellationToken);

            if (resolution != null)
            {
                context.RegisterCodeFix(CreateCodeAction(context, TestForEmptyStringCorrectlyUsingIsNullOrEmpty), context.Diagnostics);
                context.RegisterCodeFix(CreateCodeAction(context, TestForEmptyStringCorrectlyUsingStringLength), context.Diagnostics);
            }
        }

        private CodeAction CreateCodeAction(CodeFixContext context, string equivalenceKey)
        {
            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            return CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.TestForEmptyStringsUsingStringLengthMessage,
                ct => SyntaxEditorFixAllProvider.ApplyFixesAsync(document, diagnostics, (doc, diagnostic, editor, token) => ApplyFixAsync(doc, diagnostic, editor, equivalenceKey, token), ct),
                equivalenceKey);
        }

        private async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            SyntaxNode expressionSyntax = GetExpression(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));

            if (!IsFixableBinaryExpression(expressionSyntax) && !IsFixableInvocationExpression(expressionSyntax))
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (TryGetFixResolution(expressionSyntax, model, cancellationToken) is not FixResolution resolution)
            {
                return;
            }

            if (equivalenceKey == TestForEmptyStringCorrectlyUsingIsNullOrEmpty)
            {
                ConvertToMethodInvocation(editor, resolution);
            }
            else if (equivalenceKey == TestForEmptyStringCorrectlyUsingStringLength)
            {
                ConvertToStringLengthComparison(editor, resolution);
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

        private static void ConvertToMethodInvocation(SyntaxEditor editor, FixResolution fixResolution)
        {
            //  The replacement carries the target over from inside the node being replaced, so track it:
            //  a nested violation may already have rewritten it.
            editor.TrackNode(fixResolution.Target);

            editor.ReplaceNode(fixResolution.ExpressionSyntax, (currentNode, generator) =>
            {
                SyntaxNode target = currentNode.GetCurrentNode(fixResolution.Target) ?? fixResolution.Target;

                SyntaxNode typeNameSyntax = generator.TypeExpression(SpecialType.System_String);
                SyntaxNode nullOrEmptyMemberSyntax = generator.MemberAccessExpression(typeNameSyntax, "IsNullOrEmpty");
                SyntaxNode nullOrEmptyInvocationSyntax = generator.InvocationExpression(nullOrEmptyMemberSyntax, target.WithoutTrailingTrivia());

                SyntaxNode replacementSyntax = fixResolution.UsesEqualsOperator ? nullOrEmptyInvocationSyntax : generator.LogicalNotExpression(nullOrEmptyInvocationSyntax);

                return replacementSyntax.WithAdditionalAnnotations(Formatter.Annotation).WithTriviaFrom(currentNode);
            });
        }

        private void ConvertToStringLengthComparison(SyntaxEditor editor, FixResolution fixResolution)
        {
            SyntaxNode originalLeftOperand = GetLeftOperand(fixResolution.ExpressionSyntax);
            SyntaxNode originalRightOperand = GetRightOperand(fixResolution.ExpressionSyntax);
            bool targetIsLeftOperand = originalLeftOperand == fixResolution.Target;

            editor.TrackNode(originalLeftOperand);
            editor.TrackNode(originalRightOperand);

            editor.ReplaceNode(fixResolution.ExpressionSyntax, (currentNode, generator) =>
            {
                SyntaxNode leftOperand = currentNode.GetCurrentNode(originalLeftOperand) ?? originalLeftOperand;
                SyntaxNode rightOperand = currentNode.GetCurrentNode(originalRightOperand) ?? originalRightOperand;

                // Take the below example:
                //   if (f == String.Empty) ...
                // The comparison operand, f, will now become 'f.Length' and a the other operand will become '0'
                SyntaxNode zeroLengthSyntax = generator.LiteralExpression(0);
                if (targetIsLeftOperand)
                {
                    leftOperand = generator.MemberAccessExpression(leftOperand, "Length");
                    rightOperand = zeroLengthSyntax.WithTriviaFrom(rightOperand);
                }
                else
                {
                    leftOperand = zeroLengthSyntax;
                    rightOperand = generator.MemberAccessExpression(rightOperand.WithoutTrivia(), "Length");
                }

                SyntaxNode replacementSyntax = fixResolution.UsesEqualsOperator ?
                    generator.ValueEqualsExpression(leftOperand, rightOperand) :
                    generator.ValueNotEqualsExpression(leftOperand, rightOperand);

                return replacementSyntax.WithAdditionalAnnotations(Formatter.Annotation);
            });
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
