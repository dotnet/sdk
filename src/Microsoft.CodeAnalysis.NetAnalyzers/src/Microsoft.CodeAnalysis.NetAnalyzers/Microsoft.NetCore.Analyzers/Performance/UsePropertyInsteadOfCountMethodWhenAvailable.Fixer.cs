// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1829: Use property instead of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>, when available.
    /// Implements the <see cref="CodeFixProvider" />
    /// </summary>
    public abstract class UsePropertyInsteadOfCountMethodWhenAvailableFixer : SyntaxEditorBasedCodeFixProvider
    {
        /// <summary>
        /// A list of diagnostic IDs that this provider can provider fixes for.
        /// </summary>
        /// <value>The fixable diagnostic ids.</value>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseCountProperlyAnalyzer.CA1829);

        /// <summary>
        /// Computes one or more fixes for the specified <see cref="CodeFixContext" />.
        /// </summary>
        /// <param name="context">A <see cref="CodeFixContext" /> containing context information about the diagnostics to fix.
        /// The context must only contain diagnostics with a <see cref="Diagnostic.Id" /> included in the <see cref="CodeFixProvider.FixableDiagnosticIds" /> 
        /// for the current provider.</param>
        /// <returns>A <see cref="Task" /> that represents the asynchronous operation.</returns>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is object &&
                context.Diagnostics[0].Properties.TryGetValue(UseCountProperlyAnalyzer.PropertyNameKey, out var propertyName) &&
                propertyName is object &&
                TryGetExpression(node, out _, out _))
            {
                RegisterCodeFix(context,
                    MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableTitle,
                    MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableTitle);
            }
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (node is null ||
                !diagnostic.Properties.TryGetValue(UseCountProperlyAnalyzer.PropertyNameKey, out var propertyName) ||
                propertyName is null)
            {
                return Task.CompletedTask;
            }

            // Everything but the Count identifier is carried over from the receiver, which can hold
            // another Count() call, so the member access is re-read from the node as the editor has
            // rewritten it.
            editor.ReplaceNode(node, (currentNode, generator) =>
            {
                if (!TryGetExpression(currentNode, out var memberAccessNode, out var nameNode))
                {
                    return currentNode;
                }

                return generator.ReplaceNode(memberAccessNode, nameNode, generator.IdentifierName(propertyName))
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithTriviaFrom(currentNode);
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the expression from the specified <paramref name="invocationNode" /> where to replace the invocation of the
        /// <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})" /> method with a property invocation.
        /// </summary>
        /// <param name="invocationNode">The invocation node to get a fixer for.</param>
        /// <param name="memberAccessNode">The member access node for the invocation node.</param>
        /// <param name="nameNode">The name node for the invocation node.</param>
        /// <returns><see langword="true"/> if a <paramref name="memberAccessNode" /> and <paramref name="nameNode"/> were found;
        /// <see langword="false" /> otherwise.</returns>
        protected abstract bool TryGetExpression(
            SyntaxNode invocationNode,
            [NotNullWhen(returnValue: true)] out SyntaxNode? memberAccessNode,
            [NotNullWhen(returnValue: true)] out SyntaxNode? nameNode);
    }
}
