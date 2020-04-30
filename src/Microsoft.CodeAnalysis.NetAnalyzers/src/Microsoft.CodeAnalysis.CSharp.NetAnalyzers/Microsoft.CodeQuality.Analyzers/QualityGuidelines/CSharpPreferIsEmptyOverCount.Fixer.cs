// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1836: Prefer IsEmpty over Count when available.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpPreferIsEmptyOverCountFixer : PreferIsEmptyOverCountFixer
    {
        protected override SyntaxNode? GetExpressionFromMemberAccessor(SyntaxNode node)
        {
            while (node is ParenthesizedExpressionSyntax parenthesizedExpression)
            {
                node = parenthesizedExpression.Expression;
            }

            if (node is MemberAccessExpressionSyntax ma)
            {
                return ma.Expression;
            }

            RoslynDebug.Assert(node is IdentifierNameSyntax);
            return null;
        }

        protected override SyntaxNode GetMemberAccessorFromBinary(SyntaxNode binaryExpression, bool useRightSide)
        {
            var csharpBinaryExpression = (BinaryExpressionSyntax)binaryExpression;
            return useRightSide ? csharpBinaryExpression.Right : csharpBinaryExpression.Left;
        }
    }
}
