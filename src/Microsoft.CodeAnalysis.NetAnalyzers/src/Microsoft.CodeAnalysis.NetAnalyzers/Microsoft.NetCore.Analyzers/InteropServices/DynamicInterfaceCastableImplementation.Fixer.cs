// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public class DynamicInterfaceCastableImplementationFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                DynamicInterfaceCastableImplementationAnalyzer.InterfaceMethodsMissingImplementationRuleId,
                DynamicInterfaceCastableImplementationAnalyzer.MethodsDeclaredOnImplementationTypeMustBeSealedRuleId);

        public override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span);
            SyntaxNode declaration = generator.GetDeclaration(enclosingNode);
            if (declaration == null)
            {
                return;
            }
            Diagnostic diagnostic = context.Diagnostics.First();
            if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.InterfaceMethodsMissingImplementationRuleId)
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation,
                        async ct => await ImplementInterfacesOnDynamicCastableImplementation(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation)),
                    diagnostic);
            }
            else if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.MethodsDeclaredOnImplementationTypeMustBeSealedRuleId)
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.SealMethodDeclaredOnImplementationType,
                        async ct => await SealMethodDeclaredOnImplementationType(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.SealMethodDeclaredOnImplementationType)),
                    diagnostic);
            }
        }

        private static async Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
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
                        generatedMembers.Add(generator.AsPrivateInterfaceImplementation(
                            member.Kind switch
                            {
                                SymbolKind.Method => GenerateMethodImplementation((IMethodSymbol)member),
                                SymbolKind.Property => GeneratePropertyImplementation((IPropertySymbol)member),
                                SymbolKind.Event => GenerateEventImplementation((IEventSymbol)member),
                                _ => throw new InvalidOperationException()
                            },
                            generator.NameExpression(member.ContainingType)));
                    }
                }
            }

            editor.ReplaceNode(declaration, generator.AddMembers(declaration, generatedMembers));

            return editor.GetChangedDocument();

            SyntaxNode GenerateMethodImplementation(IMethodSymbol method)
            {
                SyntaxNode methodDecl = generator.MethodDeclaration(method);
                methodDecl = generator.WithModifiers(methodDecl, generator.GetModifiers(declaration).WithIsAbstract(false));
                return generator.WithStatements(methodDecl, defaultMethodBodyStatements);
            }

            SyntaxNode GeneratePropertyImplementation(IPropertySymbol property)
            {
                return generator.PropertyDeclaration(
                    property,
                    getAccessorStatements: defaultMethodBodyStatements,
                    setAccessorStatements: defaultMethodBodyStatements);
            }

            SyntaxNode GenerateEventImplementation(IEventSymbol evt)
            {
                return generator.CustomEventDeclaration(
                    evt,
                    addAccessorStatements: defaultMethodBodyStatements,
                    removeAccessorStatements: defaultMethodBodyStatements);
            }
        }

        private static async Task<Document> SealMethodDeclaredOnImplementationType(
            SyntaxNode declaration,
            Document document,
            CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            IMethodSymbol method = (IMethodSymbol)editor.SemanticModel.GetDeclaredSymbol(declaration, ct);
            var generator = editor.Generator;
            DeclarationModifiers modifiers = generator.GetModifiers(declaration)
                .WithIsAbstract(false)
                .WithIsVirtual(false)
                .WithIsSealed(true);
            if (method.IsAbstract)
            {
                editor.SetStatements(declaration, generator.DefaultMethodBody(editor.SemanticModel.Compilation));
            }
            editor.SetModifiers(declaration, modifiers);
            return editor.GetChangedDocument();
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
