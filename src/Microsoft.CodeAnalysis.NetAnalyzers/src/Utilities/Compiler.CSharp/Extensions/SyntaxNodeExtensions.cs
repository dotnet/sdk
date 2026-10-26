// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Analyzer.Utilities.Extensions
{
    // Several of these methods are cloned from the internal ExpressionSyntaxExtensions defined by Roslyn in Microsoft.CodeAnalysis.CSharp

    internal static partial class SyntaxNodeExtensions
    {
        public static SyntaxNode WalkDownParentheses(this SyntaxNode node)
        {
            SyntaxNode current = node;
            while (current.IsKind(SyntaxKind.ParenthesizedExpression) && current.ChildNodes().FirstOrDefault() is SyntaxNode expression)
            {
                current = expression;
            }

            return current;
        }

        public static ExpressionSyntax Parenthesize(
            this ExpressionSyntax expression, bool includeElasticTrivia = true, bool addSimplifierAnnotation = true)
        {
            // a 'ref' expression should never be parenthesized.  It fundamentally breaks the code.
            // This is because, from the language's perspective there is no such thing as a ref
            // expression.  instead, there are constructs like ```return ref expr``` or 
            // ```x ? ref expr1 : ref expr2```, or ```ref int a = ref expr``` in these cases, the 
            // ref's do not belong to the exprs, but instead belong to the parent construct. i.e.
            // ```return ref``` or ``` ? ref  ... : ref ... ``` or ``` ... = ref ...```.  For 
            // parsing convenience, and to prevent having to update all these constructs, we settled
            // on a ref-expression node.  But this node isn't a true expression that be operated
            // on like with everything else.
            if (expression.IsKind(SyntaxKind.RefExpression))
            {
                return expression;
            }

            // Throw expressions are not permitted to be parenthesized:
            //
            //     "a" ?? throw new ArgumentNullException()
            //
            // is legal whereas
            //
            //     "a" ?? (throw new ArgumentNullException())
            //
            // is not.
            if (expression.IsKind(SyntaxKind.ThrowExpression))
            {
                return expression;
            }

            var result = ParenthesizeWorker(expression, includeElasticTrivia);
            return addSimplifierAnnotation
                ? result.WithAdditionalAnnotations(Simplifier.Annotation)
                : result;
        }

        private static ExpressionSyntax ParenthesizeWorker(
            this ExpressionSyntax expression, bool includeElasticTrivia)
        {
            var withoutTrivia = expression.WithoutTrivia();
            var parenthesized = includeElasticTrivia
                ? SyntaxFactory.ParenthesizedExpression(withoutTrivia)
                : SyntaxFactory.ParenthesizedExpression(
                    SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.OpenParenToken, SyntaxTriviaList.Empty),
                    withoutTrivia,
                    SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.CloseParenToken, SyntaxTriviaList.Empty));

            return parenthesized.WithTriviaFrom(expression);
        }

        public static PatternSyntax Parenthesize(
            this PatternSyntax pattern, bool includeElasticTrivia = true, bool addSimplifierAnnotation = true)
        {
            var withoutTrivia = pattern.WithoutTrivia();
            var parenthesized = includeElasticTrivia
                ? SyntaxFactory.ParenthesizedPattern(withoutTrivia)
                : SyntaxFactory.ParenthesizedPattern(
                    SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.OpenParenToken, SyntaxTriviaList.Empty),
                    withoutTrivia,
                    SyntaxFactory.Token(SyntaxTriviaList.Empty, SyntaxKind.CloseParenToken, SyntaxTriviaList.Empty));

            var result = parenthesized.WithTriviaFrom(pattern);
            return addSimplifierAnnotation
                ? result.WithAdditionalAnnotations(Simplifier.Annotation)
                : result;
        }
    }
}
