// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeQuality.Analyzers.Maintainability;
using static Microsoft.CodeQuality.Analyzers.Maintainability.UseCrossPlatformIntrinsicsAnalyzer;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseCrossPlatformIntrinsicsFixer : UseCrossPlatformIntrinsicsFixer
    {
        protected override SyntaxNode ReplaceNode(SyntaxNode currentNode, SyntaxGenerator generator, RuleKind ruleKind)
        {
            return ruleKind switch
            {
                RuleKind.op_ExclusiveOr => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: true, generator.ExclusiveOrExpression),
                RuleKind.op_LeftShift => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: false, generator.LeftShiftExpression),
                RuleKind.op_RightShift => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: false, generator.RightShiftExpression),
                RuleKind.op_UnsignedRightShift => ReplaceWithBinaryOperator(currentNode, generator, isCommutative: false, generator.UnsignedRightShiftExpression),
                _ => base.ReplaceNode(currentNode, generator, ruleKind),
            };
        }

        protected override SyntaxNode ReplaceWithBinaryOperator(SyntaxNode currentNode, SyntaxGenerator generator, bool isCommutative, Func<SyntaxNode, SyntaxNode, SyntaxNode?> binaryOpFunc)
        {
            if (currentNode is not InvocationExpressionSyntax invocationExpression)
            {
                Debug.Fail($"Found unexpected node kind: {currentNode.RawKind}");
                return currentNode;
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpression.ArgumentList.Arguments;

            if (arguments.Count != 2)
            {
                Debug.Fail($"Found unexpected number of arguments for binary operator replacement: {arguments.Count}");
                return currentNode;
            }

            if (binaryOpFunc(arguments[0].Expression, arguments[1].Expression) is not ExpressionSyntax replacementExpression)
            {
                return currentNode;
            }

            return generator.Parenthesize(replacementExpression);
        }

        protected override SyntaxNode ReplaceWithUnaryOperator(SyntaxNode currentNode, SyntaxGenerator generator, Func<SyntaxNode, SyntaxNode?> unaryOpFunc)
        {
            if (currentNode is not InvocationExpressionSyntax invocationExpression)
            {
                Debug.Fail($"Found unexpected node kind: {currentNode.RawKind}");
                return currentNode;
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpression.ArgumentList.Arguments;

            if (arguments.Count != 1)
            {
                Debug.Fail($"Found unexpected number of arguments for unary operator replacement: {arguments.Count}");
                return currentNode;
            }

            if (unaryOpFunc(arguments[0].Expression) is not ExpressionSyntax replacementExpression)
            {
                return currentNode;
            }

            return generator.Parenthesize(replacementExpression);
        }

        protected override SyntaxNode ReplaceWithUnaryMethod(SyntaxNode currentNode, SyntaxGenerator generator, string methodName)
        {
            if (currentNode is not InvocationExpressionSyntax invocationExpression)
            {
                Debug.Fail($"Found unexpected node kind: {currentNode.RawKind}");
                return currentNode;
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpression.ArgumentList.Arguments;

            if (arguments.Count != 1)
            {
                Debug.Fail($"Found unexpected number of arguments for unary method replacement: {arguments.Count}");
                return currentNode;
            }

            // Get the type from the invocation expression's return type
            var typeArgumentSyntax = GetTypeArgumentFromInvocation(invocationExpression);
            if (typeArgumentSyntax == null)
            {
                Debug.Fail("Unable to extract type argument from invocation expression");
                return currentNode;
            }

            // Create the cross-platform method call: VectorXXX<T>.MethodName(arg)
            var vectorTypeExpression = generator.GenericName(GetVectorTypeName(invocationExpression), typeArgumentSyntax);
            var replacementExpression = generator.InvocationExpression(
                generator.MemberAccessExpression(vectorTypeExpression, methodName),
                arguments[0].Expression);

            return generator.Parenthesize(replacementExpression);
        }

        protected override SyntaxNode ReplaceWithBinaryMethod(SyntaxNode currentNode, SyntaxGenerator generator, string methodName)
        {
            if (currentNode is not InvocationExpressionSyntax invocationExpression)
            {
                Debug.Fail($"Found unexpected node kind: {currentNode.RawKind}");
                return currentNode;
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpression.ArgumentList.Arguments;

            if (arguments.Count != 2)
            {
                Debug.Fail($"Found unexpected number of arguments for binary method replacement: {arguments.Count}");
                return currentNode;
            }

            // Get the type from the invocation expression's return type
            var typeArgumentSyntax = GetTypeArgumentFromInvocation(invocationExpression);
            if (typeArgumentSyntax == null)
            {
                Debug.Fail("Unable to extract type argument from invocation expression");
                return currentNode;
            }

            // Create the cross-platform method call: VectorXXX<T>.MethodName(arg1, arg2)
            var vectorTypeExpression = generator.GenericName(GetVectorTypeName(invocationExpression), typeArgumentSyntax);
            var replacementExpression = generator.InvocationExpression(
                generator.MemberAccessExpression(vectorTypeExpression, methodName),
                arguments[0].Expression,
                arguments[1].Expression);

            return generator.Parenthesize(replacementExpression);
        }

        private static TypeSyntax? GetTypeArgumentFromInvocation(InvocationExpressionSyntax invocation)
        {
            // The invocation is something like Sse.Sqrt(Vector128<float>), we need to extract the <float> part
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName &&
                genericName.TypeArgumentList.Arguments.Count == 1)
            {
                return genericName.TypeArgumentList.Arguments[0];
            }

            return null;
        }

        private static string GetVectorTypeName(InvocationExpressionSyntax invocation)
        {
            // Determine the Vector type name (Vector64, Vector128, Vector256, Vector512) from the invocation
            // by looking at the return type's name
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name is GenericNameSyntax genericName)
            {
                var identifier = genericName.Identifier.Text;
                if (identifier.StartsWith("Vector"))
                {
                    return identifier;
                }
            }

            // Default to Vector128 if we can't determine it
            return "Vector128";
        }
    }
}
