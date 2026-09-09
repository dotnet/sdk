// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1831, CA1832, CA1833: Use AsSpan or AsMemory instead of Range-based indexers when appropriate.
    /// </summary>
    public abstract class UseAsSpanInsteadOfRangeIndexerFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(
                UseAsSpanInsteadOfRangeIndexerAnalyzer.StringRuleId,
                UseAsSpanInsteadOfRangeIndexerAnalyzer.ArrayReadOnlyRuleId,
                UseAsSpanInsteadOfRangeIndexerAnalyzer.ArrayReadWriteRuleId);

        // The action is keyed by rule ID, and one document can carry diagnostics from more than one of
        // the three rules, so a fix-all has to apply only the one that was invoked.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                (document, diagnostic, editor, equivalenceKey, cancellationToken) =>
                {
                    if (equivalenceKey is null || diagnostic.Id == equivalenceKey)
                    {
                        ApplyFix(diagnostic, editor);
                    }

                    return Task.CompletedTask;
                });

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            if (node is null)
            {
                return;
            }

            // The rules are mutually exclusive, so there can't be more than one for the same span:
            var diagnostic = context.Diagnostics.First();
            var targetMethod = diagnostic.Properties.GetValueOrDefault(UseAsSpanInsteadOfRangeIndexerAnalyzer.TargetMethodName);

            if (targetMethod == null)
            {
                return;
            }

            if (TrySplitExpression(node, out _, out _, out _))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: GetTitle(diagnostic.Id, targetMethod),
                        createChangedDocument: async cancellationToken =>
                        {
                            var editor = await DocumentEditor.CreateAsync(context.Document, cancellationToken).ConfigureAwait(false);
                            ApplyFix(diagnostic, editor);
                            return editor.GetChangedDocument();
                        },
                        equivalenceKey: diagnostic.Id),
                    diagnostic);
            }
        }

        private void ApplyFix(Diagnostic diagnostic, SyntaxEditor editor)
        {
            var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            var targetMethod = diagnostic.Properties.GetValueOrDefault(UseAsSpanInsteadOfRangeIndexerAnalyzer.TargetMethodName);

            if (node is null || targetMethod is null || !TrySplitExpression(node, out var toReplace, out _, out _))
            {
                return;
            }

            // Both the receiver and the range arguments are carried over from inside the expression and
            // either can hold another range indexer, so they are re-read from the node as the editor has
            // rewritten it.
            editor.ReplaceNode(toReplace, (currentNode, generator) =>
            {
                if (!TrySplitExpression(currentNode, out _, out var target, out var arguments))
                {
                    return currentNode;
                }

                // target.AsSpan()
                var asSpan = generator.InvocationExpression(generator.MemberAccessExpression(target, targetMethod));

                // target.AsSpan()[args]
                return generator.ElementAccessExpression(asSpan, arguments);
            });
        }

        private static string GetTitle(string ruleId, string targetMethod)
            => ruleId.Equals(UseAsSpanInsteadOfRangeIndexerAnalyzer.StringRuleId, StringComparison.InvariantCulture) ?
                string.Format(CultureInfo.InvariantCulture, MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfRangeIndexerOnAStringCodeFixTitle, targetMethod) :
                string.Format(CultureInfo.InvariantCulture, MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfRangeIndexerOnAnArrayCodeFixTitle, targetMethod);

        protected abstract bool TrySplitExpression(
            SyntaxNode node,
            out SyntaxNode toReplace,
            [NotNullWhen(true)] out SyntaxNode? target,
            [NotNullWhen(true)] out IEnumerable<SyntaxNode>? arguments);
    }
}
