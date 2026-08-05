// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.NetAnalyzers
{
    /// <summary>
    /// Routes every diagnostic in a document through a single <see cref="SyntaxEditor"/>, either as a
    /// <see cref="FixAllProvider"/> or, through <see cref="ApplyFixesAsync"/>, from a fixer's own code action.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WellKnownFixAllProviders.BatchFixer"/> computes a separate fixed document for every
    /// diagnostic, each against the original document, then diffs each one and merges the results. That is one
    /// document fork, one semantic model, and one text diff per diagnostic, so the cost of fixing a document
    /// grows with the number of diagnostics in it rather than with the size of the document.
    /// </para>
    /// <para>
    /// It is also incomplete: when two of those text changes overlap the merge silently discards one of them,
    /// which happens whenever several diagnostics resolve to the same edit anchor - for example a fix that adds
    /// a member to the declaration that contains the diagnostic. The dropped diagnostic survives and the user
    /// has to invoke fix-all again.
    /// </para>
    /// <para>
    /// The fix is handed a <see cref="SyntaxEditor"/> and the <see cref="Document"/> it came from rather than a
    /// <see cref="DocumentEditor"/>, because <see cref="DocumentEditor.CreateAsync"/> binds a
    /// <see cref="SemanticModel"/> whether or not the fix reads one. A fix that needs semantics awaits
    /// <see cref="Document.GetSemanticModelAsync"/> itself; the document caches the model, so asking once per
    /// diagnostic still binds once.
    /// </para>
    /// <para>
    /// A fix that derives a name or a signature from the semantic model needs more than a shared editor,
    /// because every fix still binds against the unmodified document. Give those a <c>TState</c> recording what
    /// earlier fixes already produced, or they will agree on a name and emit code that does not compile.
    /// </para>
    /// <para>
    /// Nesting is handled but not composed. <see cref="SyntaxEditor"/> tracks every node it is asked to change
    /// and throws once a change replaces a node another change still needs, so a document whose diagnostics
    /// nest is fixed innermost-first rather than in source order. That is enough to keep the pass from
    /// failing, and the enclosing fix still re-emits the syntax it was built from, so an inner diagnostic it
    /// encloses survives into the next fix-all iteration. To fix both in one pass the enclosing fix has to
    /// read the node as already rewritten, which means expressing it through the
    /// <see cref="SyntaxEditor.ReplaceNode(SyntaxNode, Func{SyntaxNode, SyntaxGenerator, SyntaxNode})"/>
    /// overload rather than building a replacement out of <see cref="SyntaxEditor.OriginalRoot"/>.
    /// </para>
    /// <para>
    /// <see cref="DocumentBasedFixAllProvider"/> hands over every diagnostic it collected, including ones the
    /// user's chosen action does not apply to - unlike <see cref="WellKnownFixAllProviders.BatchFixer"/>, it
    /// does not filter by <see cref="FixAllContext.CodeActionEquivalenceKey"/>. A fixer that registers more
    /// than one action, or that reports secondary diagnostics it does not fix, has to read the key through
    /// <c>createDocumentState</c> and skip the diagnostics its action does not cover.
    /// </para>
    /// </remarks>
    public static class SyntaxEditorFixAllProvider
    {
        /// <summary>
        /// Creates a provider for a fix that needs nothing beyond the editor and the diagnostic.
        /// </summary>
        public static FixAllProvider Create(
            Action<Document, Diagnostic, SyntaxEditor> applyFix)
        {
            return new Provider<object?>(
                _ => null,
                (document, diagnostic, editor, _, _) =>
                {
                    applyFix(document, diagnostic, editor);
                    return Task.CompletedTask;
                },
                fixAllTitle: null);
        }

        /// <summary>
        /// Creates a provider for an asynchronous fix that needs nothing beyond the editor and the diagnostic.
        /// </summary>
        public static FixAllProvider Create(
            Func<Document, Diagnostic, SyntaxEditor, CancellationToken, Task> applyFixAsync)
        {
            return new Provider<object?>(
                _ => null,
                (document, diagnostic, editor, _, cancellationToken) => applyFixAsync(document, diagnostic, editor, cancellationToken),
                fixAllTitle: null);
        }

        /// <summary>
        /// Creates a provider for a fix that is asynchronous, needs state shared across a document, or needs to
        /// know which of several registered actions the user picked.
        /// </summary>
        /// <param name="createDocumentState">
        /// Invoked once per document. Use it to carry state across the document's fixes, and to read
        /// <see cref="FixAllContext.CodeActionEquivalenceKey"/> - <see cref="DocumentBasedFixAllProvider"/> does
        /// not filter diagnostics by it, so a fixer that registers more than one action has to honor it here.
        /// </param>
        /// <param name="getFixedDocument">
        /// Optional. Produces the document from the editor once every fix has been applied. Use it for a step
        /// that has to happen once rather than per diagnostic, such as adding a namespace import. Defaults to
        /// applying <see cref="SyntaxEditor.GetChangedRoot"/> to the document.
        /// </param>
        public static FixAllProvider Create<TState>(
            Func<FixAllContext, TState> createDocumentState,
            Func<Document, Diagnostic, SyntaxEditor, TState, CancellationToken, Task> applyFixAsync,
            string? fixAllTitle = null,
            Func<Document, SyntaxEditor, TState, Document>? getFixedDocument = null)
        {
            return new Provider<TState>(createDocumentState, applyFixAsync, fixAllTitle, getFixedDocument);
        }

        /// <summary>
        /// Applies a fix to every one of a document's diagnostics through a single
        /// <see cref="SyntaxEditor"/>, in the same order a fix-all pass would.
        /// </summary>
        /// <remarks>
        /// Call this from <see cref="CodeFixProvider.RegisterCodeFixesAsync"/> when a fixer registers one action
        /// covering all of <see cref="CodeFixContext.Diagnostics"/>, so that a single invocation and a fix-all
        /// pass go through the same code.
        /// </remarks>
        public static Task<Document> ApplyFixesAsync(
            Document document,
            ImmutableArray<Diagnostic> diagnostics,
            Func<Document, Diagnostic, SyntaxEditor, CancellationToken, Task> applyFixAsync,
            CancellationToken cancellationToken = default)
        {
            return ApplyFixesAsync<object?>(
                document,
                diagnostics,
                state: null,
                (doc, diagnostic, editor, _, token) => applyFixAsync(doc, diagnostic, editor, token),
                getFixedDocument: null,
                cancellationToken);
        }

        private static async Task<Document> ApplyFixesAsync<TState>(
            Document document,
            ImmutableArray<Diagnostic> diagnostics,
            TState state,
            Func<Document, Diagnostic, SyntaxEditor, TState, CancellationToken, Task> applyFixAsync,
            Func<Document, SyntaxEditor, TState, Document>? getFixedDocument,
            CancellationToken cancellationToken)
        {
            // The host can report the same diagnostic more than once (https://github.com/dotnet/roslyn/issues/31381),
            // and applying a fix twice to one node throws rather than producing a duplicate edit.
            ImmutableArray<Diagnostic> distinctDiagnostics = diagnostics.Distinct().ToImmutableArray();

            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = new SyntaxEditor(root, document.Project.Solution.Workspace);

            foreach (Diagnostic diagnostic in Order(distinctDiagnostics))
            {
                await applyFixAsync(document, diagnostic, editor, state, cancellationToken).ConfigureAwait(false);
            }

            return getFixedDocument is null
                ? document.WithSyntaxRoot(editor.GetChangedRoot())
                : getFixedDocument(document, editor, state);
        }

        private sealed class Provider<TState> : DocumentBasedFixAllProvider
        {
            private readonly Func<FixAllContext, TState> _createDocumentState;
            private readonly Func<Document, Diagnostic, SyntaxEditor, TState, CancellationToken, Task> _applyFixAsync;
            private readonly string? _fixAllTitle;
            private readonly Func<Document, SyntaxEditor, TState, Document>? _getFixedDocument;

            public Provider(
                Func<FixAllContext, TState> createDocumentState,
                Func<Document, Diagnostic, SyntaxEditor, TState, CancellationToken, Task> applyFixAsync,
                string? fixAllTitle,
                Func<Document, SyntaxEditor, TState, Document>? getFixedDocument = null)
            {
                _createDocumentState = createDocumentState;
                _applyFixAsync = applyFixAsync;
                _fixAllTitle = fixAllTitle;
                _getFixedDocument = getFixedDocument;
            }

            protected override string GetFixAllTitle(FixAllContext fixAllContext)
                => _fixAllTitle ?? base.GetFixAllTitle(fixAllContext);

            protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                if (diagnostics.IsEmpty)
                {
                    return document;
                }

                return await ApplyFixesAsync(
                    document,
                    diagnostics,
                    _createDocumentState(fixAllContext),
                    _applyFixAsync,
                    _getFixedDocument,
                    fixAllContext.CancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Orders a document's diagnostics so that each fix sees the edits made by the fixes before it.
        /// </summary>
        /// <remarks>
        /// Ascending by span start, as Roslyn's own fixers use. A single nested pair reverses the whole
        /// document rather than only that pair: an enclosing replacement discards the editor's tracking of
        /// everything inside it, so the inner fix has to run first, and the fixes neither of them encloses
        /// are indifferent to which end the pass starts from. Roslyn's forking provider reverses the same
        /// way, by draining a stack it filled in source order.
        /// </remarks>
        internal static IEnumerable<Diagnostic> Order(ImmutableArray<Diagnostic> diagnostics)
        {
            return HasNestedSpans(diagnostics)
                ? diagnostics
                    .OrderByDescending(diagnostic => diagnostic.Location.SourceSpan.Start)
                    .ThenBy(diagnostic => diagnostic.Location.SourceSpan.End)
                : diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start);
        }

        private static bool HasNestedSpans(ImmutableArray<Diagnostic> diagnostics)
        {
            if (diagnostics.Length < 2)
            {
                return false;
            }

            // Sweeping in source order, a span that ends before the furthest end seen so far begins at or
            // after - and ends before - a span already visited, so it is contained in it. Equal spans are
            // not nesting: several diagnostics reported at one node are fixed against one target.
            TextSpan[] spans = diagnostics
                .Select(diagnostic => diagnostic.Location.SourceSpan)
                .OrderBy(span => span.Start)
                .ThenByDescending(span => span.End)
                .ToArray();

            int furthestEnd = spans[0].End;

            for (int i = 1; i < spans.Length; i++)
            {
                if (spans[i].End < furthestEnd)
                {
                    return true;
                }

                furthestEnd = spans[i].End;
            }

            return false;
        }
    }
}
