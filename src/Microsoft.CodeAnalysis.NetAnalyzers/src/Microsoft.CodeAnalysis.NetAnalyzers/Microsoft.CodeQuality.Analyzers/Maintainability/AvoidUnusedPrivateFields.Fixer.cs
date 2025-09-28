﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeActions;
using Analyzer.Utilities;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1823: Avoid unused private fields
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = AvoidUnusedPrivateFieldsAnalyzer.RuleId), Shared]
    public sealed class AvoidUnusedPrivateFieldsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidUnusedPrivateFieldsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            if (node == null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.AvoidUnusedPrivateFieldsTitle;
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    async ct => await RemoveFieldAsync(context.Document, node, ct).ConfigureAwait(false),
                    equivalenceKey: title),
                context.Diagnostics);

            return;
        }

        private static async Task<Document> RemoveFieldAsync(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            node = editor.Generator.GetDeclaration(node);
            editor.RemoveNode(node);
            return editor.GetChangedDocument();
        }
    }
}