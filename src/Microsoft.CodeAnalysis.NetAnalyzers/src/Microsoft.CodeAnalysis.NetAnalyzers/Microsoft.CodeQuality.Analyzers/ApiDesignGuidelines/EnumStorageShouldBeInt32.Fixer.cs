// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using Analyzer.Utilities;
using System;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1028: Enum Storage should be Int32
    /// </summary>
    public abstract class EnumStorageShouldBeInt32Fixer : CodeFixProvider
    {
        protected abstract SyntaxNode? GetTargetNode(SyntaxNode node);

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EnumStorageShouldBeInt32Analyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Fixes all occurrences within within Document, Project, or Solution
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var title = MicrosoftCodeQualityAnalyzersResources.EnumStorageShouldBeInt32Title;

            // Get syntax root node
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                // Register fixer
                context.RegisterCodeFix(new MyCodeAction(title,
                         c => ChangeEnumTypeToInt32Async(context.Document, diagnostic, root, c),
                         equivalenceKey: title), diagnostic);
            }
        }

        private async Task<Document> ChangeEnumTypeToInt32Async(Document document, Diagnostic diagnostic, SyntaxNode root, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            // Find syntax node that declares the enum
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);
            var enumDeclarationNode = generator.GetDeclaration(node, DeclarationKind.Enum);

            // Find the target syntax node to replace. Was not able to find a language neutral way of doing this. So using the language specific methods
            var targetNode = GetTargetNode(enumDeclarationNode);
            if (targetNode == null)
            {
                return document;
            }

            // Remove target node 
            editor.RemoveNode(targetNode, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia | SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.KeepEndOfLine);

            return editor.GetChangedDocument();
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}
