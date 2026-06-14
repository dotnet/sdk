// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer : DoNotUseEnumerableMethodsOnIndexableCollectionsInsteadUseTheCollectionDirectlyFixer
    {
        private protected sealed override SyntaxNode CreateLastElementAccessExpression(Document document,SyntaxGenerator generator,SyntaxNode adjustedCollectionSyntaxNoTrailingTrivia,SyntaxNode collectionSyntax)
        {
            if (document.Project.ParseOptions is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp8 })
            {
                var expression = (ExpressionSyntax)adjustedCollectionSyntaxNoTrailingTrivia;

                var indexExpression = PrefixUnaryExpression(
                    SyntaxKind.IndexExpression,
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1)));

                return ElementAccessExpression(
                    expression,
                    BracketedArgumentList(
                        SingletonSeparatedList(
                            Argument(indexExpression))));
            }

            return base.CreateLastElementAccessExpression(document, generator, adjustedCollectionSyntaxNoTrailingTrivia, collectionSyntax);
        }
        private protected sealed override SyntaxNode? AdjustSyntaxNode(SyntaxNode? syntaxNode)
        {
            if (syntaxNode?.Parent.IsKind(SyntaxKind.SuppressNullableWarningExpression) == true)
            {
                return syntaxNode.Parent;
            }

            return syntaxNode;
        }
    }
}
