// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.NetCore.Analyzers.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    /// <summary>
    /// CA1820: Test for empty strings using string length
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class CSharpTestForEmptyStringsUsingStringLengthFixer : TestForEmptyStringsUsingStringLengthFixer
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

        protected override bool ContainsEmptyStringLiteral(SyntaxNode node)
        => node is LiteralExpressionSyntax literalExpressionSyntax &&
        	literalExpressionSyntax.Token.ValueText.Length == 0;
    }
}
