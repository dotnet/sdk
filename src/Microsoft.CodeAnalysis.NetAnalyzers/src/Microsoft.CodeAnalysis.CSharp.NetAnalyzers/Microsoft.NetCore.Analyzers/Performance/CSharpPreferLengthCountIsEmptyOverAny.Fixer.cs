// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using System.Diagnostics.CodeAnalysis;
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
        protected override SyntaxNode? GetNodeToReplace(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax } invocation)
            {
                return null;
            }

            return invocation.Parent.IsKind(SyntaxKind.LogicalNotExpression) ? invocation.Parent : invocation;
        }

        protected override SyntaxNode? ReplaceAnyWithIsEmpty(SyntaxNode currentNode)
        {
            if (!TrySplit(currentNode, out bool isNegated, out ExpressionSyntax? expression))
            {
                return null;
            }

            var newMemberAccess = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                IdentifierName(PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText)
            );

            if (isNegated)
            {
                return newMemberAccess.WithTriviaFrom(currentNode);
            }

            return PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                newMemberAccess
            ).WithTriviaFrom(currentNode);
        }

        protected override SyntaxNode? ReplaceAnyWithPropertyCheck(SyntaxNode currentNode, string propertyName)
        {
            if (!TrySplit(currentNode, out bool isNegated, out ExpressionSyntax? expression))
            {
                return null;
            }

            return BinaryExpression(
                isNegated ? SyntaxKind.EqualsExpression : SyntaxKind.NotEqualsExpression,
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    expression,
                    IdentifierName(propertyName)
                ),
                LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    Literal(0)
                )
            ).WithTriviaFrom(currentNode);
        }

        private static bool TrySplit(SyntaxNode currentNode, out bool isNegated, [NotNullWhen(true)] out ExpressionSyntax? expression)
        {
            isNegated = currentNode is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression };
            SyntaxNode operand = isNegated ? ((PrefixUnaryExpressionSyntax)currentNode).Operand : currentNode;

            if (operand is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } invocation)
            {
                expression = null;

                return false;
            }

            // `.Any()` used like a normal static method and not like an extension method.
            expression = invocation.ArgumentList.Arguments.Count > 0
                ? invocation.ArgumentList.Arguments[0].Expression
                : memberAccess.Expression;

            return true;
        }
    }
}