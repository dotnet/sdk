// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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