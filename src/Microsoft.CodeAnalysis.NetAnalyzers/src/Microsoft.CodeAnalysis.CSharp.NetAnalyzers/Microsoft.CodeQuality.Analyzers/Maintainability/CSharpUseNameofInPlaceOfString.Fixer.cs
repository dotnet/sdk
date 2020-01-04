// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    /// <summary>
    /// CA1507: Use nameof to express symbol names
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseNameofInPlaceOfStringFixer : UseNameOfInPlaceOfStringFixer
    {
        protected override SyntaxNode GetNameOfExpression(SyntaxGenerator generator, string identifierNameArgument)
        {
            // Workaround for https://github.com/dotnet/roslyn/issues/24212
            // Once the above Roslyn bug is fixed, we can remove this override and make UseNameOfInPlaceOfStringFixer language agnostic.
            string nameofString = SyntaxFacts.GetText(SyntaxKind.NameOfKeyword);
            SyntaxToken nameofIdentifierToken = SyntaxFactory.Identifier(leading: default, contextualKind: SyntaxKind.NameOfKeyword,
                text: nameofString, valueText: nameofString, trailing: default);
            var nameofIdentifierNode = SyntaxFactory.IdentifierName(nameofIdentifierToken);
            var nameofArgumentNode = SyntaxFactory.IdentifierName(identifierNameArgument);
            return generator.InvocationExpression(expression: nameofIdentifierNode, arguments: nameofArgumentNode);
        }
    }
}