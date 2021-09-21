// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines;

namespace Microsoft.CodeQuality.CSharp.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1802: Use literals where appropriate
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpUseLiteralsWhereAppropriateFixer : UseLiteralsWhereAppropriateFixer
    {
        protected override SyntaxNode? GetFieldDeclaration(SyntaxNode syntaxNode)
        {
            while (syntaxNode is not null and not FieldDeclarationSyntax)
            {
                syntaxNode = syntaxNode.Parent;
            }

            var field = (FieldDeclarationSyntax?)syntaxNode;

            // Multiple declarators are not supported, as one of them may not be constant.
            return field?.Declaration.Variables.Count > 1 ? null : field;
        }

        protected override bool IsStaticKeyword(SyntaxToken syntaxToken)
        {
            return syntaxToken.IsKind(SyntaxKind.StaticKeyword);
        }

        protected override bool IsReadonlyKeyword(SyntaxToken syntaxToken)
        {
            return syntaxToken.IsKind(SyntaxKind.ReadOnlyKeyword);
        }

        protected override SyntaxToken GetConstKeywordToken()
        {
            return SyntaxFactory.Token(SyntaxKind.ConstKeyword);
        }

        protected override SyntaxTokenList GetModifiers(SyntaxNode fieldSyntax)
        {
            var field = (FieldDeclarationSyntax)fieldSyntax;
            return field.Modifiers;
        }

        protected override SyntaxNode WithModifiers(SyntaxNode fieldSyntax, SyntaxTokenList modifiers)
        {
            var field = (FieldDeclarationSyntax)fieldSyntax;
            return field.WithModifiers(modifiers);
        }
    }
}