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
    /// CA1855: Use Span.Clear instead of Span.Fill(default)
    /// </summary>
    public abstract class UseSpanClearInsteadOfFillFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(UseSpanClearInsteadOfFillAnalyzer.DiagnosticId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            if (GetInvocationTarget(node) is null)
            {
                return;
            }

            RegisterCodeFix(context,
                MicrosoftNetCoreAnalyzersResources.UseSpanClearInsteadOfFillCodeFixTitle,
                nameof(MicrosoftNetCoreAnalyzersResources.UseSpanClearInsteadOfFillCodeFixTitle));
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var invocationTarget = GetInvocationTarget(node);
            if (invocationTarget is null)
            {
                return Task.CompletedTask;
            }

            // Span<T>.Fill returns void and the argument has to be a default value, so one diagnosed
            // call can never sit inside another and the target can be reused as it was written.
            SyntaxGenerator generator = editor.Generator;
            var memberAccess = generator.MemberAccessExpression(invocationTarget, UseSpanClearInsteadOfFillAnalyzer.ClearMethod);

            editor.ReplaceNode(node, generator.InvocationExpression(memberAccess));

            return Task.CompletedTask;
        }

        protected abstract SyntaxNode? GetInvocationTarget(SyntaxNode node);
    }
}