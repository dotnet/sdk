// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Runtime;

namespace Microsoft.NetCore.CSharp.Analyzers.Runtime
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CSharpDetectPreviewFeatureAnalyzer : DetectPreviewFeatureAnalyzer
    {
        protected override SyntaxNode? GetPreviewSyntaxNodeForFieldsOrEvents(ISymbol fieldOrEventSymbol, ISymbol previewSymbol)
        {
            ImmutableArray<SyntaxReference> fieldOrEventReferences = fieldOrEventSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? fieldOrEventReference in fieldOrEventReferences)
            {
                SyntaxNode? definition = fieldOrEventReference.GetSyntax();

                while (definition is VariableDeclaratorSyntax)
                {
                    definition = definition.Parent;
                }

                if (definition is VariableDeclarationSyntax fieldDeclaration)
                {
                    TypeSyntax parameterType = fieldDeclaration.Type;
                    parameterType = GetElementTypeForNullableAndArrayTypeNodes(parameterType);

                    if (IsIdentifierNameSyntax(parameterType, previewSymbol))
                    {
                        return parameterType;
                    }
                    else if (parameterType is GenericNameSyntax genericName)
                    {
                        if (TryMatchGenericSyntaxNodeWithGivenSymbol(genericName, previewSymbol, out SyntaxNode? previewNode))
                        {
                            return previewNode;
                        }
                    }
                }
            }

            return null;
        }

        private static TypeSyntax GetElementTypeForNullableAndArrayTypeNodes(TypeSyntax parameterType)
        {
            while (parameterType is NullableTypeSyntax nullable)
            {
                parameterType = nullable.ElementType;
            }

            while (parameterType is ArrayTypeSyntax arrayType)
            {
                parameterType = arrayType.ElementType;
            }

            return parameterType;
        }

        protected override SyntaxNode? GetPreviewParameterSyntaxNodeForMethod(IMethodSymbol methodSymbol, ISymbol parameterSymbol)
        {
            ImmutableArray<SyntaxReference> methodSymbolDeclaringReferences = methodSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? syntaxReference in methodSymbolDeclaringReferences)
            {
                SyntaxNode methodDefinition = syntaxReference.GetSyntax();
                if (methodDefinition is MethodDeclarationSyntax methodDeclaration)
                {
                    ParameterListSyntax? parameters = methodDeclaration.ParameterList;
                    foreach (ParameterSyntax? parameter in parameters.Parameters)
                    {
                        TypeSyntax parameterType = parameter.Type!;
                        parameterType = GetElementTypeForNullableAndArrayTypeNodes(parameterType);

                        if (IsIdentifierNameSyntax(parameterType, parameterSymbol))
                        {
                            return parameterType;
                        }
                        else if (parameterType is GenericNameSyntax genericName)
                        {
                            if (TryMatchGenericSyntaxNodeWithGivenSymbol(genericName, parameterSymbol, out SyntaxNode? previewNode))
                            {
                                return previewNode;
                            }
                        }
                    }
                }
            }

            return null;
        }

        // Handles both generic and non-generic return types
        protected override SyntaxNode? GetPreviewReturnTypeSyntaxNodeForMethodOrProperty(ISymbol methodOrPropertySymbol, ISymbol previewReturnTypeSymbol)
        {
            ImmutableArray<SyntaxReference> methodOrPropertySymbolDeclaringReferences = methodOrPropertySymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? syntaxReference in methodOrPropertySymbolDeclaringReferences)
            {
                SyntaxNode methodOrPropertyDefinition = syntaxReference.GetSyntax();
                if (methodOrPropertyDefinition is PropertyDeclarationSyntax propertyDeclaration)
                {
                    TypeSyntax returnType = propertyDeclaration.Type;
                    returnType = GetElementTypeForNullableAndArrayTypeNodes(returnType);
                    if (IsIdentifierNameSyntax(returnType, previewReturnTypeSymbol))
                    {
                        return returnType;
                    }
                    else if (returnType is GenericNameSyntax genericName)
                    {
                        if (TryMatchGenericSyntaxNodeWithGivenSymbol(genericName, previewReturnTypeSymbol, out SyntaxNode? previewNode))
                        {
                            return previewNode;
                        }
                    }
                }
                else if (methodOrPropertyDefinition is MethodDeclarationSyntax methodDeclaration)
                {
                    TypeSyntax returnType = methodDeclaration.ReturnType;
                    returnType = GetElementTypeForNullableAndArrayTypeNodes(returnType);
                    if (IsIdentifierNameSyntax(returnType, previewReturnTypeSymbol))
                    {
                        return returnType;
                    }
                    else if (returnType is GenericNameSyntax genericName)
                    {
                        if (TryMatchGenericSyntaxNodeWithGivenSymbol(genericName, previewReturnTypeSymbol, out SyntaxNode? previewNode))
                        {
                            return previewNode;
                        }
                    }
                }
            }

            return null;
        }

        private static bool TryMatchGenericSyntaxNodeWithGivenSymbol(GenericNameSyntax genericName, ISymbol previewReturnTypeSymbol, [NotNullWhen(true)] out SyntaxNode? syntaxNode)
        {
            if (IsSyntaxToken(genericName.Identifier, previewReturnTypeSymbol))
            {
                syntaxNode = genericName;
                return true;
            }

            TypeArgumentListSyntax typeArgumentList = genericName.TypeArgumentList;
            foreach (TypeSyntax typeArgument in typeArgumentList.Arguments)
            {
                TypeSyntax typeArgumentElementType = GetElementTypeForNullableAndArrayTypeNodes(typeArgument);
                if (typeArgumentElementType is GenericNameSyntax innerGenericName)
                {
                    if (TryMatchGenericSyntaxNodeWithGivenSymbol(innerGenericName, previewReturnTypeSymbol, out syntaxNode))
                    {
                        return true;
                    }
                }

                if (IsIdentifierNameSyntax(typeArgumentElementType, previewReturnTypeSymbol))
                {
                    syntaxNode = typeArgumentElementType;
                    return true;
                }
            }

            syntaxNode = null;
            return false;
        }

        protected override SyntaxNode? GetConstraintSyntaxNodeForTypeConstrainedByPreviewTypes(ISymbol typeOrMethodSymbol, ISymbol previewInterfaceConstraintSymbol)
        {
            ImmutableArray<SyntaxReference> typeSymbolDeclaringReferences = typeOrMethodSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? syntaxReference in typeSymbolDeclaringReferences)
            {
                SyntaxNode typeOrMethodDefinition = syntaxReference.GetSyntax();
                if (typeOrMethodDefinition is TypeDeclarationSyntax typeDeclaration)
                {
                    // For ex: class A<T> where T : IFoo, new() // where IFoo is preview
                    SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = typeDeclaration.ConstraintClauses;
                    if (TryGetConstraintClauseNode(constraintClauses, previewInterfaceConstraintSymbol, out SyntaxNode? ret))
                    {
                        return ret;
                    }
                }
                else if (typeOrMethodDefinition is MethodDeclarationSyntax methodDeclaration)
                {
                    SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = methodDeclaration.ConstraintClauses;
                    if (TryGetConstraintClauseNode(constraintClauses, previewInterfaceConstraintSymbol, out SyntaxNode? ret))
                    {
                        return ret;
                    }
                }
            }

            return null;
        }

        private static bool TryGetConstraintClauseNode(SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses, ISymbol previewInterfaceConstraintSymbol, [NotNullWhen(true)] out SyntaxNode? syntaxNode)
        {
            foreach (TypeParameterConstraintClauseSyntax constraintClause in constraintClauses)
            {
                SeparatedSyntaxList<TypeParameterConstraintSyntax> constraints = constraintClause.Constraints;
                foreach (TypeParameterConstraintSyntax? constraint in constraints)
                {
                    if (constraint is TypeConstraintSyntax typeConstraintSyntax)
                    {
                        TypeSyntax typeConstraintSyntaxType = typeConstraintSyntax.Type;
                        typeConstraintSyntaxType = GetElementTypeForNullableAndArrayTypeNodes(typeConstraintSyntaxType);
                        if (typeConstraintSyntaxType is GenericNameSyntax generic)
                        {
                            if (TryMatchGenericSyntaxNodeWithGivenSymbol(generic, previewInterfaceConstraintSymbol, out SyntaxNode? previewConstraint))
                            {
                                syntaxNode = previewConstraint;
                                return true;
                            }
                        }

                        if (IsIdentifierNameSyntax(typeConstraintSyntaxType, previewInterfaceConstraintSymbol))
                        {
                            syntaxNode = constraint;
                            return true;
                        }
                    }
                }
            }

            syntaxNode = null;
            return false;
        }

        protected override SyntaxNode? GetPreviewInterfaceNodeForTypeImplementingPreviewInterface(ISymbol typeSymbol, ISymbol previewInterfaceSymbol)
        {
            SyntaxNode? ret = null;
            ImmutableArray<SyntaxReference> typeSymbolDeclaringReferences = typeSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? syntaxReference in typeSymbolDeclaringReferences)
            {
                SyntaxNode typeSymbolDefinition = syntaxReference.GetSyntax();
                if (typeSymbolDefinition is TypeDeclarationSyntax { BaseList.Types: var baseListTypes })
                {
                    if (TryGetPreviewInterfaceNodeForTypeImplementingPreviewInterface(baseListTypes, previewInterfaceSymbol, out ret))
                    {
                        return ret;
                    }
                }
            }

            return ret;
        }

        private static bool TryGetPreviewInterfaceNodeForTypeImplementingPreviewInterface(SeparatedSyntaxList<BaseTypeSyntax> baseListTypes, ISymbol previewInterfaceSymbol, out SyntaxNode? previewInterfaceNode)
        {
            foreach (BaseTypeSyntax baseTypeSyntax in baseListTypes)
            {
                if (baseTypeSyntax is BaseTypeSyntax simpleBaseTypeSyntax)
                {
                    TypeSyntax type = simpleBaseTypeSyntax.Type;
                    if (type is IdentifierNameSyntax identifier && IsSyntaxToken(identifier.Identifier, previewInterfaceSymbol))
                    {
                        previewInterfaceNode = simpleBaseTypeSyntax;
                        return true;
                    }

                    if (type is GenericNameSyntax generic)
                    {
                        if (TryMatchGenericSyntaxNodeWithGivenSymbol(generic, previewInterfaceSymbol, out SyntaxNode? previewConstraint))
                        {
                            previewInterfaceNode = previewConstraint;
                            return true;
                        }
                    }
                }
            }

            previewInterfaceNode = null;
            return false;
        }

        private static bool IsSyntaxToken(SyntaxToken identifier, ISymbol previewInterfaceSymbol) => identifier.ValueText == previewInterfaceSymbol.Name;

        private static bool IsIdentifierNameSyntax(TypeSyntax identifier, ISymbol previewInterfaceSymbol) => identifier is IdentifierNameSyntax identifierName && IsSyntaxToken(identifierName.Identifier, previewInterfaceSymbol) ||
          identifier is NullableTypeSyntax nullable && IsIdentifierNameSyntax(nullable.ElementType, previewInterfaceSymbol);

        protected override SyntaxNode? GetPreviewImplementsClauseSyntaxNodeForMethodOrProperty(ISymbol methodOrPropertySymbol, ISymbol previewSymbol)
        {
            throw new System.NotImplementedException();
        }
    }
}
