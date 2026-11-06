// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotCompareSpanToNullFixer : DoNotCompareSpanToNullFixer
    {
        protected override SyntaxNode? MakeIsEmptyCheck(SyntaxNode comparison)
        {
            if (comparison is not BinaryExpressionSyntax binaryExpression)
            {
                return null;
            }

            ExpressionSyntax memberAccess = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, GetComparatorExpression(binaryExpression), SyntaxFactory.IdentifierName(IsEmpty));
            if (binaryExpression.IsKind(SyntaxKind.NotEqualsExpression))
            {
                return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, memberAccess);
            }

            return memberAccess;
        }

        private static ExpressionSyntax GetComparatorExpression(BinaryExpressionSyntax binaryExpression)
        {
            return binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression)
                   || binaryExpression.Left.IsKind(SyntaxKind.DefaultLiteralExpression)
                   || binaryExpression.Left.IsKind(SyntaxKind.DefaultExpression)
                ? binaryExpression.Right
                : binaryExpression.Left;
        }
    }
}