// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.NetCore.Analyzers.InteropServices;

namespace Microsoft.NetCore.CSharp.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class CSharpDynamicInterfaceCastableImplementationFixer : DynamicInterfaceCastableImplementationFixer
    {
        // Manually define the InitKeyword and InitAccessorDeclaration values since we compile against too old of a Roslyn to use it directly.
        // We only generate init accessors if they already exist, so we don't need to worry about these being unrecognized.
        private const SyntaxKind InitKeyword = (SyntaxKind)8443;
        private const SyntaxKind InitAccessorDeclaration = (SyntaxKind)9060;

        protected override async Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode declaration,
            Document document,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var type = (INamedTypeSymbol)editor.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            var generator = editor.Generator;

            var defaultMethodBodyStatements = generator.DefaultMethodBody(editor.SemanticModel.Compilation).ToArray();
            var generatedMembers = new List<SyntaxNode>();
            foreach (var iface in type.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (!member.IsStatic && type.FindImplementationForInterfaceMember(member) is null)
                    {
                        var implementation = member.Kind switch
                        {
                            SymbolKind.Method => GenerateMethodImplementation((IMethodSymbol)member),
                            SymbolKind.Property => GeneratePropertyImplementation((IPropertySymbol)member),
                            SymbolKind.Event => GenerateEventImplementation((IEventSymbol)member, declaration, generator, defaultMethodBodyStatements),
                            _ => null
                        };
                        if (implementation is not null)
                        {
                            generatedMembers.Add(generator.AsPrivateInterfaceImplementation(
                                implementation,
                                generator.NameExpression(member.ContainingType)));
                        }
                    }
                }
            }

            // Explicitly use the C# syntax APIs to work around https://github.com/dotnet/roslyn/issues/53605
            var typeDeclaration = (TypeDeclarationSyntax)declaration;
            typeDeclaration = typeDeclaration.AddMembers(generatedMembers.Cast<MemberDeclarationSyntax>().ToArray());

            editor.ReplaceNode(declaration, typeDeclaration);

            return editor.GetChangedDocument();

            SyntaxNode? GenerateMethodImplementation(IMethodSymbol method)
            {
                // Method with associated symbols are not standalone methods that a user would write in C#.
                // They're methods like property or event accessors, which are covered in the following local functions.
                if (method.AssociatedSymbol is not null)
                {
                    return null;
                }
                var methodDecl = generator.MethodDeclaration(method);
                methodDecl = generator.WithModifiers(methodDecl, generator.GetModifiers(declaration).WithIsAbstract(false));
                return generator.WithStatements(methodDecl, defaultMethodBodyStatements);
            }

            SyntaxNode GeneratePropertyImplementation(IPropertySymbol property)
            {
                var propertyDecl = property.IsIndexer
                    ? generator.IndexerDeclaration(property)
                    : generator.PropertyDeclaration(property);
                propertyDecl = generator.WithModifiers(propertyDecl, generator.GetModifiers(declaration).WithIsAbstract(false));

                // Remove default property accessors.
                propertyDecl = generator.WithAccessorDeclarations(propertyDecl);

                if (property.GetMethod is not null
                    && editor.SemanticModel.Compilation.IsSymbolAccessibleWithin(property.GetMethod, type))
                {
                    propertyDecl = generator.WithGetAccessorStatements(propertyDecl, defaultMethodBodyStatements);
                }
                if (property.SetMethod is not null
                    && editor.SemanticModel.Compilation.IsSymbolAccessibleWithin(property.SetMethod, type))
                {
                    propertyDecl = AddSetAccessor(property, propertyDecl, generator, defaultMethodBodyStatements, includeAccessibility: false);
                }

                return propertyDecl;
            }
        }

        private static SyntaxNode AddSetAccessor(
            IPropertySymbol property,
            SyntaxNode declaration,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements,
            bool includeAccessibility)
        {
            if (!property.SetMethod.IsInitOnly())
            {
                return generator.WithSetAccessorStatements(declaration, defaultMethodBodyStatements);
            }

            var setAccessorAccessibility = includeAccessibility && property.DeclaredAccessibility != property.SetMethod.DeclaredAccessibility
                ? property.SetMethod.DeclaredAccessibility
                : Accessibility.NotApplicable;

            var setAccessor = (AccessorDeclarationSyntax)generator.SetAccessorDeclaration(setAccessorAccessibility, defaultMethodBodyStatements);

            var propDecl = (PropertyDeclarationSyntax)declaration;

            SyntaxNode? oldInitAccessor = null;

            foreach (var accessor in propDecl.AccessorList.Accessors)
            {
                if (accessor.IsKind(InitAccessorDeclaration))
                {
                    oldInitAccessor = accessor;
                    break;
                }
            }

            if (oldInitAccessor is not null)
            {
                propDecl = propDecl.WithAccessorList(propDecl.AccessorList.RemoveNode(oldInitAccessor, SyntaxRemoveOptions.KeepNoTrivia));
            }

            return propDecl.WithAccessorList(propDecl.AccessorList.AddAccessors(
                SyntaxFactory.AccessorDeclaration(
                        InitAccessorDeclaration,
                        setAccessor.AttributeLists,
                        setAccessor.Modifiers,
                        SyntaxFactory.Token(InitKeyword),
                        setAccessor.Body,
                        setAccessor.ExpressionBody,
                        setAccessor.SemicolonToken)));
        }

        private static SyntaxNode GenerateEventImplementation(
            IEventSymbol evt,
            SyntaxNode declaration,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements)
        {
            var eventDecl = generator.CustomEventDeclaration(evt);
            eventDecl = generator.WithModifiers(eventDecl, generator.GetModifiers(declaration).WithIsAbstract(false));

            // Explicitly use the C# syntax APIs to work around https://github.com/dotnet/roslyn/issues/53649
            var eventDeclaration = (EventDeclarationSyntax)eventDecl;

            return eventDeclaration.WithAccessorList(
                SyntaxFactory.AccessorList(
                    SyntaxFactory.List(
                new[]
                {
                        generator.WithStatements(generator.GetAccessor(eventDecl, DeclarationKind.AddAccessor), defaultMethodBodyStatements),
                        generator.WithStatements(generator.GetAccessor(eventDecl, DeclarationKind.RemoveAccessor), defaultMethodBodyStatements),
                })));
        }

        protected override async Task<Document> SealMemberDeclaredOnImplementationType(
            SyntaxNode declaration,
            Document document,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;
            var defaultMethodBodyStatements = generator.DefaultMethodBody(editor.SemanticModel.Compilation).ToArray();

            var symbol = editor.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken);
            if (declaration.IsKind(SyntaxKind.EventFieldDeclaration))
            {
                // We'll only end up here with one event in the event field declaration syntax.
                var eventField = (EventFieldDeclarationSyntax)declaration;
                symbol = editor.SemanticModel.GetDeclaredSymbol(eventField.Declaration.Variables[0], cancellationToken);
            }

            editor.ReplaceNode(generator.GetDeclaration(declaration), SealMemberDeclaration(declaration, symbol));
            return editor.GetChangedDocument();

            SyntaxNode SealMemberDeclaration(SyntaxNode declaration, ISymbol symbol)
            {
                return symbol.Kind switch
                {
                    SymbolKind.Method => SealMethod(symbol),
                    SymbolKind.Property => SealProperty(symbol),
                    SymbolKind.Event => SealEvent(symbol),
                    _ => declaration,
                };
            }

            SyntaxNode SealMethod(ISymbol symbol)
            {
                var modifiers = GetModifiers(generator, declaration);
                if (symbol.IsAbstract)
                {
                    declaration = generator.WithStatements(declaration, defaultMethodBodyStatements);
                }
                return generator.WithModifiers(declaration, modifiers);
            }

            SyntaxNode SealProperty(ISymbol symbol)
            {
                var modifiers = GetModifiers(generator, declaration);
                if (symbol.IsAbstract)
                {
                    var prop = (IPropertySymbol)symbol;
                    if (prop.GetMethod is not null)
                    {
                        declaration = generator.WithGetAccessorStatements(declaration, defaultMethodBodyStatements);
                    }
                    if (prop.SetMethod is not null)
                    {
                        declaration = AddSetAccessor(prop, declaration, generator, defaultMethodBodyStatements, includeAccessibility: true);
                    }
                }
                return generator.WithModifiers(declaration, modifiers);
            }

            SyntaxNode SealEvent(ISymbol symbol)
            {
                var modifiers = GetModifiers(generator, declaration);
                if (symbol.IsAbstract)
                {
                    declaration = GenerateEventImplementation((IEventSymbol)symbol, declaration, generator, defaultMethodBodyStatements);
                }
                return generator.WithModifiers(declaration, modifiers);
            }

            static DeclarationModifiers GetModifiers(SyntaxGenerator generator, SyntaxNode declaration)
            {
                return generator.GetModifiers(declaration)
                                               .WithIsAbstract(false)
                                               .WithIsVirtual(false)
                                               .WithIsSealed(true);
            }
        }

        protected override bool CodeFixSupportsDeclaration(SyntaxNode declaration)
        {
            return !declaration.IsKind(SyntaxKind.VariableDeclarator);
        }
    }
}
