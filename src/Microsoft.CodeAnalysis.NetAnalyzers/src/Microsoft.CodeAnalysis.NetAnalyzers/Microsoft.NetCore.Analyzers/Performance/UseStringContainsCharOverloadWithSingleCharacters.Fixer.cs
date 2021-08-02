// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class UseStringContainsCharOverloadWithSingleCharactersCodeFix : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            UseStringContainsCharOverloadWithSingleCharactersAnalyzer.CA1847);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var violatingNode = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (TryGetLiteralValueFromNode(violatingNode, out var sourceCharLiteral))
            {
                if (TryGetArgumentName(violatingNode, out var argumentName))
                {
                    context.RegisterCodeFix(new ReplaceStringLiteralWithCharLiteralCodeAction(context.Document, violatingNode, sourceCharLiteral, argumentName), context.Diagnostics);
                }
                else
                {
                    context.RegisterCodeFix(new ReplaceStringLiteralWithCharLiteralCodeAction(context.Document, violatingNode, sourceCharLiteral), context.Diagnostics);
                }
            }
        }

        protected abstract bool TryGetArgumentName(SyntaxNode violatingNode, out string argumentName);
        protected abstract bool TryGetLiteralValueFromNode(SyntaxNode violatingNode, out char charLiteral);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        private class ReplaceStringLiteralWithCharLiteralCodeAction : CodeAction
        {
            private readonly Document _document;
            private readonly SyntaxNode _nodeToBeFixed;
            private readonly char _sourceCharLiteral;
            private readonly string? _argumentName;

            public override string Title => MicrosoftNetCoreAnalyzersResources.ReplaceStringLiteralWithCharLiteralCodeActionTitle;

            public override string EquivalenceKey => nameof(ReplaceStringLiteralWithCharLiteralCodeAction);
            public ReplaceStringLiteralWithCharLiteralCodeAction(Document document, SyntaxNode nodeToBeFixed, char sourceCharLiteral)
            {
                _document = document;
                _nodeToBeFixed = nodeToBeFixed;
                _sourceCharLiteral = sourceCharLiteral;
            }

            public ReplaceStringLiteralWithCharLiteralCodeAction(Document document, SyntaxNode nodeToBeFixed, char sourceCharLiteral, string? argumentName) : this(document, nodeToBeFixed, sourceCharLiteral)
            {
                _argumentName = argumentName;
            }

            protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(_document, cancellationToken).ConfigureAwait(false);
                var newExpression = editor.Generator.LiteralExpression(_sourceCharLiteral);
                if (_argumentName is not null)
                {
                    newExpression = editor.Generator.Argument(_argumentName, RefKind.None, newExpression);
                }
                editor.ReplaceNode(_nodeToBeFixed, newExpression);

                return editor.GetChangedDocument();
            }
        }
    }
}
