// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Performance;

namespace Microsoft.NetCore.CSharp.Analyzers.Performance
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix : UseStartsWithInsteadOfIndexOfComparisonWithZeroCodeFix
    {
        protected override SyntaxNode AppendElasticMarker(SyntaxNode replacement)
            => replacement.WithTrailingTrivia(SyntaxFactory.ElasticMarker);

        protected override SyntaxNode HandleCharStringComparisonOverload(SyntaxGenerator generator, SyntaxNode instance, SyntaxNode[] arguments, bool shouldNegate)
        {
            // For 'x.IndexOf(ch, stringComparison)', we switch to 'x.AsSpan().StartsWith(stackalloc char[1] { ch }, stringComparison)'
            var (argumentSyntax, index) = GetCharacterArgumentAndIndex(arguments);
            arguments[index] = argumentSyntax.WithExpression(SyntaxFactory.StackAllocArrayCreationExpression(
                SyntaxFactory.ArrayType(
                    (TypeSyntax)generator.TypeExpression(SpecialType.System_Char),
                    SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.SingletonSeparatedList((ExpressionSyntax)generator.LiteralExpression(1))))),
                SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression, SyntaxFactory.SingletonSeparatedList(argumentSyntax.Expression))
                ));
            instance = generator.InvocationExpression(generator.MemberAccessExpression(instance, "AsSpan")).WithAdditionalAnnotations(new SyntaxAnnotation("SymbolId", "System.MemoryExtensions")).WithAddImportsAnnotation();
            return CreateStartsWithInvocationFromArguments(generator, instance, arguments, shouldNegate);
        }

        private static (ArgumentSyntax Argument, int Index) GetCharacterArgumentAndIndex(SyntaxNode[] arguments)
        {
            var firstArgument = (ArgumentSyntax)arguments[0];
            if (firstArgument.NameColon is null or { Name.Identifier.Value: "value" })
            {
                return (firstArgument, 0);
            }

            return ((ArgumentSyntax)arguments[1], 1);
        }
    }
}
