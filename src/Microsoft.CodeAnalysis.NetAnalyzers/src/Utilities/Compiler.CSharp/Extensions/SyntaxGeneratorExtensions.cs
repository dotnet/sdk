// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

// Analyzer complaines `this SyntaxGenerator generator` is unused, but its an extension
#pragma warning disable IDE0060

namespace Analyzer.Utilities.Extensions
{
    internal static partial class SyntaxGeneratorExtensions
    {
        public static SyntaxNode CreateBinaryExpression(this SyntaxGenerator generator, SyntaxKind syntaxKind, SyntaxNode left, SyntaxNode right)
            => SyntaxFactory.BinaryExpression(syntaxKind, (ExpressionSyntax)generator.Parenthesize(left), (ExpressionSyntax)generator.Parenthesize(right));

        public static SyntaxNode ExclusiveOrExpression(this SyntaxGenerator generator, SyntaxNode left, SyntaxNode right)
            => generator.CreateBinaryExpression(SyntaxKind.ExclusiveOrExpression, left, right);

        public static SyntaxNode LeftShiftExpression(this SyntaxGenerator generator, SyntaxNode left, SyntaxNode right)
            => generator.CreateBinaryExpression(SyntaxKind.LeftShiftExpression, left, right);

        public static SyntaxNode RightShiftExpression(this SyntaxGenerator generator, SyntaxNode left, SyntaxNode right)
            => generator.CreateBinaryExpression(SyntaxKind.RightShiftExpression, left, right);

        public static SyntaxNode? UnsignedRightShiftExpression(this SyntaxGenerator generator, SyntaxNode left, SyntaxNode right)
        {
            const LanguageVersion CSharp11 = (LanguageVersion)1100;

            if (!Enum.IsDefined(typeof(SyntaxKind), SyntaxKindEx.UnsignedRightShiftExpression))
            {
                return null;
            }

            if ((left.SyntaxTree.Options is not CSharpParseOptions csharpParseOptions) || (csharpParseOptions.LanguageVersion < CSharp11))
            {
                return null;
            }

            return generator.CreateBinaryExpression(SyntaxKindEx.UnsignedRightShiftExpression, left, right);
        }

        public static SyntaxNode Parenthesize(this SyntaxGenerator generator, SyntaxNode expressionOrPattern, bool includeElasticTrivia = true, bool addSimplifierAnnotation = true) => expressionOrPattern switch
        {
            ExpressionSyntax expression => expression.Parenthesize(includeElasticTrivia, addSimplifierAnnotation),
            PatternSyntax pattern => pattern.Parenthesize(includeElasticTrivia, addSimplifierAnnotation),
            _ => expressionOrPattern,
        };
    }
}
