// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2200: Rethrow to preserve stack details
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = RethrowToPreserveStackDetailsAnalyzer.RuleId), Shared]
    public sealed class RethrowToPreserveStackDetailsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RethrowToPreserveStackDetailsAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers'
            return WellKnownFixAllProviders.BatchFixer;
        }
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostics = context.Diagnostics;

            var nodeToReplace = root.FindNode(context.Span);
            if (nodeToReplace == null)
            {
                return;
            }
            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MicrosoftCodeQualityAnalyzersResources.RethrowToPreserveStackDetailsTitle,
                    createChangedDocument: c => MakeThrowAsync(context.Document, nodeToReplace, c),
                    equivalenceKey: nameof(RethrowToPreserveStackDetailsFixer)),
                diagnostics);
        }

        private static async Task<Document> MakeThrowAsync(Document document, SyntaxNode nodeToReplace, CancellationToken cancellationToken)
        {
            var formattednewLocal = SyntaxGenerator.GetGenerator(document).ThrowStatement()
                .WithLeadingTrivia(nodeToReplace.GetLeadingTrivia())
                .WithTrailingTrivia(nodeToReplace.GetTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot.ReplaceNode(nodeToReplace, formattednewLocal);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
