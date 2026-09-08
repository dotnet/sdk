// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class UseStringContainsCharOverloadWithSingleCharactersCodeFix : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            UseStringContainsCharOverloadWithSingleCharactersAnalyzer.CA1847);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var violatingNode = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (!TryGetLiteralValueFromNode(violatingNode, out _))
            {
                return;
            }

            RegisterCodeFix(context,
                MicrosoftNetCoreAnalyzersResources.ReplaceStringLiteralWithCharLiteralCodeActionTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.ReplaceStringLiteralWithCharLiteralCodeActionTitle));
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var violatingNode = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (!TryGetLiteralValueFromNode(violatingNode, out var sourceCharLiteral))
            {
                return Task.CompletedTask;
            }

            // The replacement is a fresh literal and a string literal has no diagnosable descendants,
            // so nothing of the original node is carried over.
            var newExpression = editor.Generator.LiteralExpression(sourceCharLiteral);
            if (TryGetArgumentName(violatingNode, out var argumentName))
            {
                newExpression = editor.Generator.Argument(argumentName, RefKind.None, newExpression);
            }

            editor.ReplaceNode(violatingNode, newExpression);

            return Task.CompletedTask;
        }

        protected abstract bool TryGetArgumentName(SyntaxNode violatingNode, out string argumentName);
        protected abstract bool TryGetLiteralValueFromNode(SyntaxNode violatingNode, out char charLiteral);
    }
}
