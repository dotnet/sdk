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
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2200: Rethrow to preserve stack details
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic, Name = RethrowToPreserveStackDetailsAnalyzer.RuleId), Shared]
    public sealed class RethrowToPreserveStackDetailsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(RethrowToPreserveStackDetailsAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span) == null)
            {
                return;
            }

            RegisterCodeFix(
                context,
                MicrosoftCodeQualityAnalyzersResources.RethrowToPreserveStackDetailsTitle,
                nameof(RethrowToPreserveStackDetailsFixer));
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var nodeToReplace = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            if (nodeToReplace == null)
            {
                return Task.CompletedTask;
            }

            var rethrow = editor.Generator.ThrowStatement()
                .WithLeadingTrivia(nodeToReplace.GetLeadingTrivia())
                .WithTrailingTrivia(nodeToReplace.GetTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            // The replacement is a bare rethrow, so it carries nothing over from the statement it replaces
            // and a nested diagnostic cannot survive into it.
            editor.ReplaceNode(nodeToReplace, rethrow);
            return Task.CompletedTask;
        }
    }
}
