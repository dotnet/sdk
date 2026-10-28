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
                            .AddMissingSpecialConstraints(type.TypeParameters);

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
                // SyntaxGenerator.CustomEventDeclaration does not support generating explicit interface
                // implementations, so build the EventDeclarationSyntax directly in that case. Otherwise
                // GenAPI emits nothing for the explicit event and the generated reference source fails
                // to compile (CS0535).
                if (!eventSymbol.ExplicitInterfaceImplementations.IsEmpty)
                {
                    return CreateExplicitInterfaceEventDeclaration(syntaxGenerator, eventSymbol);
                }

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
                return syntaxGenerator.Declaration(symbol).AddMissingSpecialConstraints(symbol);
            }
            catch (ArgumentException ex)
            {
                // re-throw the ArgumentException with the symbol that caused it.
                throw new ArgumentException(ex.Message, symbol.ToDisplayString(), innerException: ex);
            }
        }

        // Builds an EventDeclarationSyntax for an explicit interface implementation event with empty
        // add/remove accessors. SyntaxGenerator.CustomEventDeclaration does not expose an overload
        // for specifying an explicit interface specifier.
        private static EventDeclarationSyntax CreateExplicitInterfaceEventDeclaration(
            SyntaxGenerator syntaxGenerator,
            IEventSymbol eventSymbol)
        {
            IEventSymbol implementedEvent = eventSymbol.ExplicitInterfaceImplementations[0];
            TypeSyntax eventType = (TypeSyntax)syntaxGenerator.TypeExpression(eventSymbol.Type);
            NameSyntax interfaceName = (NameSyntax)syntaxGenerator.TypeExpression(implementedEvent.ContainingType);

            AccessorListSyntax accessorList = SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
            {
                SyntaxFactory.AccessorDeclaration(SyntaxKind.AddAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block()),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.RemoveAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block())
            }));

            return SyntaxFactory.EventDeclaration(
                attributeLists: default,
                modifiers: default,
                eventKeyword: SyntaxFactory.Token(SyntaxKind.EventKeyword),
                type: eventType,
                explicitInterfaceSpecifier: SyntaxFactory.ExplicitInterfaceSpecifier(interfaceName),
                identifier: SyntaxFactory.Identifier(implementedEvent.Name),
                accessorList: accessorList);
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

        private static SyntaxNode AddMissingSpecialConstraints(this SyntaxNode declaration, ISymbol symbol) =>
            symbol switch
            {
                INamedTypeSymbol namedType => declaration.AddMissingSpecialConstraints(namedType.TypeParameters),
                IMethodSymbol method when !method.IsOverride && method.ExplicitInterfaceImplementations.IsEmpty =>
                    declaration.AddMissingSpecialConstraints(method.TypeParameters),
                _ => declaration
            };

        private static SyntaxNode AddMissingSpecialConstraints(this SyntaxNode declaration, IEnumerable<ITypeParameterSymbol> typeParameters)
        {
            ITypeParameterSymbol[] typeParameterArray = typeParameters.ToArray();

            if (!typeParameterArray.Any(typeParameter => typeParameter.HasNotNullConstraint || typeParameter.AllowsRefLikeType))
            {
                return declaration;
            }

            return declaration switch
            {
                TypeDeclarationSyntax typeDeclaration => typeDeclaration.WithConstraintClauses(
                    AddMissingSpecialConstraintClauses(typeDeclaration.ConstraintClauses, typeParameterArray)),
                DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.WithConstraintClauses(
                    AddMissingSpecialConstraintClauses(delegateDeclaration.ConstraintClauses, typeParameterArray)),
                MethodDeclarationSyntax methodDeclaration => methodDeclaration.WithConstraintClauses(
                    AddMissingSpecialConstraintClauses(methodDeclaration.ConstraintClauses, typeParameterArray)),
                _ => declaration
            };
        }

        private static SyntaxList<TypeParameterConstraintClauseSyntax> AddMissingSpecialConstraintClauses(
            SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
            ITypeParameterSymbol[] typeParameters)
        {
            for (int typeParameterIndex = 0; typeParameterIndex < typeParameters.Length; typeParameterIndex++)
            {
                ITypeParameterSymbol typeParameter = typeParameters[typeParameterIndex];

                if (!typeParameter.HasNotNullConstraint && !typeParameter.AllowsRefLikeType)
                {
                    continue;
                }

                TypeParameterConstraintClauseSyntax? constraintClause = constraintClauses
                    .FirstOrDefault(clause => clause.Name.Identifier.ValueText == typeParameter.Name);

                if (constraintClause is null)
                {
                    constraintClauses = constraintClauses.Insert(
                        GetConstraintClauseInsertIndex(constraintClauses, typeParameters, typeParameterIndex),
                        CreateConstraintClause(typeParameter));
                    continue;
                }

                SeparatedSyntaxList<TypeParameterConstraintSyntax> constraints = constraintClause.Constraints;

                if (typeParameter.HasNotNullConstraint && !constraints.Any(IsNotNullConstraint))
                {
                    constraints = constraints.Insert(0, CreateNotNullConstraint());
                }

                if (typeParameter.AllowsRefLikeType && !constraints.Any(IsAllowsRefStructConstraint))
                {
                    constraints = constraints.Add(CreateAllowsRefStructConstraint());
                }

                TypeParameterConstraintClauseSyntax updatedConstraintClause = constraintClause.WithConstraints(constraints);
                constraintClauses = constraintClauses.Replace(constraintClause, updatedConstraintClause);
            }

            return constraintClauses;
        }

        private static int GetConstraintClauseInsertIndex(
            SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
            ITypeParameterSymbol[] typeParameters,
            int typeParameterIndex)
        {
            for (int i = 0; i < constraintClauses.Count; i++)
            {
                int constraintTypeParameterIndex = Array.FindIndex(
                    typeParameters,
                    typeParameter => typeParameter.Name == constraintClauses[i].Name.Identifier.ValueText);

                if (constraintTypeParameterIndex > typeParameterIndex)
                {
                    return i;
                }
            }

            return constraintClauses.Count;
        }

        private static TypeParameterConstraintClauseSyntax CreateConstraintClause(ITypeParameterSymbol typeParameter)
        {
            SeparatedSyntaxList<TypeParameterConstraintSyntax> constraints = default;

            if (typeParameter.HasNotNullConstraint)
            {
                constraints = constraints.Add(CreateNotNullConstraint());
            }

            if (typeParameter.AllowsRefLikeType)
            {
                constraints = constraints.Add(CreateAllowsRefStructConstraint());
            }

            return SyntaxFactory.TypeParameterConstraintClause(SyntaxFactory.IdentifierName(typeParameter.Name))
                .WithConstraints(constraints);
        }

        private static TypeParameterConstraintSyntax CreateNotNullConstraint() =>
            SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName("notnull"));

        private static TypeParameterConstraintSyntax CreateAllowsRefStructConstraint() =>
            SyntaxFactory.AllowsConstraintClause(
                SyntaxFactory.SingletonSeparatedList<AllowsConstraintSyntax>(
                    SyntaxFactory.RefStructConstraint()));

        private static bool IsNotNullConstraint(TypeParameterConstraintSyntax constraint) =>
            constraint is TypeConstraintSyntax typeConstraint &&
            typeConstraint.Type is IdentifierNameSyntax identifierName &&
            identifierName.Identifier.ValueText == "notnull";

        private static bool IsAllowsRefStructConstraint(TypeParameterConstraintSyntax constraint) =>
            constraint is AllowsConstraintClauseSyntax allowsConstraintClause &&
            allowsConstraintClause.Constraints.Any(static constraint => constraint is RefStructConstraintSyntax);
    }
}
