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
        protected override SyntaxNode? GetPreviewTypeArgumentSyntaxNodeForMethod(IMethodSymbol methodSymbol, ISymbol parameterSymbol)
        {
            ImmutableArray<SyntaxReference> methodReferences = methodSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? methodReference in methodReferences)
            {
                SyntaxNode definition = methodReference.GetSyntax();
                if (definition is MethodDeclarationSyntax methodDeclaration)
                {
                    TypeParameterListSyntax? parameterList = methodDeclaration.TypeParameterList;
                    foreach (TypeParameterSyntax? parameter in parameterList.Parameters)
                    {
                        if (IsSyntaxToken(parameter.Identifier, parameterSymbol))
                        {
                            return parameter;
                        }
                    }
                }
            }

            return null;
        }

        protected override SyntaxNode? GetPreviewSyntaxNodeForFieldsOrEvents(ISymbol fieldOrEventSymbol, ISymbol previewSymbol)
        {
            ImmutableArray<SyntaxReference> fieldOrEventReferences = fieldOrEventSymbol.DeclaringSyntaxReferences;

            foreach (SyntaxReference? fieldOrEventReference in fieldOrEventReferences)
            {
                SyntaxNode definition = fieldOrEventReference.GetSyntax();

                while (definition is VariableDeclaratorSyntax)
                {
                    definition = definition.Parent;
                }

                if (definition is VariableDeclarationSyntax fieldDeclaration)
                {
                    TypeSyntax parameterType = fieldDeclaration.Type;
                    while (parameterType is ArrayTypeSyntax arrayType)
                    {
                        parameterType = arrayType.ElementType;
                    }

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
                        TypeSyntax parameterType = parameter.Type;
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
                    if (IsIdentifierNameSyntax(returnType, previewReturnTypeSymbol))
                    {
                        return returnType;
                    }
                }
                else if (methodOrPropertyDefinition is MethodDeclarationSyntax methodDeclaration)
                {
                    TypeSyntax returnType = methodDeclaration.ReturnType;
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

        private bool TryMatchGenericSyntaxNodeWithGivenSymbol(GenericNameSyntax genericName, ISymbol previewReturnTypeSymbol, [NotNullWhen(true)] out SyntaxNode? syntaxNode)
        {
            TypeArgumentListSyntax typeArgumentList = genericName.TypeArgumentList;
            foreach (TypeSyntax typeArgument in typeArgumentList.Arguments)
            {
                if (typeArgument is GenericNameSyntax innerGenericName)
                {
                    if (TryMatchGenericSyntaxNodeWithGivenSymbol(innerGenericName, previewReturnTypeSymbol, out syntaxNode))
                    {
                        return true;
                    }
                }
                if (IsIdentifierNameSyntax(typeArgument, previewReturnTypeSymbol))
                {
                    syntaxNode = typeArgument;
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
                if (typeOrMethodDefinition is ClassDeclarationSyntax classDeclaration)
                {
                    // For ex: class A<T> where T : IFoo, new() // where IFoo is preview
                    SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses = classDeclaration.ConstraintClauses;
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
                        if (typeConstraintSyntax.Type is IdentifierNameSyntax identifier)
                        {
                            if (IsSyntaxToken(identifier.Identifier, previewInterfaceConstraintSymbol))
                            {
                                syntaxNode = constraint;
                                return true;
                            }
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
                    if (type is IdentifierNameSyntax identifier && IsSyntaxToken(identifier.Identifier, previewInterfaceSymbol))
                    {
                        previewInterfaceNode = simpleBaseTypeSyntax;
                        return true;
                    }
                }
            }

            previewInterfaceNode = null;
            return false;
        }

        private static bool IsSyntaxToken(SyntaxToken identifier, ISymbol previewInterfaceSymbol)
        {
            if (identifier.ValueText == previewInterfaceSymbol.Name)
            {
                return true;
            }

            return false;
        }

        private static bool IsIdentifierNameSyntax(TypeSyntax identifier, ISymbol previewInterfaceSymbol)
        {
            if (identifier is IdentifierNameSyntax identifierName)
            {
                if (identifierName.Identifier.ValueText == previewInterfaceSymbol.Name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
