// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxGeneratorExtensions
    {
        // Creates a declaration matching an existing symbol.
        // The reason of having this similar to `SyntaxGenerator.Declaration` extension method is that
        // SyntaxGenerator does not generates attributes neither for types, neither for members.
        public static SyntaxNode DeclarationExt(this SyntaxGenerator syntaxGenerator, ISymbol symbol, ISymbolFilter symbolFilter)
        {
            if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol type = (INamedTypeSymbol)symbol;
                switch (type.TypeKind)
                {
                    case TypeKind.Class:
                    case TypeKind.Struct:
                    case TypeKind.Interface:
                        TypeDeclarationSyntax typeDeclaration = (TypeDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        if (type.IsRecord && type.TryGetRecordConstructor(out IMethodSymbol? recordConstructor))
                        {
                            // if the type is a record and we can find it's parameters, use `record Name(parameters...)` syntax.
                            typeDeclaration = typeDeclaration.WithParameterList(
                                SyntaxFactory.ParameterList(
                                    SyntaxFactory.SeparatedList<ParameterSyntax>(
                                        recordConstructor.Parameters.Select(p => (ParameterSyntax)syntaxGenerator.ParameterDeclaration(p)))));
                        }
                        return typeDeclaration
                            .WithBaseList(syntaxGenerator.GetBaseTypeList(type, symbolFilter))
                            .WithMembers(new SyntaxList<MemberDeclarationSyntax>())
                            .AddNotNullConstraints(type.TypeParameters);

                    case TypeKind.Enum:
                        EnumDeclarationSyntax enumDeclaration = (EnumDeclarationSyntax)syntaxGenerator.Declaration(symbol);
                        return enumDeclaration.WithMembers(new SeparatedSyntaxList<EnumMemberDeclarationSyntax>());
                }
            }

            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol method = (IMethodSymbol)symbol;
                if (method.MethodKind == MethodKind.Constructor)
                {
                    INamedTypeSymbol? baseType = method.ContainingType.BaseType;
                    if (baseType != null)
                    {
                        IEnumerable<IMethodSymbol> baseConstructors = baseType.Constructors.Where(symbolFilter.Include);
                        // If the base type does not have default constructor.
                        if (baseConstructors.Any() && baseConstructors.All(c => !c.Parameters.IsEmpty))
                        {
                            IOrderedEnumerable<IMethodSymbol> baseTypeConstructors = baseConstructors
                                .Where(c => c.GetAttributes().All(a => !a.IsObsoleteWithUsageTreatedAsCompilationError()))
                                .OrderBy(c => c.Parameters.Length);

                            if (baseTypeConstructors.Any())
                            {
                                IMethodSymbol constructor = baseTypeConstructors.First();

                                ConstructorDeclarationSyntax declaration = (ConstructorDeclarationSyntax)syntaxGenerator.Declaration(method);
                                if (!declaration.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.UnsafeKeyword) &&
                                    // if at least one parameter of a base constructor is raw pointer type
                                    constructor.Parameters.Any(p => p.Type.TypeKind == TypeKind.Pointer))
                                {
                                    declaration = declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword));
                                }
                                return declaration.WithInitializer(constructor.GenerateBaseConstructorInitializer());
                            }
                        }
                    }
                }
            }

            if (symbol is IEventSymbol eventSymbol && !eventSymbol.IsAbstract)
            {
                // adds generation of add & remove accessors for the non abstract events.
                return syntaxGenerator.CustomEventDeclaration(eventSymbol.Name,
                    syntaxGenerator.TypeExpression(eventSymbol.Type),
                    eventSymbol.DeclaredAccessibility,
                    DeclarationModifiers.From(eventSymbol));
            }

            if (symbol is IPropertySymbol propertySymbol)
            {
                // Explicitly implemented indexers do not set IsIndexer
                // https://github.com/dotnet/roslyn/issues/53911
                if (!propertySymbol.IsIndexer && propertySymbol.ExplicitInterfaceImplementations.Any(i => i.IsIndexer))
                {
                    return syntaxGenerator.IndexerDeclaration(propertySymbol);
                }
            }

            try
            {
                return syntaxGenerator.Declaration(symbol).AddNotNullConstraints(symbol);
            }
            catch (ArgumentException ex)
            {
                // re-throw the ArgumentException with the symbol that caused it.
                throw new ArgumentException(ex.Message, symbol.ToDisplayString(), innerException: ex);
            }
        }

        // Gets the list of base class and interfaces for a given symbol INamedTypeSymbol.
        private static BaseListSyntax? GetBaseTypeList(this SyntaxGenerator syntaxGenerator,
            INamedTypeSymbol type,
            ISymbolFilter symbolFilter)
        {
            List<BaseTypeSyntax> baseTypes = [];

            if (type.TypeKind == TypeKind.Class && type.BaseType != null && symbolFilter.Include(type.BaseType))
            {
                TypeSyntax baseTypeSyntax = (TypeSyntax)syntaxGenerator.TypeExpression(type.BaseType);

                if (type.BaseType.IsRecord && type.BaseType.TryGetRecordConstructor(out IMethodSymbol? recordConstructor))
                {
                    baseTypes.Add(SyntaxFactory.PrimaryConstructorBaseType(baseTypeSyntax, recordConstructor.CreateDefaultArgumentList()));
                }
                else
                {
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(baseTypeSyntax));
                }
            }

            // includes only interfaces that were not filtered out by the given ISymbolFilter or none of TypeParameters were filtered out.
            baseTypes.AddRange(type.Interfaces
                .Where(i => symbolFilter.Include(i) && !i.HasInaccessibleTypeArgument(symbolFilter))
                .Select(i => SyntaxFactory.SimpleBaseType((TypeSyntax)syntaxGenerator.TypeExpression(i))));

            return baseTypes.Count > 0 ?
                SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes)) :
                null;
        }

        private static SyntaxNode AddNotNullConstraints(this SyntaxNode declaration, ISymbol symbol) =>
            symbol switch
            {
                INamedTypeSymbol namedType => declaration.AddNotNullConstraints(namedType.TypeParameters),
                IMethodSymbol method when !method.IsOverride && method.ExplicitInterfaceImplementations.IsEmpty =>
                    declaration.AddNotNullConstraints(method.TypeParameters),
                _ => declaration
            };

        private static SyntaxNode AddNotNullConstraints(this SyntaxNode declaration, IEnumerable<ITypeParameterSymbol> typeParameters)
        {
            ITypeParameterSymbol[] notNullTypeParameters = typeParameters
                .Where(typeParameter => typeParameter.HasNotNullConstraint)
                .ToArray();

            if (notNullTypeParameters.Length == 0)
            {
                return declaration;
            }

            return declaration switch
            {
                TypeDeclarationSyntax typeDeclaration => typeDeclaration.WithConstraintClauses(
                    AddNotNullConstraintClauses(typeDeclaration.ConstraintClauses, notNullTypeParameters)),
                DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.WithConstraintClauses(
                    AddNotNullConstraintClauses(delegateDeclaration.ConstraintClauses, notNullTypeParameters)),
                MethodDeclarationSyntax methodDeclaration => methodDeclaration.WithConstraintClauses(
                    AddNotNullConstraintClauses(methodDeclaration.ConstraintClauses, notNullTypeParameters)),
                _ => declaration
            };
        }

        private static SyntaxList<TypeParameterConstraintClauseSyntax> AddNotNullConstraintClauses(
            SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
            IEnumerable<ITypeParameterSymbol> typeParameters)
        {
            foreach (ITypeParameterSymbol typeParameter in typeParameters)
            {
                TypeParameterConstraintClauseSyntax? constraintClause = constraintClauses
                    .FirstOrDefault(clause => clause.Name.Identifier.ValueText == typeParameter.Name);

                if (constraintClause is null)
                {
                    constraintClauses = constraintClauses.Add(CreateNotNullConstraintClause(typeParameter.Name));
                }
                else if (!constraintClause.Constraints.Any(IsNotNullConstraint))
                {
                    TypeParameterConstraintClauseSyntax updatedConstraintClause = constraintClause.WithConstraints(
                        constraintClause.Constraints.Insert(0, CreateNotNullConstraint()));
                    constraintClauses = constraintClauses.Replace(constraintClause, updatedConstraintClause);
                }
            }

            return constraintClauses;
        }

        private static TypeParameterConstraintClauseSyntax CreateNotNullConstraintClause(string typeParameterName) =>
            SyntaxFactory.TypeParameterConstraintClause(SyntaxFactory.IdentifierName(typeParameterName))
                .WithConstraints(SyntaxFactory.SingletonSeparatedList(CreateNotNullConstraint()));

        private static TypeParameterConstraintSyntax CreateNotNullConstraint() =>
            SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("notnull"));

        private static bool IsNotNullConstraint(TypeParameterConstraintSyntax constraint) =>
            constraint is TypeConstraintSyntax typeConstraint &&
            typeConstraint.Type is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.ValueText == "notnull";
    }
}
