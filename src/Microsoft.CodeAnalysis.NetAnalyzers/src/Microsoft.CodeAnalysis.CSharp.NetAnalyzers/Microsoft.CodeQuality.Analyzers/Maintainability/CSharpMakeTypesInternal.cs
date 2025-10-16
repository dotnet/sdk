// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.Maintainability;

namespace Microsoft.CodeQuality.CSharp.Analyzers.Maintainability
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpMakeTypesInternal : MakeTypesInternal
    {
        protected override SyntaxToken? GetIdentifier(SyntaxNode type) => type switch
        {
            TypeDeclarationSyntax tds => tds.Identifier,
            EnumDeclarationSyntax eds => eds.Identifier,
            DelegateDeclarationSyntax dds => dds.Identifier,
            _ => null
        };
    }
}
