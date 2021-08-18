// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    /// <summary>
    /// CA1827: Do not use Count()/LongCount() when Any() can be used.
    /// CA1828: Do not use CountAsync()/LongCountAsync() when AnyAsync() can be used.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotUseCountWhenAnyCanBeUsedFixer : DoNotUseCountWhenAnyCanBeUsedFixer
    {
        /// <summary>
        /// Tries to get a fixer for the specified <paramref name="node" />.
        /// </summary>
        /// <param name="node">The node to get a fixer for.</param>
        /// <param name="operation">The operation to get the fixer from.</param>
        /// <param name="isAsync"><see langword="true" /> if it's an asynchronous method; <see langword="false" /> otherwise.</param>
        /// <param name="expression">If this method returns <see langword="true" />, contains the expression to be used to invoke <c>Any</c>.</param>
        /// <param name="arguments">If this method returns <see langword="true" />, contains the arguments from <c>Any</c> to be used on <c>Count</c>.</param>
        /// <returns><see langword="true" /> if a fixer was found., <see langword="false" /> otherwise.</returns>
        protected override bool TryGetFixer(
            SyntaxNode node,
            string operation,
            bool isAsync,
            [NotNullWhen(returnValue: true)] out SyntaxNode? expression,
            [NotNullWhen(returnValue: true)] out IEnumerable<SyntaxNode>? arguments)
        {
            switch (operation)
            {
                case UseCountProperlyAnalyzer.OperationEqualsInstance:
                    {
                        if (node is InvocationExpressionSyntax invocation &&
                            invocation.Expression is MemberAccessExpressionSyntax member &&
                            TryGetExpressionAndInvocationArguments(
                                sourceExpression: member.Expression,
                                isAsync: isAsync,
                                expression: out expression,
                                arguments: out arguments))
                        {
                            return true;
                        }

                        break;
                    }
                case UseCountProperlyAnalyzer.OperationEqualsArgument:
                    {
                        if (node is InvocationExpressionSyntax invocation &&
                            invocation.ArgumentList.Arguments.Count == 1 &&
                            TryGetExpressionAndInvocationArguments(
                                sourceExpression: invocation.ArgumentList.Arguments[0].Expression,
                                isAsync: isAsync,
                                expression: out expression,
                                arguments: out arguments))
                        {
                            return true;
                        }

                        break;
                    }
                case UseCountProperlyAnalyzer.OperationBinaryLeft:
                    {
                        if (node is BinaryExpressionSyntax binary &&
                            TryGetExpressionAndInvocationArguments(
                                sourceExpression: binary.Left,
                                isAsync: isAsync,
                                expression: out expression,
                                arguments: out arguments))
                        {
                            return true;
                        }

                        break;
                    }
                case UseCountProperlyAnalyzer.OperationBinaryRight:
                    {
                        if (node is BinaryExpressionSyntax binary &&
                            TryGetExpressionAndInvocationArguments(
                                sourceExpression: binary.Right,
                                isAsync: isAsync,
                                expression: out expression,
                                arguments: out arguments))
                        {
                            return true;
                        }

                        break;
                    }
            }

            expression = default;
            arguments = default;
            return false;
        }

        private static bool TryGetExpressionAndInvocationArguments(
            ExpressionSyntax sourceExpression,
            bool isAsync,
            [NotNullWhen(returnValue: true)] out SyntaxNode? expression,
            [NotNullWhen(returnValue: true)] out IEnumerable<SyntaxNode>? arguments)
        {
            while (sourceExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
            {
                sourceExpression = parenthesizedExpression.Expression;
            }

            InvocationExpressionSyntax? invocationExpression = null;

            if (isAsync)
            {
                if (sourceExpression is AwaitExpressionSyntax awaitExpressionSyntax)
                {
                    invocationExpression = awaitExpressionSyntax.Expression as InvocationExpressionSyntax;
                }
            }
            else
            {
                invocationExpression = sourceExpression as InvocationExpressionSyntax;
            }

            if (invocationExpression is null)
            {
                expression = default;
                arguments = default;
                return false;
            }

            expression = ((MemberAccessExpressionSyntax)invocationExpression.Expression).Expression;
            arguments = invocationExpression.ArgumentList.ChildNodes();
            return true;
        }
    }
}
