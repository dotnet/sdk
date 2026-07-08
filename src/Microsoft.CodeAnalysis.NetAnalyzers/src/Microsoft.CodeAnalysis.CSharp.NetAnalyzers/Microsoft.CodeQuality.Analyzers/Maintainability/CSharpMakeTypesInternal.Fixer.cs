// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpMakeTypesInternalFixer : MakeTypesInternalFixer
    {
        protected override SyntaxNode MakeInternal(SyntaxNode node) =>
            node switch
            {
                TypeDeclarationSyntax type => MakeMemberInternal(type),
                EnumDeclarationSyntax @enum => MakeMemberInternal(@enum),
                DelegateDeclarationSyntax @delegate => MakeMemberInternal(@delegate),
                _ => throw new NotSupportedException()
            };

        private static SyntaxNode MakeMemberInternal(MemberDeclarationSyntax type)
        {
            var publicKeyword = type.Modifiers.First(m => m.IsKind(SyntaxKind.PublicKeyword));
            var modifiers = type.Modifiers.Replace(publicKeyword, SyntaxFactory.Token(SyntaxKind.InternalKeyword));

            return type.WithModifiers(modifiers);
        }
    }
}