// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public abstract class DynamicInterfaceCastableImplementationFixer : CodeFixProvider
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

        protected abstract Task<Document> ImplementInterfacesOnDynamicCastableImplementation(
            SyntaxNode declaration,
            Document document,
            CancellationToken ct);

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
