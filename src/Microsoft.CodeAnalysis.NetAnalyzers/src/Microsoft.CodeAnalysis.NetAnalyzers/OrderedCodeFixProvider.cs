// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeAnalysis.NetAnalyzers
{
    public abstract class OrderedCodeFixProvider : CodeFixProvider
    {
        public sealed override FixAllProvider GetFixAllProvider() => FixAllProvider.Create((context, document, diagnostics) => FixAllAsync(document, diagnostics, context.CancellationToken)!);

        protected abstract string CodeActionTitle { get; }

        protected abstract string CodeActionEquivalenceKey { get; }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            CodeAction codeAction = CodeAction.Create(
                CodeActionTitle,
                (cancellationToken) => FixAllAsync(document, diagnostics, cancellationToken),
                CodeActionEquivalenceKey
            );
            context.RegisterCodeFix(codeAction, diagnostics);

            return Task.CompletedTask;
        }

        protected abstract Task FixAllCoreAsync(SyntaxEditor editor, SyntaxGenerator generator, Diagnostic diagnostic, CancellationToken cancellationToken);

        private async Task<Document> FixAllAsync(Document document, ImmutableArray<Diagnostic> diagnostics, CancellationToken cancellationToken)
        {
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            SyntaxGenerator generator = editor.Generator;

            // Process diagnostics from inside-out so that higher up edits see and pass along the edits made to inner constructs.
            var orderedDiagnostics = diagnostics.OrderByDescending((diagnostic) => diagnostic.Location.SourceSpan.Start);

            foreach (var diagnostic in orderedDiagnostics)
            {
                await FixAllCoreAsync(editor, generator, diagnostic, cancellationToken).ConfigureAwait(false);
            }

            return editor.GetChangedDocument();
        }
    }
}
