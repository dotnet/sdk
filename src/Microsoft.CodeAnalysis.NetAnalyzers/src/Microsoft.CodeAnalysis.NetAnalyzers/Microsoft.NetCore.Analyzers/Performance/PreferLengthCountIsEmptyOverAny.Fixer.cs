// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class PreferLengthCountIsEmptyOverAnyFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferLengthCountIsEmptyOverAnyAnalyzer.RuleId);

        // The title doubles as the equivalence key and differs per replacement property, so a fix-all pass
        // has to fix only the diagnostics matching the action it was invoked from.
        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                (document, diagnostic, editor, equivalenceKey, cancellationToken) =>
                {
                    if (equivalenceKey is null || equivalenceKey == GetTitle(diagnostic))
                    {
                        ApplyFix(diagnostic, editor);
                    }

                    return Task.CompletedTask;
                });

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                if (GetNodeToReplace(root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)) is null)
                {
                    continue;
                }

                string title = GetTitle(diagnostic);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        async cancellationToken =>
                        {
                            DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, cancellationToken).ConfigureAwait(false);
                            ApplyFix(diagnostic, editor);

                            return editor.GetChangedDocument();
                        },
                        title),
                    diagnostic);
            }
        }

        private void ApplyFix(Diagnostic diagnostic, SyntaxEditor editor)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (GetNodeToReplace(node) is not SyntaxNode toReplace)
            {
                return;
            }

            // `.Any()` calls nest, and the replacement re-emits the receiver, so it has to be read off the
            // node as an inner fix has already rewritten it rather than off the original tree.
            switch (diagnostic.Properties[PreferLengthCountIsEmptyOverAnyAnalyzer.DiagnosticPropertyKey])
            {
                case PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText:
                    editor.ReplaceNode(toReplace, (currentNode, _) => ReplaceAnyWithIsEmpty(currentNode) ?? currentNode);
                    break;

                case PreferLengthCountIsEmptyOverAnyAnalyzer.LengthText:
                    editor.ReplaceNode(toReplace, (currentNode, _) => ReplaceAnyWithPropertyCheck(currentNode, PreferLengthCountIsEmptyOverAnyAnalyzer.LengthText) ?? currentNode);
                    break;

                case PreferLengthCountIsEmptyOverAnyAnalyzer.CountText:
                    editor.ReplaceNode(toReplace, (currentNode, _) => ReplaceAnyWithPropertyCheck(currentNode, PreferLengthCountIsEmptyOverAnyAnalyzer.CountText) ?? currentNode);
                    break;

                default:
                    throw new NotSupportedException();
            }
        }

        private static string GetTitle(Diagnostic diagnostic)
            => diagnostic.Properties[PreferLengthCountIsEmptyOverAnyAnalyzer.DiagnosticPropertyKey] switch
            {
                PreferLengthCountIsEmptyOverAnyAnalyzer.IsEmptyText => MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverAnyCodeFixTitle,
                PreferLengthCountIsEmptyOverAnyAnalyzer.LengthText => MicrosoftNetCoreAnalyzersResources.PreferLengthOverAnyCodeFixTitle,
                PreferLengthCountIsEmptyOverAnyAnalyzer.CountText => MicrosoftNetCoreAnalyzersResources.PreferCountOverAnyCodeFixTitle,
                _ => throw new NotSupportedException()
            };

        /// <summary>
        /// Returns the node the fix replaces - the `.Any()` call, or the negation enclosing it - or
        /// <see langword="null"/> when <paramref name="node"/> is not a shape the fix handles.
        /// </summary>
        protected abstract SyntaxNode? GetNodeToReplace(SyntaxNode node);

        protected abstract SyntaxNode? ReplaceAnyWithIsEmpty(SyntaxNode currentNode);

        protected abstract SyntaxNode? ReplaceAnyWithPropertyCheck(SyntaxNode currentNode, string propertyName);
    }
}