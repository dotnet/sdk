// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1827: Do not use Count()/LongCount() when Any() can be used.
    /// CA1828: Do not use CountAsync()/LongCountAsync() when AnyAsync() can be used.
    /// </summary>
    public abstract class DoNotUseCountWhenAnyCanBeUsedFixer : CodeFixProvider
    {
        private const string AsyncMethodName = "AnyAsync";
        private const string SyncMethodName = "Any";

        /// <summary>
        /// A list of diagnostic IDs that this provider can provider fixes for.
        /// </summary>
        /// <value>The fixable diagnostic ids.</value>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(
                UseCountProperlyAnalyzer.CA1827,
                UseCountProperlyAnalyzer.CA1828);

        /// <summary>
        /// Gets an optional <see cref="FixAllProvider" /> that can fix all/multiple occurrences of diagnostics fixed by this code fix provider.
        /// </summary>
        /// <returns>FixAllProvider.</returns>
        /// <remarks>
        /// The synchronous and asynchronous fixes carry different equivalence keys, so this filters on the key
        /// itself -- <see cref="SyntaxEditorFixAllProvider"/> does not.
        /// </remarks>
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        /// <summary>
        /// Computes one or more fixes for the specified <see cref="CodeFixContext" />.
        /// </summary>
        /// <param name="context">A <see cref="CodeFixContext" /> containing context information about the diagnostics to fix.
        /// The context must only contain diagnostics with a <see cref="Diagnostic.Id" /> included in the <see cref="CodeFixProvider.FixableDiagnosticIds" /> 
        /// for the current provider.</param>
        /// <returns>A <see cref="Task" /> that represents the asynchronous operation.</returns>
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            var diagnostic = context.Diagnostics[0];
            var isAsync = IsAsync(diagnostic);

            if (node is object &&
                diagnostic.Properties.TryGetValue(UseCountProperlyAnalyzer.OperationKey, out var operation) &&
                this.TryGetFixer(node, operation!, isAsync, out _, out _))
            {
                var document = context.Document;
                var diagnostics = context.Diagnostics;
                var title = GetTitle(isAsync);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        ct => SyntaxEditorFixAllProvider.ApplyFixesAsync(document, diagnostics, (doc, diag, editor, token) => ApplyFixAsync(doc, diag, editor, title, token), ct),
                        title),
                    diagnostics);
            }
        }

        private Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            var isAsync = IsAsync(diagnostic);

            if (equivalenceKey is not null && equivalenceKey != GetTitle(isAsync))
            {
                return Task.CompletedTask;
            }

            var pattern = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (pattern is null ||
                !diagnostic.Properties.TryGetValue(UseCountProperlyAnalyzer.OperationKey, out var operation) ||
                !this.TryGetFixer(pattern, operation!, isAsync, out var expression, out var arguments))
            {
                return Task.CompletedTask;
            }

            var shouldNegate = diagnostic.Properties.ContainsKey(UseCountProperlyAnalyzer.ShouldNegateKey);
            var carriedOver = new List<SyntaxNode>(arguments) { expression };

            //  The replacement is built out of the reported node's own descendants, so track them: a nested
            //  violation may already have been rewritten by the time this fix runs.
            foreach (var node in carriedOver)
            {
                editor.TrackNode(node);
            }

            editor.ReplaceNode(pattern, (currentNode, generator) =>
            {
                SyntaxNode Current(SyntaxNode original) => currentNode.GetCurrentNode(original) ?? original;

                var memberAccess = generator.MemberAccessExpression(Current(expression).WithoutTrailingTrivia(), isAsync ? AsyncMethodName : SyncMethodName);
                var replacementSyntax = generator.InvocationExpression(memberAccess, arguments.Select(Current));

                if (isAsync)
                {
                    replacementSyntax = generator.AwaitExpression(replacementSyntax);
                }

                if (shouldNegate)
                {
                    replacementSyntax = generator.LogicalNotExpression(replacementSyntax);
                }

                return replacementSyntax
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithTriviaFrom(currentNode);
            });

            return Task.CompletedTask;
        }

        private static bool IsAsync(Diagnostic diagnostic)
            => diagnostic.Properties.ContainsKey(UseCountProperlyAnalyzer.IsAsyncKey) ||
               diagnostic.Id == UseCountProperlyAnalyzer.CA1828;

        private static string GetTitle(bool isAsync)
            => isAsync ?
                MicrosoftNetCoreAnalyzersResources.DoNotUseCountAsyncWhenAnyAsyncCanBeUsedTitle :
                MicrosoftNetCoreAnalyzersResources.DoNotUseCountWhenAnyCanBeUsedTitle;

        /// <summary>
        /// Tries to get a fixer for the specified <paramref name="node" />.
        /// </summary>
        /// <param name="node">The node to get a fixer for.</param>
        /// <param name="operation">The operation to get the fixer from.</param>
        /// <param name="isAsync"><see langword="true" /> if it's an asynchronous method; <see langword="false" /> otherwise.</param>
        /// <param name="expression">If this method returns <see langword="true" />, contains the expression to be used to invoke <c>Any</c>.</param>
        /// <param name="arguments">If this method returns <see langword="true" />, contains the arguments from <c>Any</c> to be used on <c>Count</c>.</param>
        /// <returns><see langword="true" /> if a fixer was found., <see langword="false" /> otherwise.</returns>
        protected abstract bool TryGetFixer(
            SyntaxNode node,
            string operation,
            bool isAsync,
            [NotNullWhen(returnValue: true)] out SyntaxNode? expression,
            [NotNullWhen(returnValue: true)] out IEnumerable<SyntaxNode>? arguments);
    }
}
