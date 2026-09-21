// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1514: <inheritdoc cref="MicrosoftCodeQualityAnalyzersResources.AvoidLengthCalculationWhenSlicingToEndTitle"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AvoidLengthCalculationWhenSlicingToEndFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AvoidLengthCalculationWhenSlicingToEndAnalyzer.RuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetRequiredSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            if (GetLengthArgument(root, semanticModel, context.Span, context.CancellationToken) is null)
            {
                return;
            }

            RegisterCodeFix(
                context,
                MicrosoftCodeQualityAnalyzersResources.AvoidLengthCalculationWhenSlicingToEndCodeFixTitle,
                nameof(MicrosoftCodeQualityAnalyzersResources.AvoidLengthCalculationWhenSlicingToEndCodeFixTitle));
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // Dropping the length argument is the whole fix, so the start argument keeps whatever
            // form the user wrote -- including a name: prefix -- without rebuilding the invocation.
            if (GetLengthArgument(editor.OriginalRoot, semanticModel, diagnostic.Location.SourceSpan, cancellationToken) is SyntaxNode lengthArgument)
            {
                editor.RemoveNode(lengthArgument);
            }
        }

        private static SyntaxNode? GetLengthArgument(SyntaxNode root, SemanticModel semanticModel, TextSpan span, CancellationToken cancellationToken)
        {
            var node = root.FindNode(span, getInnermostNodeForTie: true);
            if (node is null)
            {
                return null;
            }

            return semanticModel.GetOperation(node, cancellationToken) is IInvocationOperation { Instance: not null, Arguments.Length: 2 } invocation
                ? invocation.Arguments.GetArgumentForParameterAtIndex(1).Syntax
                : null;
        }
    }
}
