// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxNodeExtensions
    {
        public static SyntaxNode Rewrite(this SyntaxNode node, CSharpSyntaxRewriter rewriter) => rewriter.Visit(node);

        public static SyntaxNode AddMemberAttributes(this SyntaxNode node,
            SyntaxGenerator syntaxGenerator,
            ISymbol member,
            ISymbolFilter attributeDataSymbolFilter)
        {
            // Add attributes to the member itself
            foreach (AttributeData attribute in member.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(attributeDataSymbolFilter))
            {
                // The C# compiler emits the DefaultMemberAttribute on any type containing an indexer.
                // In C# it is an error to manually attribute a type with the DefaultMemberAttribute if the type also declares an indexer.
                if (member is INamedTypeSymbol typeMember && typeMember.HasIndexer() && attribute.IsDefaultMemberAttribute())
                {
                    continue;
                }

                if (attribute.IsReserved())
                {
                    continue;
                }

                node = syntaxGenerator.AddAttributes(node, syntaxGenerator.Attribute(attribute));
            }

            // Add attributes to parameters for methods, constructors, indexers, and delegates that have parameters
            node = AddParameterAttributes(node, syntaxGenerator, member, attributeDataSymbolFilter);

            // Add attributes to type parameters for types and methods that have type parameters
            node = AddTypeParameterAttributes(node, syntaxGenerator, member, attributeDataSymbolFilter);

            return node;
        }

        private static SyntaxNode AddParameterAttributes(SyntaxNode node,
            SyntaxGenerator syntaxGenerator,
            ISymbol member,
            ISymbolFilter attributeDataSymbolFilter)
        {
            // Get the parameters from the symbol based on the member type
            ImmutableArray<IParameterSymbol> parameters = GetParametersFromSymbol(member);
            if (parameters.IsEmpty)
            {
                return node;
            }

            // Get the parameter syntax nodes from the declaration
            SeparatedSyntaxList<ParameterSyntax>? parameterSyntaxList = GetParameterSyntaxList(node);
            if (parameterSyntaxList == null || parameterSyntaxList.Value.Count != parameters.Length)
            {
                // If we can't match parameters between symbol and syntax, skip adding parameter attributes
                return node;
            }

            // Apply attributes to each parameter
            SeparatedSyntaxList<ParameterSyntax> updatedParameters = parameterSyntaxList.Value;
            for (int i = 0; i < parameters.Length; i++)
            {
                IParameterSymbol parameterSymbol = parameters[i];
                ParameterSyntax parameterSyntax = updatedParameters[i];

                // Add attributes to this parameter
                foreach (AttributeData attribute in parameterSymbol.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(attributeDataSymbolFilter))
                {
                    if (attribute.IsReserved())
                    {
                        continue;
                    }

                    parameterSyntax = (ParameterSyntax)syntaxGenerator.AddAttributes(parameterSyntax, syntaxGenerator.Attribute(attribute));
                }

                updatedParameters = updatedParameters.Replace(updatedParameters[i], parameterSyntax);
            }

            // Update the node with the modified parameters
            return UpdateNodeWithParameters(node, updatedParameters);
        }

        private static ImmutableArray<IParameterSymbol> GetParametersFromSymbol(ISymbol member)
        {
            return member switch
            {
                IMethodSymbol method => method.Parameters,
                IPropertySymbol property when property.IsIndexer => property.Parameters,
                INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType => GetDelegateParameters(delegateType),
                INamedTypeSymbol { IsRecord: true } record => GetRecordPrimaryConstructorParameters(record),
                _ => ImmutableArray<IParameterSymbol>.Empty
            };
        }

        private static ImmutableArray<IParameterSymbol> GetDelegateParameters(INamedTypeSymbol delegateType)
        {
            // For delegates, get the parameters from the Invoke method
            var invokeMethod = delegateType.GetMembers("Invoke").OfType<IMethodSymbol>().FirstOrDefault();
            return invokeMethod?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
        }

        private static ImmutableArray<IParameterSymbol> GetRecordPrimaryConstructorParameters(INamedTypeSymbol recordType)
        {
            // For records, find the primary constructor parameters
            var primaryConstructor = recordType.Constructors.FirstOrDefault(c => c.IsImplicitlyDeclared == false);
            return primaryConstructor?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;
        }

        private static SeparatedSyntaxList<ParameterSyntax>? GetParameterSyntaxList(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax method => method.ParameterList.Parameters,
                ConstructorDeclarationSyntax constructor => constructor.ParameterList.Parameters,
                IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
                DelegateDeclarationSyntax delegateDecl => delegateDecl.ParameterList.Parameters,
                OperatorDeclarationSyntax operatorDecl => operatorDecl.ParameterList.Parameters,
                ConversionOperatorDeclarationSyntax conversionOp => conversionOp.ParameterList.Parameters,
                RecordDeclarationSyntax record => record.ParameterList?.Parameters,
                _ => null
            };
        }

        private static SyntaxNode UpdateNodeWithParameters(SyntaxNode node, SeparatedSyntaxList<ParameterSyntax> updatedParameters)
        {
            return node switch
            {
                MethodDeclarationSyntax method => method.WithParameterList(method.ParameterList.WithParameters(updatedParameters)),
                ConstructorDeclarationSyntax constructor => constructor.WithParameterList(constructor.ParameterList.WithParameters(updatedParameters)),
                IndexerDeclarationSyntax indexer => indexer.WithParameterList(indexer.ParameterList.WithParameters(updatedParameters)),
                DelegateDeclarationSyntax delegateDecl => delegateDecl.WithParameterList(delegateDecl.ParameterList.WithParameters(updatedParameters)),
                OperatorDeclarationSyntax operatorDecl => operatorDecl.WithParameterList(operatorDecl.ParameterList.WithParameters(updatedParameters)),
                ConversionOperatorDeclarationSyntax conversionOp => conversionOp.WithParameterList(conversionOp.ParameterList.WithParameters(updatedParameters)),
                RecordDeclarationSyntax record when record.ParameterList != null => record.WithParameterList(record.ParameterList.WithParameters(updatedParameters)),
                _ => node
            };
        }

        private static SyntaxNode AddTypeParameterAttributes(SyntaxNode node,
            SyntaxGenerator syntaxGenerator,
            ISymbol member,
            ISymbolFilter attributeDataSymbolFilter)
        {
            // Get the type parameters from the symbol based on the member type
            ImmutableArray<ITypeParameterSymbol> typeParameters = GetTypeParametersFromSymbol(member);
            if (typeParameters.IsEmpty)
            {
                return node;
            }

            // Get the type parameter syntax nodes from the declaration
            SeparatedSyntaxList<TypeParameterSyntax>? typeParameterSyntaxList = GetTypeParameterSyntaxList(node);
            if (typeParameterSyntaxList == null || typeParameterSyntaxList.Value.Count != typeParameters.Length)
            {
                // If we can't match type parameters between symbol and syntax, skip adding type parameter attributes
                return node;
            }

            // Apply attributes to each type parameter
            SeparatedSyntaxList<TypeParameterSyntax> updatedTypeParameters = typeParameterSyntaxList.Value;
            for (int i = 0; i < typeParameters.Length; i++)
            {
                ITypeParameterSymbol typeParameterSymbol = typeParameters[i];
                TypeParameterSyntax typeParameterSyntax = updatedTypeParameters[i];

                // Add attributes to this type parameter
                foreach (AttributeData attribute in typeParameterSymbol.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(attributeDataSymbolFilter))
                {
                    if (attribute.IsReserved())
                    {
                        continue;
                    }

                    typeParameterSyntax = (TypeParameterSyntax)syntaxGenerator.AddAttributes(typeParameterSyntax, syntaxGenerator.Attribute(attribute));
                }

                updatedTypeParameters = updatedTypeParameters.Replace(updatedTypeParameters[i], typeParameterSyntax);
            }

            // Update the node with the modified type parameters
            return UpdateNodeWithTypeParameters(node, updatedTypeParameters);
        }

        private static ImmutableArray<ITypeParameterSymbol> GetTypeParametersFromSymbol(ISymbol member)
        {
            return member switch
            {
                INamedTypeSymbol namedType => namedType.TypeParameters,
                IMethodSymbol method => method.TypeParameters,
                _ => ImmutableArray<ITypeParameterSymbol>.Empty
            };
        }

        private static SeparatedSyntaxList<TypeParameterSyntax>? GetTypeParameterSyntaxList(SyntaxNode node)
        {
            return node switch
            {
                ClassDeclarationSyntax classDecl => classDecl.TypeParameterList?.Parameters,
                StructDeclarationSyntax structDecl => structDecl.TypeParameterList?.Parameters,
                InterfaceDeclarationSyntax interfaceDecl => interfaceDecl.TypeParameterList?.Parameters,
                RecordDeclarationSyntax record => record.TypeParameterList?.Parameters,
                DelegateDeclarationSyntax delegateDecl => delegateDecl.TypeParameterList?.Parameters,
                MethodDeclarationSyntax method => method.TypeParameterList?.Parameters,
                _ => null
            };
        }

        private static SyntaxNode UpdateNodeWithTypeParameters(SyntaxNode node, SeparatedSyntaxList<TypeParameterSyntax> updatedTypeParameters)
        {
            return node switch
            {
                ClassDeclarationSyntax classDecl when classDecl.TypeParameterList != null => 
                    classDecl.WithTypeParameterList(classDecl.TypeParameterList.WithParameters(updatedTypeParameters)),
                StructDeclarationSyntax structDecl when structDecl.TypeParameterList != null => 
                    structDecl.WithTypeParameterList(structDecl.TypeParameterList.WithParameters(updatedTypeParameters)),
                InterfaceDeclarationSyntax interfaceDecl when interfaceDecl.TypeParameterList != null => 
                    interfaceDecl.WithTypeParameterList(interfaceDecl.TypeParameterList.WithParameters(updatedTypeParameters)),
                RecordDeclarationSyntax record when record.TypeParameterList != null => 
                    record.WithTypeParameterList(record.TypeParameterList.WithParameters(updatedTypeParameters)),
                DelegateDeclarationSyntax delegateDecl when delegateDecl.TypeParameterList != null => 
                    delegateDecl.WithTypeParameterList(delegateDecl.TypeParameterList.WithParameters(updatedTypeParameters)),
                MethodDeclarationSyntax method when method.TypeParameterList != null => 
                    method.WithTypeParameterList(method.TypeParameterList.WithParameters(updatedTypeParameters)),
                _ => node
            };
        }
    }
}
