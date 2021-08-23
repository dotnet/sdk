// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1836: Prefer IsEmpty over Count when available.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferIsEmptyOverCountFixer : PreferIsEmptyOverCountFixer
    {
        protected override SyntaxNode? GetObjectExpressionFromOperation(SyntaxNode node, string operationKey)
        {
            SyntaxNode? countNode = null;
            switch (operationKey)
            {
                case UseCountProperlyAnalyzer.OperationBinaryLeft:
                    if (node is BinaryExpressionSyntax binaryExpression)
                    {
                        countNode = binaryExpression.Left;
                    }
                    break;
                case UseCountProperlyAnalyzer.OperationBinaryRight:
                    if (node is BinaryExpressionSyntax binaryExpression2)
                    {
                        countNode = binaryExpression2.Right;
                    }
                    break;
                case UseCountProperlyAnalyzer.OperationEqualsArgument:
                    if (node is InvocationExpressionSyntax invocationExpression)
                    {
                        countNode = invocationExpression.ArgumentList.Arguments[0].Expression;
                    }
                    break;
                case UseCountProperlyAnalyzer.OperationEqualsInstance:
                    if (node is InvocationExpressionSyntax invocationExpression2)
                    {
                        SyntaxNode equalsMemberAccess = invocationExpression2.Expression;
                        if (equalsMemberAccess is MemberAccessExpressionSyntax memberAccess)
                        {
                            countNode = memberAccess.Expression;
                        }
                    }
                    break;
            }

            RoslynDebug.Assert(countNode != null);

            bool isParenthesizedOrCastExpression;
            do
            {
                isParenthesizedOrCastExpression = true;

                switch (countNode)
                {
                    case ParenthesizedExpressionSyntax parenthesizedExpression:
                        countNode = parenthesizedExpression.Expression;
                        break;
                    case CastExpressionSyntax castExpression:
                        countNode = castExpression.Expression;
                        break;
                    default:
                        isParenthesizedOrCastExpression = false;
                        break;
                }
            }
            while (isParenthesizedOrCastExpression);

            if (countNode is InvocationExpressionSyntax invocationExpression3)
            {
                countNode = invocationExpression3.Expression;
            }

            SyntaxNode? objectNode = null;

            if (countNode is MemberAccessExpressionSyntax memberAccess2)
            {
                objectNode = memberAccess2.Expression;
            }

            RoslynDebug.Assert(objectNode != null || countNode is IdentifierNameSyntax);
            return objectNode;
        }
    }
}
