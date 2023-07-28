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
        protected override IEnumerable<SyntaxNode> GetNewArgumentsForInvocation(SyntaxGenerator generator, string caseChangingApproachValue, IInvocationOperation mainInvocationOperation,
            INamedTypeSymbol stringComparisonType, out SyntaxNode? mainInvocationInstance)
        {
            List<SyntaxNode> arguments = new();
            bool isAnyArgumentNamed = false;

            InvocationExpressionSyntax invocationExpression = (InvocationExpressionSyntax)mainInvocationOperation.Syntax;

            bool isChangingCaseInArgument = false;
            mainInvocationInstance = null;

            if (invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression)
            {
                ExpressionSyntax internalExpression = memberAccessExpression.Expression is ParenthesizedExpressionSyntax parenthesizedExpression ?
                    parenthesizedExpression.Expression :
                    memberAccessExpression.Expression;

                if (internalExpression is InvocationExpressionSyntax internalInvocationExpression &&
                    internalInvocationExpression.Expression is MemberAccessExpressionSyntax internalMemberAccessExpression)
                {
                    mainInvocationInstance = internalMemberAccessExpression.Expression;
                }
                else
                {
                    mainInvocationInstance = memberAccessExpression.Expression;
                    isChangingCaseInArgument = true;
                }
            }

            foreach (ArgumentSyntax node in invocationExpression.ArgumentList.Arguments)
            {
                string? argumentName = node.NameColon?.Name.Identifier.ValueText;
                isAnyArgumentNamed |= argumentName != null;

                ExpressionSyntax argumentExpression = node.Expression is ParenthesizedExpressionSyntax argumentParenthesizedExpression ?
                    argumentParenthesizedExpression.Expression :
                    node.Expression;

                MemberAccessExpressionSyntax? argumentMemberAccessExpression = null;
                if (argumentExpression is InvocationExpressionSyntax argumentInvocationExpression)
                {
                    argumentMemberAccessExpression = argumentInvocationExpression.Expression as MemberAccessExpressionSyntax;
                }

                SyntaxNode newArgumentNode;
                if (isChangingCaseInArgument)
                {
                    if (argumentMemberAccessExpression != null)
                    {
                        newArgumentNode = argumentName == RCISCAnalyzer.StringParameterName ?
                            generator.Argument(RCISCAnalyzer.StringParameterName, RefKind.None, argumentMemberAccessExpression.Expression) :
                            generator.Argument(argumentMemberAccessExpression.Expression);
                    }
                    else
                    {
                        newArgumentNode = node;
                    }
                }
                else
                {
                    newArgumentNode = node;
                }

                arguments.Add(newArgumentNode.WithTriviaFrom(node));
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