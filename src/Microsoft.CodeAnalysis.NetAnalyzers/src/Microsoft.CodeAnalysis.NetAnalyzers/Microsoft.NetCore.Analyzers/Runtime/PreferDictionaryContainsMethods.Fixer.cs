// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class PreferDictionaryContainsMethodsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(PreferDictionaryContainsMethods.RuleId);

        // One title per replaced property, so a fix-all pass has to apply only the one the user invoked it from.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                (document, diagnostic, editor, equivalenceKey, cancellationToken) =>
                {
                    ApplyFix(diagnostic, editor, equivalenceKey);
                    return Task.CompletedTask;
                });

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (GetTitle(root.FindNode(context.Span)) is not string title)
            {
                return;
            }

            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                        document,
                        diagnostics,
                        (_, diagnostic, editor, _) =>
                        {
                            ApplyFix(diagnostic, editor, title);
                            return Task.CompletedTask;
                        },
                        cancellationToken),
                    equivalenceKey: title),
                diagnostics);
        }

        private void ApplyFix(Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (GetPropertyName(node) is not string propertyName ||
                (equivalenceKey is not null && equivalenceKey != GetTitle(propertyName)))
            {
                return;
            }

            string methodName = propertyName == PreferDictionaryContainsMethods.KeysPropertyName
                ? PreferDictionaryContainsMethods.ContainsKeyMethodName
                : PreferDictionaryContainsMethods.ContainsValueMethodName;

            // The replacement re-emits the dictionary and the arguments, and a `Keys.Contains` call can sit
            // inside the argument of another, so those are read off the node as an inner fix has already
            // rewritten it.
            editor.ReplaceNode(node, (currentNode, generator) => Rewrite(currentNode, methodName, generator) ?? currentNode);
        }

        private string? GetTitle(SyntaxNode node)
            => GetPropertyName(node) is string propertyName ? GetTitle(propertyName) : null;

        private static string GetTitle(string propertyName)
            => propertyName == PreferDictionaryContainsMethods.KeysPropertyName
                ? MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsKeyCodeFixTitle
                : MicrosoftNetCoreAnalyzersResources.PreferDictionaryContainsValueCodeFixTitle;

        /// <summary>
        /// Returns <see cref="PreferDictionaryContainsMethods.KeysPropertyName"/> or
        /// <see cref="PreferDictionaryContainsMethods.ValuesPropertyName"/> for the property
        /// <paramref name="invocation"/> calls <c>Contains</c> on, or <see langword="null"/> when it is not a
        /// shape the fix handles.
        /// </summary>
        protected abstract string? GetPropertyName(SyntaxNode invocation);

        /// <summary>
        /// Rewrites <paramref name="invocation"/> as a call to <paramref name="methodName"/> on the dictionary
        /// itself, or returns <see langword="null"/> when it is not a shape the fix handles.
        /// </summary>
        protected abstract SyntaxNode? Rewrite(SyntaxNode invocation, string methodName, SyntaxGenerator generator);
    }
}
