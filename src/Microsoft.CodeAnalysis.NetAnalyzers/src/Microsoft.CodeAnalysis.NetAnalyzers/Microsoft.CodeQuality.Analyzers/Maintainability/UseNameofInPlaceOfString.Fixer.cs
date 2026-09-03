// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1507: Use nameof to express symbol names
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class UseNameOfInPlaceOfStringFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(UseNameofInPlaceOfStringAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // getInnerModeNodeForTie = true so we are replacing the string literal node and not the whole argument node
            if (root.FindNode(context.Span, getInnermostNodeForTie: true) == null)
            {
                return;
            }

            RegisterCodeFix(
                context,
                MicrosoftCodeQualityAnalyzersResources.UseNameOfInPlaceOfStringTitle,
                nameof(UseNameOfInPlaceOfStringFixer));
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var nodeToReplace = editor.OriginalRoot.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
            if (nodeToReplace == null)
            {
                return Task.CompletedTask;
            }

            var stringText = nodeToReplace.FindToken(diagnosticSpan.Start).ValueText;

            var trailingTrivia = nodeToReplace.GetTrailingTrivia();
            var leadingTrivia = nodeToReplace.GetLeadingTrivia();
            var nameOfExpression = editor.Generator.NameOfExpression(editor.Generator.IdentifierName(stringText))
                .WithTrailingTrivia(trailingTrivia)
                .WithLeadingTrivia(leadingTrivia);

            // A string literal has only tokens beneath it, so no diagnostic can nest inside another one here
            // and the replacement carries nothing over from the node it replaces.
            editor.ReplaceNode(nodeToReplace, nameOfExpression);
            return Task.CompletedTask;
        }
    }
}