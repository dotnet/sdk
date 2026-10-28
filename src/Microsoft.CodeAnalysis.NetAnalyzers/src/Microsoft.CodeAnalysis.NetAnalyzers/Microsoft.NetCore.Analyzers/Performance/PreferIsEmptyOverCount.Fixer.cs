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
    /// <summary>
    /// CA1836: Prefer IsEmpty over Count when available.
    /// </summary>
    public abstract class PreferIsEmptyOverCountFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseCountProperlyAnalyzer.CA1836);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (node == null)
            {
                return;
            }

            if (context.Diagnostics[0].Properties is null)
            {
                return;
            }

            RegisterCodeFix(context,
                MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountTitle,
                MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountMessage);
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            ImmutableDictionary<string, string?> properties = diagnostic.Properties;
            if (properties is null)
            {
                return Task.CompletedTask;
            }

            // Indicates whether the Count method or property is on the Right or Left side of a binary expression 
            // OR if it is the argument or the instance of an Equals invocation.
            string operationKey = properties[UseCountProperlyAnalyzer.OperationKey]!;

            // Indicates if the replacing IsEmpty node should be negated. (!IsEmpty). 
            bool shouldNegate = properties.ContainsKey(UseCountProperlyAnalyzer.ShouldNegateKey);

            // The object the Count belongs to is a descendant of the diagnosed node and can hold another
            // diagnosed comparison, so it is re-read from the node as the editor has rewritten it rather
            // than from the original tree.
            editor.ReplaceNode(node, (currentNode, generator) =>
            {
                // The object that the Count property belongs to OR null if countAccessor is not a MemberAccessExpressionSyntax.
                SyntaxNode? objectExpression = GetObjectExpressionFromOperation(currentNode, operationKey);

                // The IsEmpty property meant to replace the binary expression.
                SyntaxNode isEmptyNode = objectExpression is null ?
                    generator.IdentifierName(UseCountProperlyAnalyzer.IsEmpty) :
                    generator.MemberAccessExpression(objectExpression, UseCountProperlyAnalyzer.IsEmpty);

                if (shouldNegate)
                {
                    isEmptyNode = generator.LogicalNotExpression(isEmptyNode);
                }

                return isEmptyNode.WithTriviaFrom(currentNode);
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// The object that the Count method or property belongs to OR null if the Count method or property is not a MemberAccessExpressionSyntax.
        /// </summary>
        protected abstract SyntaxNode? GetObjectExpressionFromOperation(SyntaxNode node, string operationKey);
    }
}
