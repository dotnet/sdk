// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA2242: Test for NaN correctly
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpTestForNaNCorrectlyFixer : TestForNaNCorrectlyFixer
    {
        protected override SyntaxNode GetBinaryExpression(SyntaxNode node)
        {
            return node is ArgumentSyntax argumentSyntax ? argumentSyntax.Expression : node;
        }

        protected override bool IsEqualsOperator(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.EqualsExpression);
        }

        protected override bool IsNotEqualsOperator(SyntaxNode node)
        {
            return node.IsKind(SyntaxKind.NotEqualsExpression);
        }

        protected override SyntaxNode GetLeftOperand(SyntaxNode binaryExpressionSyntax)
        {
            return ((BinaryExpressionSyntax)binaryExpressionSyntax).Left;
        }

        protected override SyntaxNode GetRightOperand(SyntaxNode binaryExpressionSyntax)
        {
            return ((BinaryExpressionSyntax)binaryExpressionSyntax).Right;
        }
    }
}
