// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1507: Use nameof to express symbol names
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseNameOfInPlaceOfStringFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(UseNameofInPlaceOfStringAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers'
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostics = context.Diagnostics;
            var diagnosticSpan = context.Span;

            // getInnerModeNodeForTie = true so we are replacing the string literal node and not the whole argument node
            var nodeToReplace = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
            if (nodeToReplace == null)
            {
                return;
            }

            var stringText = nodeToReplace.FindToken(diagnosticSpan.Start).ValueText;
            context.RegisterCodeFix(CodeAction.Create(
                    MicrosoftCodeQualityAnalyzersResources.UseNameOfInPlaceOfStringTitle,
                    c => ReplaceWithNameOf(context.Document, nodeToReplace, stringText, c),
                    equivalenceKey: nameof(UseNameOfInPlaceOfStringFixer)),
                context.Diagnostics);
        }

        private static async Task<Document> ReplaceWithNameOf(Document document, SyntaxNode nodeToReplace,
            string stringText, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            var trailingTrivia = nodeToReplace.GetTrailingTrivia();
            var leadingTrivia = nodeToReplace.GetLeadingTrivia();
            var nameOfExpression = generator.NameOfExpression(generator.IdentifierName(stringText))
                .WithTrailingTrivia(trailingTrivia)
                .WithLeadingTrivia(leadingTrivia);

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(nodeToReplace, nameOfExpression);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}