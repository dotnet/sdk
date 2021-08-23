// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeQuality.CSharp.Analyzers.ApiDesignGuidelines
{
    /// <summary> 
    /// CA1028: Enum Storage should be Int32
    /// </summary> 
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpEnumStorageShouldBeInt32Fixer : EnumStorageShouldBeInt32Fixer
    {
        protected override SyntaxNode? GetTargetNode(SyntaxNode node)
        {
            var enumDecl = (EnumDeclarationSyntax)node;
            return (enumDecl.BaseList.Types.FirstOrDefault() as SimpleBaseTypeSyntax)?.Type;
        }
    }
}
