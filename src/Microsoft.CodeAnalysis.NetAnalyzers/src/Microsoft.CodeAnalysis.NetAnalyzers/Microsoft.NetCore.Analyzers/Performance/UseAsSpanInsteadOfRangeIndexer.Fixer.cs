// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

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

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);

            if (node is null)
            {
                return;
            }

            // The rules are mutually exclusive, so there can't be more than one for the same span:
            var diagnostic = context.Diagnostics.FirstOrDefault();
            var targetMethod = diagnostic.Properties.GetValueOrDefault(UseAsSpanInsteadOfRangeIndexerAnalyzer.TargetMethodName);

            if (targetMethod == null)
            {
                return;
            }

            if (TrySplitExpression(node, out var toReplace, out var target, out var arguments))
            {
                context.RegisterCodeFix(
                    new UseAsSpanInsteadOfRangeIndexerCodeAction(
                        diagnostic.Id,
                        targetMethod,
                        context.Document,
                        toReplace,
                        target,
                        arguments),
                    diagnostic);
            }
        }

        protected abstract bool TrySplitExpression(
            SyntaxNode node,
            out SyntaxNode toReplace,
            [NotNullWhen(true)] out SyntaxNode? target,
            [NotNullWhen(true)] out IEnumerable<SyntaxNode>? arguments);

        private class UseAsSpanInsteadOfRangeIndexerCodeAction : CodeAction
        {
            private readonly string _targetMethod;
            private readonly Document _document;
            private readonly SyntaxNode _toReplace;
            private readonly SyntaxNode _methodTarget;
            private readonly IEnumerable<SyntaxNode> _rangeArguments;

            public override string Title { get; }

            public override string EquivalenceKey { get; }

            public UseAsSpanInsteadOfRangeIndexerCodeAction(
                string ruleId,
                string targetMethod,
                Document document,
                SyntaxNode toReplace,
                SyntaxNode methodTarget,
                IEnumerable<SyntaxNode> rangeArguments)
            {
                _targetMethod = targetMethod;
                _document = document;
                _toReplace = toReplace;
                _methodTarget = methodTarget;
                _rangeArguments = rangeArguments;
                EquivalenceKey = ruleId;
                Title = ruleId.Equals(UseAsSpanInsteadOfRangeIndexerAnalyzer.StringRuleId, StringComparison.InvariantCulture) ?
                    string.Format(CultureInfo.InvariantCulture, MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfRangeIndexerOnAStringCodeFixTitle, targetMethod) :
                    string.Format(CultureInfo.InvariantCulture, MicrosoftNetCoreAnalyzersResources.UseAsSpanInsteadOfRangeIndexerOnAnArrayCodeFixTitle, targetMethod);
            }

            protected override async Task<Document> GetChangedDocumentAsync(CancellationToken cancellationToken)
            {
                var editor = await DocumentEditor.CreateAsync(_document, cancellationToken).ConfigureAwait(false);

                // target.AsSpan()
                var asSpan = editor.Generator.InvocationExpression(
                    editor.Generator.MemberAccessExpression(_methodTarget, _targetMethod));

                // target.AsSpan()[args]
                var indexed = editor.Generator.ElementAccessExpression(asSpan, _rangeArguments);

                editor.ReplaceNode(_toReplace, indexed);
                return editor.GetChangedDocument();
            }
        }
    }
}
