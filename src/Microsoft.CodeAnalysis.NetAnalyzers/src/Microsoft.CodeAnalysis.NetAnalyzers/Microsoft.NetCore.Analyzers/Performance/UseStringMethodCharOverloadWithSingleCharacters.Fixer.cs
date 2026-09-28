// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class UseStringMethodCharOverloadWithSingleCharactersFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            UseStringMethodCharOverloadWithSingleCharacters.SafeTransformationRule.Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var argumentListNode = root.FindNode(context.Span, getInnermostNodeForTie: true);

            var model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (TryGetChar(model, argumentListNode, out var c))
            {
                context.RegisterCodeFix(CreateCodeAction(context.Document, argumentListNode, c), context.Diagnostics);
            }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        protected abstract bool TryGetChar(SemanticModel model, SyntaxNode argumentListNode, out char c);

        protected abstract CodeAction CreateCodeAction(Document document, SyntaxNode argumentListNode, char sourceCharLiteral);

        protected abstract class ReplaceStringLiteralWithCharLiteralCodeAction : CodeAction
        {
            private readonly Document _document;
            private readonly SyntaxNode _argumentListNode;
            private readonly char _sourceCharLiteral;

            protected ReplaceStringLiteralWithCharLiteralCodeAction(Document document, SyntaxNode argumentListNode, char sourceCharLiteral)
            {
                _document = document;
                _argumentListNode = argumentListNode;
                _sourceCharLiteral = sourceCharLiteral;
            }

            public override string Title => MicrosoftNetCoreAnalyzersResources.ReplaceStringLiteralWithCharLiteralCodeActionTitle;

            public override string EquivalenceKey => nameof(ReplaceStringLiteralWithCharLiteralCodeAction);

            protected abstract void ApplyFix(DocumentEditor editor, SemanticModel model, SyntaxNode oldArgumentListNode, char c);

            protected static bool PreserveArgument(IArgumentOperation? argument)
            {
                // In our target methods, IndexOf/LastIndexOf have additional int arguments for the `startIndex` and `count`
                // that we want to preserve when fixing.
                // A better method might be to detect StringComparison and CultureInfo in particular and return false on these instead,
                // but that will require a lot of additional effort to resolve these types from here.
                return argument?.Value.Type != null && argument.Value.Type.SpecialType == SpecialType.System_Int32;
            }

            protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(_document, cancellationToken).ConfigureAwait(false);
                var model = await _document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                ApplyFix(editor, model, _argumentListNode, _sourceCharLiteral);

                return editor.GetChangedDocument();
            }
        }
    }
}
