// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferLengthCountIsEmptyOverAnyFixer : PreferLengthCountIsEmptyOverAnyFixer
    {
        protected override SyntaxNode? ReplaceAnyWithIsEmpty(SyntaxNode root, SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
            {
                return null;
            }

            var expression = memberAccess.Expression;
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                expression = invocation.ArgumentList.Arguments[0].Expression;
            }

            var newMemberAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                IdentifierName(PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText)
            );
            if (invocation.Parent.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return root.ReplaceNode(invocation.Parent, newMemberAccess.WithTriviaFrom(invocation.Parent));
            }

            var negatedExpression = PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                newMemberAccess
            );

            return root.ReplaceNode(invocation, negatedExpression.WithTriviaFrom(invocation));
        }

        protected override SyntaxNode? ReplaceAnyWithLength(SyntaxNode root, SyntaxNode node)
        {
            return ReplaceAnyWithPropertyCheck(root, node, PreferLengthCountIsEmptyOverAnyAnalyzer.LengthText);
        }

        protected override SyntaxNode? ReplaceAnyWithCount(SyntaxNode root, SyntaxNode node)
        {
            return ReplaceAnyWithPropertyCheck(root, node, PreferLengthCountIsEmptyOverAnyAnalyzer.CountText);
        }

        private static SyntaxNode? ReplaceAnyWithPropertyCheck(SyntaxNode root, SyntaxNode node, string propertyName)
        {
            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
            {
                return null;
            }

            var expression = memberAccess.Expression;
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                // .Any() used like a normal static method and not like an extension method.
                expression = invocation.ArgumentList.Arguments[0].Expression;
            }

            static BinaryExpressionSyntax GetBinaryExpression(ExpressionSyntax expression, string member, SyntaxKind expressionKind)
            {
                return BinaryExpression(
                    expressionKind,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        expression,
                        IdentifierName(member)
                    ),
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(0)
                    )
                );
            }

            if (invocation.Parent.IsKind(SyntaxKind.LogicalNotExpression))
            {
                var binaryExpression = GetBinaryExpression(expression, propertyName, SyntaxKind.EqualsExpression);

                return root.ReplaceNode(invocation.Parent, binaryExpression.WithTriviaFrom(invocation.Parent));
            }

            return root.ReplaceNode(invocation, GetBinaryExpression(expression, propertyName, SyntaxKind.NotEqualsExpression).WithTriviaFrom(invocation));
        }
    }
}