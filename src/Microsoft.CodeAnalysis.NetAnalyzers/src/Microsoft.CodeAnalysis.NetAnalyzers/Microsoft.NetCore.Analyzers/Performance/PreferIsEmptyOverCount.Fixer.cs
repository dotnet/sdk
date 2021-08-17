// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1836: Prefer IsEmpty over Count when available.
    /// </summary>
    public abstract class PreferIsEmptyOverCountFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseCountProperlyAnalyzer.CA1836);

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span, getInnermostNodeForTie: true);
            if (node == null)
            {
                return;
            }

            ImmutableDictionary<string, string> properties = context.Diagnostics[0].Properties;
            if (properties == null)
            {
                return;
            }

            // Indicates whether the Count method or property is on the Right or Left side of a binary expression 
            // OR if it is the argument or the instance of an Equals invocation.
            string operationKey = properties[UseCountProperlyAnalyzer.OperationKey];

            // Indicates if the replacing IsEmpty node should be negated. (!IsEmpty). 
            bool shouldNegate = properties.ContainsKey(UseCountProperlyAnalyzer.ShouldNegateKey);

            context.RegisterCodeFix(CodeAction.Create(
                title: MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountTitle,
                createChangedDocument: async cancellationToken =>
                {
                    DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, cancellationToken).ConfigureAwait(false);
                    SyntaxGenerator generator = editor.Generator;

                    // The object that the Count property belongs to OR null if countAccessor is not a MemberAccessExpressionSyntax.
                    SyntaxNode? objectExpression = GetObjectExpressionFromOperation(node, operationKey);

                    // The IsEmpty property meant to replace the binary expression.
                    SyntaxNode isEmptyNode = objectExpression is null ?
                        generator.IdentifierName(UseCountProperlyAnalyzer.IsEmpty) :
                        generator.MemberAccessExpression(objectExpression, UseCountProperlyAnalyzer.IsEmpty);

                    if (shouldNegate)
                    {
                        isEmptyNode = generator.LogicalNotExpression(isEmptyNode);
                    }

                    editor.ReplaceNode(node, isEmptyNode.WithTriviaFrom(node));
                    return editor.GetChangedDocument();
                },
                equivalenceKey: MicrosoftNetCoreAnalyzersResources.PreferIsEmptyOverCountMessage),
            context.Diagnostics);
        }

        /// <summary>
        /// The object that the Count method or property belongs to OR null if the Count method or property is not a MemberAccessExpressionSyntax.
        /// </summary>
        protected abstract SyntaxNode? GetObjectExpressionFromOperation(SyntaxNode node, string operationKey);
    }
}
