// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1821: Remove empty finalizers
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = RemoveEmptyFinalizersAnalyzer.RuleId), Shared]
    public sealed class RemoveEmptyFinalizersFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RemoveEmptyFinalizersAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            if (node == null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizers;
            context.RegisterCodeFix(new MyCodeAction(title,
                             async ct => await RemoveFinalizerAsync(context.Document, node, ct).ConfigureAwait(false),
                             equivalenceKey: title),
                        context.Diagnostics);
            return;
        }

        private static async Task<Document> RemoveFinalizerAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Get the declaration so that we step up to the methodblocksyntax and not the methodstatementsyntax for VB.
            node = editor.Generator.GetDeclaration(node);
            editor.RemoveNode(node);
            return editor.GetChangedDocument();
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
