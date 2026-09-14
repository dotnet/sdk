// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    using RCISCAnalyzer = RecommendCaseInsensitiveStringComparisonAnalyzer;

    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpRecommendCaseInsensitiveStringComparisonFixer : RecommendCaseInsensitiveStringComparisonFixer
    {
        protected override IEnumerable<SyntaxNode> GetNewArgumentsForInvocation(SyntaxGenerator generator,
            string caseChangingApproachValue, IInvocationOperation mainInvocationOperation, INamedTypeSymbol stringComparisonType,
            string? leftOffendingMethod, string? rightOffendingMethod, out SyntaxNode? mainInvocationInstance)
        {
            InvocationExpressionSyntax invocationExpression = (InvocationExpressionSyntax)mainInvocationOperation.Syntax;

            mainInvocationInstance = null;

            if (invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression)
            {
                ExpressionSyntax internalExpression = memberAccessExpression.Expression;
                while (internalExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
                {
                    internalExpression = parenthesizedExpression.Expression;
                }

                if (leftOffendingMethod != null &&
                    internalExpression is InvocationExpressionSyntax internalInvocationExpression &&
                    internalInvocationExpression.Expression is MemberAccessExpressionSyntax internalMemberAccessExpression &&
                    internalMemberAccessExpression.Name.Identifier.Text == leftOffendingMethod)
                {
                    // We know we have an instance invocation that is an offending method, retrieve just the instance
                    mainInvocationInstance = internalMemberAccessExpression.Expression;
                }
                else
                {
                    mainInvocationInstance = memberAccessExpression.Expression;
                }
            }

            List<SyntaxNode> arguments = new();
            bool isAnyArgumentNamed = false;

            foreach (IArgumentOperation arg in mainInvocationOperation.Arguments)
            {
                SyntaxNode newArgumentNode;

                // When accessing the main invocation operation arguments, the bottom operation is retrieved
                // so we need to go up until we find the actual argument syntax ancestor, and skip through any
                // parenthesized syntax nodes
                SyntaxNode actualArgumentNode = arg.Syntax;
                while (actualArgumentNode is not ArgumentSyntax)
                {
                    actualArgumentNode = actualArgumentNode.Parent!;
                }

                string? argumentName = ((ArgumentSyntax)actualArgumentNode).NameColon?.Name.Identifier.ValueText;
                isAnyArgumentNamed |= argumentName != null;

                // The arguments could be named and out of order, so we need to detect the string parameter
                // and remove the offending invocation if there is one
                if (rightOffendingMethod != null && arg.Parameter?.Type?.Name == StringTypeName)
                {
                    ExpressionSyntax? desiredExpression = null;
                    if (arg.Syntax is ArgumentSyntax argumentExpression)
                    {
                        desiredExpression = argumentExpression.Expression;
                        while (desiredExpression is ParenthesizedExpressionSyntax parenthesizedExpression)
                        {
                            desiredExpression = parenthesizedExpression.Expression;
                        }
                    }
                    else if (arg.Syntax is InvocationExpressionSyntax argumentInvocationExpression)
                    {
                        desiredExpression = argumentInvocationExpression;
                    }

                    if (desiredExpression is InvocationExpressionSyntax invocation &&
                        invocation.Expression is MemberAccessExpressionSyntax argumentMemberAccessExpression)
                    {
                        newArgumentNode = argumentName == RCISCAnalyzer.StringParameterName ?
                                generator.Argument(RCISCAnalyzer.StringParameterName, RefKind.None, argumentMemberAccessExpression.Expression) :
                                generator.Argument(argumentMemberAccessExpression.Expression);
                    }
                    else
                    {
                        newArgumentNode = arg.Syntax;
                    }
                }
                else
                {
                    newArgumentNode = arg.Syntax;
                }

                arguments.Add(newArgumentNode.WithTriviaFrom(arg.Syntax));
            }

            Debug.Assert(mainInvocationInstance != null);

            SyntaxNode stringComparisonArgument = GetNewStringComparisonArgument(generator, stringComparisonType, caseChangingApproachValue, isAnyArgumentNamed);

            arguments.Add(stringComparisonArgument);

            return arguments;
        }

        protected override IEnumerable<SyntaxNode> GetNewArgumentsForBinary(SyntaxGenerator generator, SyntaxNode rightNode, SyntaxNode typeMemberAccess) =>
            new List<SyntaxNode>()
            {
                generator.Argument(rightNode),
                generator.Argument(typeMemberAccess)
            };
    }
}