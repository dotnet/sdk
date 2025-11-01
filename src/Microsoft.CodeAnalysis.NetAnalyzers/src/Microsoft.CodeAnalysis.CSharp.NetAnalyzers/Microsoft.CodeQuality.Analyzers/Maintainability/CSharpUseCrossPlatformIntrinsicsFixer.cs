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

            // Determine the vector type name from the return type if available
            var vectorTypeName = DetermineVectorTypeName(invocationExpression);

            // Create the cross-platform method call: VectorXXX.MethodName(arg)
            // The type parameter will be inferred from the argument
            var vectorTypeIdentifier = generator.IdentifierName(vectorTypeName);
            var replacementExpression = generator.InvocationExpression(
                generator.MemberAccessExpression(vectorTypeIdentifier, methodName),
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

            // Determine the vector type name from the return type if available
            var vectorTypeName = DetermineVectorTypeName(invocationExpression);

            // Create the cross-platform method call: VectorXXX.MethodName(arg1, arg2)
            // The type parameter will be inferred from the arguments
            var vectorTypeIdentifier = generator.IdentifierName(vectorTypeName);
            var replacementExpression = generator.InvocationExpression(
                generator.MemberAccessExpression(vectorTypeIdentifier, methodName),
                arguments[0].Expression,
                arguments[1].Expression);

            return generator.Parenthesize(replacementExpression);
        }

        protected override SyntaxNode ReplaceWithBinaryMethodSwapped(SyntaxNode currentNode, SyntaxGenerator generator, string methodName)
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

            // Determine the vector type name from the return type if available
            var vectorTypeName = DetermineVectorTypeName(invocationExpression);

            // Create the cross-platform method call with swapped parameters: VectorXXX.MethodName(arg2, arg1)
            // For example, Sse.AndNot(x, y) = ~x & y maps to Vector128.AndNot(y, x) = y & ~x
            var vectorTypeIdentifier = generator.IdentifierName(vectorTypeName);
            var replacementExpression = generator.InvocationExpression(
                generator.MemberAccessExpression(vectorTypeIdentifier, methodName),
                arguments[1].Expression,  // Swap: second argument first
                arguments[0].Expression); // Swap: first argument second

            return generator.Parenthesize(replacementExpression);
        }

        private static string DetermineVectorTypeName(SyntaxNode node)
        {
            // For method signatures like "Vector256<float> M(Vector256<float> x)",
            // we need to find the return type of the method containing this invocation
            
            // Walk up to find the method declaration
            var current = node;
            while (current != null)
            {
                if (current is MethodDeclarationSyntax methodDecl)
                {
                    // Check the return type
                    var returnType = methodDecl.ReturnType;
                    if (returnType is GenericNameSyntax genericReturn &&
                        IsVectorType(genericReturn.Identifier.Text))
                    {
                        return genericReturn.Identifier.Text;
                    }
                }
                current = current.Parent;
            }

            // Also check the invocation itself for argument types
            // This handles cases where the invocation is in an expression-bodied member
            if (node is InvocationExpressionSyntax invocation)
            {
                foreach (var arg in invocation.ArgumentList.Arguments)
                {
                    var vectorType = FindVectorTypeInExpression(arg.Expression);
                    if (vectorType != null)
                    {
                        return vectorType;
                    }
                }
            }

            // Default to Vector128 if we can't determine the vector type from context.
            // This fallback is used when the platform-specific intrinsic call is in a context
            // where we cannot infer the return type (e.g., passed as an argument to another method).
            // Vector128 is the most common vector size across all platforms.
            return "Vector128";
        }

        private static string? FindVectorTypeInExpression(SyntaxNode node)
        {
            // Look for Vector types in the expression (could be identifiers or generic names)
            foreach (var descendant in node.DescendantNodesAndSelf())
            {
                if (descendant is GenericNameSyntax genericName &&
                    IsVectorType(genericName.Identifier.Text))
                {
                    return genericName.Identifier.Text;
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if the given type name is a Vector type (Vector64, Vector128, Vector256, or Vector512).
        /// </summary>
        private static bool IsVectorType(string typeName)
            => typeName is "Vector64" or "Vector128" or "Vector256" or "Vector512";

        protected override SyntaxNode ReplaceWithTernaryMethod(SyntaxNode currentNode, SyntaxGenerator generator, string methodName)
        {
            if (currentNode is not InvocationExpressionSyntax invocationExpression)
            {
                Debug.Fail($"Found unexpected node kind: {currentNode.RawKind}");
                return currentNode;
            }

            SeparatedSyntaxList<ArgumentSyntax> arguments = invocationExpression.ArgumentList.Arguments;

            if (arguments.Count != 3)
            {
                Debug.Fail($"Found unexpected number of arguments for ternary method replacement: {arguments.Count}");
                return currentNode;
            }

            // Determine the vector type name from the return type if available
            var vectorTypeName = DetermineVectorTypeName(invocationExpression);

            // Create the cross-platform method call: VectorXXX.MethodName(arg1, arg2, arg3)
            // The type parameter will be inferred from the arguments
            var vectorTypeIdentifier = generator.IdentifierName(vectorTypeName);
            var replacementExpression = generator.InvocationExpression(
                generator.MemberAccessExpression(vectorTypeIdentifier, methodName),
                arguments[0].Expression,
                arguments[1].Expression,
                arguments[2].Expression);

            return generator.Parenthesize(replacementExpression);
        }
    }
}
