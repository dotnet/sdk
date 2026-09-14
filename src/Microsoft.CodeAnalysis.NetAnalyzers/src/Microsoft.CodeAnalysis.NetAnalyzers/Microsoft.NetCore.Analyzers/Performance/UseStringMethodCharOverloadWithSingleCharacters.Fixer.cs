// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class UseStringMethodCharOverloadWithSingleCharactersFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            UseStringMethodCharOverloadWithSingleCharacters.SafeTransformationRule.Id);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var argumentListNode = root.FindNode(context.Span, getInnermostNodeForTie: true);

            var model = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (TryGetChar(model, argumentListNode, out _))
            {
                RegisterCodeFix(context,
                    MicrosoftNetCoreAnalyzersResources.ReplaceStringLiteralWithCharLiteralCodeActionTitle,
                    nameof(MicrosoftNetCoreAnalyzersResources.ReplaceStringLiteralWithCharLiteralCodeActionTitle));
            }
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var argumentListNode = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (!TryGetChar(model, argumentListNode, out var c))
            {
                return;
            }

            // Which arguments survive is a semantic question and has to be answered against the original
            // tree, but a surviving argument can itself hold a diagnosed call, so the nodes carried over
            // are taken by position from the list as the editor has rewritten it.
            var preservedIndices = GetArguments(argumentListNode)
                .Select((argument, index) => (argument, index))
                .Where(t => PreserveArgument(model.GetOperation(t.argument, cancellationToken) as IArgumentOperation))
                .Select(t => t.index)
                .ToImmutableArray();

            editor.ReplaceNode(argumentListNode, (currentNode, generator) =>
            {
                var currentArguments = GetArguments(currentNode);
                var arguments = new[] { generator.Argument(generator.LiteralExpression(c)) }
                    .Concat(preservedIndices.Select(index => currentArguments[index]));

                return CreateArgumentList(arguments).WithTriviaFrom(currentNode);
            });
        }

        protected abstract bool TryGetChar(SemanticModel model, SyntaxNode argumentListNode, out char c);

        protected abstract ImmutableArray<SyntaxNode> GetArguments(SyntaxNode argumentListNode);

        protected abstract SyntaxNode CreateArgumentList(IEnumerable<SyntaxNode> arguments);

        private static bool PreserveArgument(IArgumentOperation? argument)
        {
            // In our target methods, IndexOf/LastIndexOf have additional int arguments for the `startIndex` and `count`
            // that we want to preserve when fixing.
            // A better method might be to detect StringComparison and CultureInfo in particular and return false on these instead,
            // but that will require a lot of additional effort to resolve these types from here.
            return argument?.Value.Type != null && argument.Value.Type.SpecialType == SpecialType.System_Int32;
        }
    }
}
