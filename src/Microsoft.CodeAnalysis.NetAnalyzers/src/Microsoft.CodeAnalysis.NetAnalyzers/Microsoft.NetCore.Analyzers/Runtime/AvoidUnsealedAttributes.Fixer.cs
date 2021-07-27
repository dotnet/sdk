// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1813: Avoid unsealed attributes
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public class AvoidUnsealedAttributesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(AvoidUnsealedAttributesAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, context.CancellationToken).ConfigureAwait(false);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);
            SyntaxNode declaration = editor.Generator.GetDeclaration(node);

            if (declaration != null)
            {
                string title = MicrosoftNetCoreAnalyzersResources.AvoidUnsealedAttributesMessage;
                context.RegisterCodeFix(new MyCodeAction(title,
                    async ct => await MakeSealedAsync(editor, declaration).ConfigureAwait(false),
                    equivalenceKey: title),
                    context.Diagnostics);
            }
        }

        private static Task<Document> MakeSealedAsync(DocumentEditor editor, SyntaxNode declaration)
        {
            DeclarationModifiers modifiers = editor.Generator.GetModifiers(declaration);
            editor.SetModifiers(declaration, modifiers + DeclarationModifiers.Sealed);
            return Task.FromResult(editor.GetChangedDocument());
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
