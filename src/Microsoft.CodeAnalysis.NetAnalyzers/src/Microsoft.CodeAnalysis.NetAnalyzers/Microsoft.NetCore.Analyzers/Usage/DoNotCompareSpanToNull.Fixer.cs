// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Usage
{
    public abstract class DoNotCompareSpanToNullFixer : SyntaxEditorBasedCodeFixProvider
    {
        protected const string IsEmpty = nameof(IsEmpty);

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotCompareSpanToNullAnalyzer.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.DoNotCompareSpanToNullIsEmptyCodeFixTitle,
                MicrosoftNetCoreAnalyzersResources.DoNotCompareSpanToNullIsEmptyCodeFixTitle);

            return Task.CompletedTask;
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            // The replacement re-emits the compared expression, and a comparison can sit inside the expression
            // another one compares, so it is read off the node as an inner fix has already rewritten it.
            editor.ReplaceNode(node, (currentNode, _) => MakeIsEmptyCheck(currentNode) ?? currentNode);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Rewrites <paramref name="comparison"/> as an <c>IsEmpty</c> check, or returns <see langword="null"/>
        /// when it is not a shape the fix handles.
        /// </summary>
        protected abstract SyntaxNode? MakeIsEmptyCheck(SyntaxNode comparison);
    }
}