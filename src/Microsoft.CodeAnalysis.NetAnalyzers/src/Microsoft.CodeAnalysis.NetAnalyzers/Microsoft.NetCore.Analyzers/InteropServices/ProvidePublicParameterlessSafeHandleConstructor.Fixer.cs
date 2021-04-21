// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class ProvidePublicParameterlessSafeHandleConstructorFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ProvidePublicParameterlessSafeHandleConstructorAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
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

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            ISymbol symbol = model.GetDeclaredSymbol(declaration, context.CancellationToken);
            if (symbol.Kind == SymbolKind.Method)
            {
                Diagnostic diagnostic = context.Diagnostics.First();
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic,
                        async ct => await MakeParameterlessConstructorPublic(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic)),
                    diagnostic);
            }
            else if (symbol is INamedTypeSymbol type)
            {
                bool baseTypeHasAccessibleParameterlessConstructor = false;
                foreach (var constructor in type.BaseType.InstanceConstructors)
                {
                    if (constructor.Parameters.Length == 0 &&
                        model.Compilation.IsSymbolAccessibleWithin(constructor, type, type))
                    {
                        baseTypeHasAccessibleParameterlessConstructor = true;
                        break;
                    }
                }

                if (baseTypeHasAccessibleParameterlessConstructor)
                {
                    Diagnostic diagnostic = context.Diagnostics.First();
                    context.RegisterCodeFix(
                        new MyCodeAction(
                            MicrosoftNetCoreAnalyzersResources.AddPublicParameterlessConstructor,
                            async ct => await AddParameterlessConstructor(declaration, type, context.Document, context.CancellationToken).ConfigureAwait(false),
                            equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.AddPublicParameterlessConstructor)),
                        diagnostic);
                }
            }
        }

        private static async Task<Document> AddParameterlessConstructor(SyntaxNode declaration, INamedTypeSymbol type, Document document, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            var generator = editor.Generator;

            var parameterlessConstructor = generator.ConstructorDeclaration(type.Name, accessibility: Accessibility.Public);

            editor.AddMember(declaration, parameterlessConstructor);

            return editor.GetChangedDocument();
        }

        private static async Task<Document> MakeParameterlessConstructorPublic(SyntaxNode declaration, Document document, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            editor.SetAccessibility(declaration, Accessibility.Public);
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
