﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2007: Do not directly await a Task in libraries.
    ///     1. Append ConfigureAwait(false) to the task.
    ///     2. Append ConfigureAwait(true) to the task.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class DoNotDirectlyAwaitATaskFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DoNotDirectlyAwaitATaskAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode expression = root.FindNode(context.Span);

            if (expression != null)
            {
                string title = MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitFalse;
                context.RegisterCodeFix(
                    CodeAction.Create(title,
                        async ct => await GetFixAsync(context.Document, expression, argument: false, cancellationToken: ct).ConfigureAwait(false),
                        equivalenceKey: nameof(AppendConfigureAwaitFalse)),
                    context.Diagnostics);

                title = MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitTrue;
                context.RegisterCodeFix(
                    CodeAction.Create(title,
                        async ct => await GetFixAsync(context.Document, expression, argument: true, cancellationToken: ct).ConfigureAwait(false),
                        equivalenceKey: nameof(AppendConfigureAwaitTrue)),
                    context.Diagnostics);
            }
        }

        private static async Task<Document> GetFixAsync(Document document, SyntaxNode expression, bool argument, CancellationToken cancellationToken)
        {
            // Rewrite the expression to include a .ConfigureAwait() after it. We reattach trailing trivia to the end.
            // This is especially important for VB, as the end-of-line may be in the trivia
            DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            FixDiagnostic(editor, expression, argument);
            return editor.GetChangedDocument();
        }

        private static void FixDiagnostic(DocumentEditor editor, SyntaxNode expression, bool argument)
        {
            editor.ReplaceNode(
                expression,
                (expression, generator) =>
                {
                    SyntaxNode memberAccess = generator.MemberAccessExpression(expression.WithoutTrailingTrivia(), "ConfigureAwait");
                    SyntaxNode argumentLiteral = argument ? generator.TrueLiteralExpression() : generator.FalseLiteralExpression();
                    SyntaxNode invocation = generator.InvocationExpression(memberAccess, argumentLiteral);
                    return invocation.WithLeadingTrivia(expression.GetLeadingTrivia()).WithTrailingTrivia(expression.GetTrailingTrivia());
                });
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return CustomFixAllProvider.Instance;
        }

        private sealed class CustomFixAllProvider : DocumentBasedFixAllProvider
        {
            public static readonly CustomFixAllProvider Instance = new();

            protected override string GetFixAllTitle(FixAllContext fixAllContext)
                => MicrosoftCodeQualityAnalyzersResources.AppendConfigureAwaitFalse;

            protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                var useConfigureAwaitTrue = fixAllContext.CodeActionEquivalenceKey == nameof(AppendConfigureAwaitTrue);
                var editor = await DocumentEditor.CreateAsync(document, fixAllContext.CancellationToken).ConfigureAwait(false);
                foreach (var diagnostic in diagnostics)
                {
                    SyntaxNode expression = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
                    FixDiagnostic(editor, expression, argument: useConfigureAwaitTrue);
                }

                return editor.GetChangedDocument();
            }
        }
    }
}
