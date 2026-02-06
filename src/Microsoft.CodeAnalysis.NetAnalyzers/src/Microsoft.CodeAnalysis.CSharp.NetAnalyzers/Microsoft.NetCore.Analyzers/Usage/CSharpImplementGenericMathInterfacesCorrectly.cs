// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Usage;

namespace Microsoft.NetCore.CSharp.Analyzers.Usage
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpImplementGenericMathInterfacesCorrectly : ImplementGenericMathInterfacesCorrectly
    {
        protected override SyntaxNode? FindTheTypeArgumentOfTheInterfaceFromTypeDeclaration(ISymbol typeSymbol, ISymbol theInterfaceSymbol)
        {
            foreach (SyntaxReference syntaxReference in typeSymbol.DeclaringSyntaxReferences)
            {
                SyntaxNode typeDefinition = syntaxReference.GetSyntax();
                if (typeDefinition is BaseTypeDeclarationSyntax baseType &&
                    baseType.BaseList is { } baseList &&
                    FindTypeArgumentFromBaseInterfaceList(baseList.Types, theInterfaceSymbol) is { } node)
                {
                    return node;
                }
            }

            return null;
        }

        private static SyntaxNode? FindTypeArgumentFromBaseInterfaceList(SeparatedSyntaxList<BaseTypeSyntax> baseListTypes, ISymbol anInterfaceSymbol)
        {
            foreach (BaseTypeSyntax baseType in baseListTypes)
            {
                if (baseType is SimpleBaseTypeSyntax simpleBaseType &&
                    simpleBaseType.Type is GenericNameSyntax genericName &&
                    genericName.Identifier.ValueText == anInterfaceSymbol.Name)
                {
                    return genericName.TypeArgumentList.Arguments[0];
                }
            }

            return null;
        }
    }
}
