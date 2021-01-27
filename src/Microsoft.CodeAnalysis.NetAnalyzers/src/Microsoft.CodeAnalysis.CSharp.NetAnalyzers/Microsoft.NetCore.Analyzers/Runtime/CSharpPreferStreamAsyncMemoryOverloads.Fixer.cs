// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class CSharpPreferStreamAsyncMemoryOverloadsFixer : PreferStreamAsyncMemoryOverloadsFixer
    {
        protected override SyntaxNode? GetArgumentByPositionOrName(IInvocationOperation invocation, int index, string name, out bool isNamed)
        {
            isNamed = false;

            if (index < invocation.Arguments.Length &&
                invocation.Syntax is InvocationExpressionSyntax expression)
            {
                var args = invocation.Arguments;
                // If the argument in the specified index does not have a name, then it is in its expected position
                if (args[index].Syntax is ArgumentSyntax argNode && argNode.NameColon == null)
                {
                    return args[index].Syntax;
                }
                // The argument in the specified index does not have a name but is part of a nullable expression
                else if (args[index].Syntax is IdentifierNameSyntax identifierNameNode &&
                    identifierNameNode.Identifier.Value.Equals(name) &&
                    identifierNameNode.Parent is PostfixUnaryExpressionSyntax nullableExpression)
                {
                    return nullableExpression;
                }
                // Otherwise, find it by name
                else
                {
                    IArgumentOperation? operation = args.FirstOrDefault(argOperation =>
                    {
                        return argOperation.Syntax is ArgumentSyntax argNode &&
                               argNode.NameColon?.Name?.Identifier.ValueText == name;
                    });

                    if (operation != null)
                    {
                        isNamed = true;
                        return operation.Syntax;
                    }
                }
            }

            return null;
        }

        protected override bool IsSystemNamespaceImported(IReadOnlyList<SyntaxNode> importList)
        {
            foreach (SyntaxNode import in importList)
            {
                if (import is UsingDirectiveSyntax { Name: IdentifierNameSyntax { Identifier: { Text: nameof(System) } } })
                {
                    return true;
                }
            }
            return false;
        }

        protected override bool IsPassingZeroAndBufferLength(SemanticModel model, SyntaxNode bufferValueNode, SyntaxNode offsetValueNode, SyntaxNode countValueNode)
        {
            // First argument should be an identifier name node
            if (bufferValueNode is ArgumentSyntax arg1 &&
                arg1.Expression is IdentifierNameSyntax firstArgumentIdentifierName)
            {
                // Second argument should be a literal expression node with a constant value of zero
                if (offsetValueNode is ArgumentSyntax arg2 &&
                    arg2.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.Value is int value && value == 0)
                {
                    // Third argument should be a member access node...
                    if (countValueNode is ArgumentSyntax arg3 &&
                        arg3.Expression is MemberAccessExpressionSyntax thirdArgumentMemberAccessExpression &&
                        thirdArgumentMemberAccessExpression.Expression is IdentifierNameSyntax thirdArgumentIdentifierName &&
                        // whose identifier is that of the first argument...
                        firstArgumentIdentifierName.Identifier.ValueText == thirdArgumentIdentifierName.Identifier.ValueText &&
                        // and the member name is `Length`
                        thirdArgumentMemberAccessExpression.Name.Identifier.ValueText == WellKnownMemberNames.LengthPropertyName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected override SyntaxNode GetNodeWithNullability(IInvocationOperation invocation)
        {
            if (invocation.Syntax is InvocationExpressionSyntax invocationExpression &&
                invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessExpression &&
                memberAccessExpression.Expression is PostfixUnaryExpressionSyntax postfixUnaryExpression)
            {
                return postfixUnaryExpression;
            }

            return invocation.Instance.Syntax;
        }

        protected override SyntaxNode GetNamedArgument(SyntaxGenerator generator, SyntaxNode node, bool isNamed, string newName)
        {
            if (isNamed)
            {
                SyntaxNode actualNode = node;

                if (node is ArgumentSyntax argument)
                {
                    actualNode = argument.Expression;
                }

                return generator.Argument(name: newName, RefKind.None, actualNode);
            }

            return node;
        }

        protected override SyntaxNode GetNamedMemberInvocation(SyntaxGenerator generator, SyntaxNode node, string memberName)
        {
            SyntaxNode actualNode = node;

            if (node is ArgumentSyntax argument)
            {
                actualNode = argument.Expression;
            }

            return generator.MemberAccessExpression(actualNode.WithoutTrivia(), memberName);
        }
    }
}
