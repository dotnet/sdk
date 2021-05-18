// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1829: Use property instead of <see cref="Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/>, when available.
    /// Implements the <see cref="CodeFixProvider" />
    /// </summary>
    public abstract class UsePropertyInsteadOfCountMethodWhenAvailableFixer : CodeFixProvider
    {
        /// <summary>
        /// A list of diagnostic IDs that this provider can provider fixes for.
        /// </summary>
        /// <value>The fixable diagnostic ids.</value>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseCountProperlyAnalyzer.CA1829);

        /// <summary>
        /// Gets an optional <see cref="FixAllProvider" /> that can fix all/multiple occurrences of diagnostics fixed by this code fix provider.
        /// Return null if the provider doesn't support fix all/multiple occurrences.
        /// Otherwise, you can return any of the well known fix all providers from <see cref="WellKnownFixAllProviders" /> or implement your own fix all provider.
        /// </summary>
        /// <returns>FixAllProvider.</returns>
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        /// <summary>
        /// Computes one or more fixes for the specified <see cref="CodeFixContext" />.
        /// </summary>
        /// <param name="context">A <see cref="CodeFixContext" /> containing context information about the diagnostics to fix.
        /// The context must only contain diagnostics with a <see cref="Diagnostic.Id" /> included in the <see cref="CodeFixProvider.FixableDiagnosticIds" /> 
        /// for the current provider.</param>
        /// <returns>A <see cref="Task" /> that represents the asynchronous operation.</returns>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (node is object &&
                context.Diagnostics[0].Properties.TryGetValue(UseCountProperlyAnalyzer.PropertyNameKey, out var propertyName) &&
                propertyName is object &&
                TryGetExpression(node, out var expressionNode, out var nameNode))
            {
                context.RegisterCodeFix(
                    new UsePropertyInsteadOfCountMethodWhenAvailableCodeAction(context.Document, node, expressionNode, nameNode, propertyName),
                    context.Diagnostics);
            }
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

        /// <summary>
        /// Implements the <see cref="CodeAction"/> for replacing the use of <see cref="System.Linq.Enumerable.Count{TSource}(System.Collections.Generic.IEnumerable{TSource})"/> 
        /// for the use of a property of the receiving type.
        /// This class cannot be inherited.
        /// </summary>
        /// <seealso cref="Microsoft.CodeAnalysis.CodeActions.CodeAction" />
        private sealed class UsePropertyInsteadOfCountMethodWhenAvailableCodeAction : CodeAction
        {
            private readonly Document _document;
            private readonly SyntaxNode _invocationNode;
            private readonly SyntaxNode _memberAccessNode;
            private readonly SyntaxNode _nameNode;
            private readonly string _propertyName;

            public UsePropertyInsteadOfCountMethodWhenAvailableCodeAction(
                Document document,
                SyntaxNode invocationNode,
                SyntaxNode memberAccessNode,
                SyntaxNode nameNode,
                string propertyName)
            {
                this._document = document;
                this._invocationNode = invocationNode;
                this._memberAccessNode = memberAccessNode;
                this._nameNode = nameNode;
                this._propertyName = propertyName;
            }

            /// <inheritdoc/>
            public override string Title { get; } = MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableTitle;

            /// <inheritdoc/>
            public override string EquivalenceKey { get; } = MicrosoftNetCoreAnalyzersResources.UsePropertyInsteadOfCountMethodWhenAvailableTitle;

            /// <inheritdoc/>
            protected sealed override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(this._document, cancellationToken).ConfigureAwait(false);
                var generator = editor.Generator;
                var replacementSyntax = generator.ReplaceNode(this._memberAccessNode, this._nameNode, generator.IdentifierName(_propertyName))
                    .WithAdditionalAnnotations(Formatter.Annotation)
                    .WithTriviaFrom(this._invocationNode);

                editor.ReplaceNode(this._invocationNode, replacementSyntax);

                return editor.GetChangedDocument();
            }
        }
    }
}
