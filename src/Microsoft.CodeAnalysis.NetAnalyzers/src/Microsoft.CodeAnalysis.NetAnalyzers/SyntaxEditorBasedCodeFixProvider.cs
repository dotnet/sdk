// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeAnalysis.NetAnalyzers
{
    /// <summary>
    /// A <see cref="CodeFixProvider"/> whose fix is expressed as edits into a <see cref="SyntaxEditor"/>, so
    /// that a single invocation and a fix-all pass run the same code over the same editor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This mirrors Roslyn's own <c>SyntaxEditorBasedCodeFixProvider</c>, which is <see langword="internal"/>
    /// and source-shared and so cannot be referenced from here.
    /// </para>
    /// <para>
    /// It differs in one way: it applies one diagnostic per call rather than handing the fix the whole batch,
    /// so that the loop and its ordering live in one place instead of in every fixer - which is what Roslyn
    /// needs a second base class, <c>ForkingSyntaxEditorBasedCodeFixProvider&lt;T&gt;</c>, to do.
    /// </para>
    /// </remarks>
    public abstract class SyntaxEditorBasedCodeFixProvider : CodeFixProvider
    {
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create(ApplyFixAsync);

        /// <summary>
        /// Registers an action applying <see cref="ApplyFixAsync"/> to every diagnostic in
        /// <paramref name="context"/>, through the same editor a fix-all pass would use.
        /// </summary>
        protected void RegisterCodeFix(CodeFixContext context, string title, string equivalenceKey)
        {
            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            CodeAction codeAction = CodeAction.Create(
                title,
                (cancellationToken) => SyntaxEditorFixAllProvider.ApplyFixesAsync(document, diagnostics, ApplyFixAsync, cancellationToken),
                equivalenceKey
            );
            context.RegisterCodeFix(codeAction, diagnostics);
        }

        /// <summary>
        /// Applies the fix for a single diagnostic into <paramref name="editor"/>.
        /// </summary>
        /// <remarks>
        /// Every diagnostic in the document is applied into one editor, which is not re-created between them,
        /// so <paramref name="diagnostic"/> locates against <see cref="SyntaxEditor.OriginalRoot"/> and
        /// <paramref name="document"/> is the document as it was before any of the fixes ran. A fix that
        /// encloses another diagnostic has to read the node as already rewritten, through the
        /// <see cref="SyntaxEditor.ReplaceNode(SyntaxNode, Func{SyntaxNode, SyntaxGenerator, SyntaxNode})"/>
        /// overload - see the remarks on <see cref="SyntaxEditorFixAllProvider"/>.
        /// </remarks>
        protected abstract Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken);
    }
}
