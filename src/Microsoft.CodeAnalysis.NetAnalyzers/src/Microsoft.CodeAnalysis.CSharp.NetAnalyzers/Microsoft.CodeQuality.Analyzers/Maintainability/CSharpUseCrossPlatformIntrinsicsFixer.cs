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
    }
}
