// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpDetectPreviewFeatureAnalyzer : DetectPreviewFeatureAnalyzer
    {
        protected override SyntaxNode? GetPreviewInterfaceNodeForTypeImplementingPreviewInterface(ISymbol typeSymbol, ISymbol previewInterfaceSymbol)
        {
            SyntaxNode? ret = null;
            ImmutableArray<SyntaxReference> typeSymbolDeclaringReferences = typeSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? syntaxReference in typeSymbolDeclaringReferences)
            {
                SyntaxNode typeSymbolDefinition = syntaxReference.GetSyntax();
                if (typeSymbolDefinition is ClassDeclarationSyntax classDeclaration)
                {
                    SeparatedSyntaxList<BaseTypeSyntax> baseListTypes = classDeclaration.BaseList.Types;
                    if (TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseListTypes, previewInterfaceSymbol, out ret))
                    {
                        return ret;
                    }
                }
                else if (typeSymbolDefinition is StructDeclarationSyntax structDeclaration)
                {
                    SeparatedSyntaxList<BaseTypeSyntax> baseListTypes = structDeclaration.BaseList.Types;
                    if (TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(baseListTypes, previewInterfaceSymbol, out ret))
                    {
                        return ret;
                    }
                }
            }

            return ret;
        }

        private static bool TryGetPreviewInterfaceNodeForClassOrStructImplementingPreviewInterface(SeparatedSyntaxList<BaseTypeSyntax> baseListTypes, ISymbol previewInterfaceSymbol, out SyntaxNode? previewInterfaceNode)
        {
            foreach (BaseTypeSyntax baseTypeSyntax in baseListTypes)
            {
                if (baseTypeSyntax is SimpleBaseTypeSyntax simpleBaseTypeSyntax)
                {
                    TypeSyntax type = simpleBaseTypeSyntax.Type;
                    if (type is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == previewInterfaceSymbol.Name)
                        {
                        previewInterfaceNode = simpleBaseTypeSyntax;
                        return true;
                    }
                }
            }

            previewInterfaceNode = null;
            return false;
        }
    }
}
