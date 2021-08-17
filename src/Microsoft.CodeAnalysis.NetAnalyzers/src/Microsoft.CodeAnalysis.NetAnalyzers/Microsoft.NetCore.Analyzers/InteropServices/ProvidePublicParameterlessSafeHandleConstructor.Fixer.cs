// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

            Diagnostic diagnostic = context.Diagnostics.First();
            if (diagnostic.Properties.ContainsKey(ProvidePublicParameterlessSafeHandleConstructorAnalyzer.DiagnosticPropertyConstructorExists))
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic,
                        async ct => await MakeParameterlessConstructorPublic(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.MakeParameterlessConstructorPublic)),
                    diagnostic);
            }
            else if (diagnostic.Properties.ContainsKey(ProvidePublicParameterlessSafeHandleConstructorAnalyzer.DiagnosticPropertyBaseConstructorAccessible))
            {
                context.RegisterCodeFix(
                    new MyCodeAction(
                        MicrosoftNetCoreAnalyzersResources.AddPublicParameterlessConstructor,
                        async ct => await AddParameterlessConstructor(declaration, context.Document, context.CancellationToken).ConfigureAwait(false),
                        equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.AddPublicParameterlessConstructor)),
                    diagnostic);
            }
        }

        private static async Task<Document> AddParameterlessConstructor(SyntaxNode declaration, Document document, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            INamedTypeSymbol type = (INamedTypeSymbol)editor.SemanticModel.GetDeclaredSymbol(declaration, ct);
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
