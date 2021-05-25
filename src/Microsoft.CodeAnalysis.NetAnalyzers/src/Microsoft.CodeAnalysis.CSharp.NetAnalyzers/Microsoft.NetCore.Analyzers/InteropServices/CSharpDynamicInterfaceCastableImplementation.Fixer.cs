// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
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
        protected override async Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode declaration,
            Document document,
            CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

            INamedTypeSymbol type = (INamedTypeSymbol)editor.SemanticModel.GetDeclaredSymbol(declaration, ct);
            var generator = editor.Generator;

            var defaultMethodBodyStatements = generator.DefaultMethodBody(editor.SemanticModel.Compilation).ToArray();
            List<SyntaxNode> generatedMembers = new List<SyntaxNode>();
            foreach (var iface in type.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (!member.IsStatic && type.FindImplementationForInterfaceMember(member) is null)
                    {
                        SyntaxNode? implementation = member.Kind switch
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
            TypeDeclarationSyntax typeDeclaration = (TypeDeclarationSyntax)declaration;
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
                SyntaxNode methodDecl = generator.MethodDeclaration(method);
                methodDecl = generator.WithModifiers(methodDecl, generator.GetModifiers(declaration).WithIsAbstract(false));
                return generator.WithStatements(methodDecl, defaultMethodBodyStatements);
            }

            SyntaxNode GeneratePropertyImplementation(IPropertySymbol property)
            {
                SyntaxNode propertyDecl = property.IsIndexer
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
                    propertyDecl = generator.WithSetAccessorStatements(propertyDecl, defaultMethodBodyStatements);
                }

                return propertyDecl;
            }
        }

        private static SyntaxNode GenerateEventImplementation(
            IEventSymbol evt,
            SyntaxNode declaration,
            SyntaxGenerator generator,
            SyntaxNode[] defaultMethodBodyStatements)
        {
            SyntaxNode eventDecl = generator.CustomEventDeclaration(evt);
            eventDecl = generator.WithModifiers(eventDecl, generator.GetModifiers(declaration).WithIsAbstract(false));

            // Explicitly use the C# syntax APIs to work around https://github.com/dotnet/roslyn/issues/53649
            EventDeclarationSyntax eventDeclaration = (EventDeclarationSyntax)eventDecl;

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
    CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            var generator = editor.Generator;
            var defaultMethodBodyStatements = generator.DefaultMethodBody(editor.SemanticModel.Compilation).ToArray();

            ISymbol symbol = editor.SemanticModel.GetDeclaredSymbol(declaration, ct);
            if (declaration.IsKind(SyntaxKind.EventFieldDeclaration))
            {
                // We'll only end up here with one event in the event field declaration syntax.
                EventFieldDeclarationSyntax eventField = (EventFieldDeclarationSyntax)declaration;
                symbol = editor.SemanticModel.GetDeclaredSymbol(eventField.Declaration.Variables[0], ct);
            }

            editor.ReplaceNode(generator.GetDeclaration(declaration), SealMemberDeclaration(declaration, symbol));
            return editor.GetChangedDocument();

            SyntaxNode SealMemberDeclaration(SyntaxNode declaration, ISymbol symbol)
            {
                switch (symbol.Kind)
                {
                    case SymbolKind.Method:
                        {
                            var modifiers = GetModifiers(generator, declaration);
                            if (symbol.IsAbstract)
                            {
                                declaration = generator.WithStatements(declaration, defaultMethodBodyStatements);
                            }
                            return generator.WithModifiers(declaration, modifiers);
                        }
                    case SymbolKind.Property:
                        {
                            DeclarationModifiers modifiers = GetModifiers(generator, declaration);
                            if (symbol.IsAbstract)
                            {
                                IPropertySymbol prop = (IPropertySymbol)symbol;
                                if (prop.GetMethod is not null)
                                {
                                    declaration = generator.WithGetAccessorStatements(declaration, defaultMethodBodyStatements);
                                }
                                if (prop.SetMethod is not null)
                                {
                                    declaration = generator.WithSetAccessorStatements(declaration, defaultMethodBodyStatements);
                                }
                            }
                            return generator.WithModifiers(declaration, modifiers);
                        }
                    case SymbolKind.Event:
                        {
                            DeclarationModifiers modifiers = GetModifiers(generator, declaration);
                            if (symbol.IsAbstract)
                            {
                                declaration = GenerateEventImplementation((IEventSymbol)symbol, declaration, generator, defaultMethodBodyStatements);
                            }
                            return generator.WithModifiers(declaration, modifiers);
                        }
                }

                return declaration;

                static DeclarationModifiers GetModifiers(SyntaxGenerator generator, SyntaxNode declaration)
                {
                    return generator.GetModifiers(declaration)
                                                   .WithIsAbstract(false)
                                                   .WithIsVirtual(false)
                                                   .WithIsSealed(true);
                }
            }
        }

        protected override bool CodeFixSupportsDeclaration(SyntaxNode declaration)
        {
            return !declaration.IsKind(SyntaxKind.VariableDeclarator);
        }
    }
}
