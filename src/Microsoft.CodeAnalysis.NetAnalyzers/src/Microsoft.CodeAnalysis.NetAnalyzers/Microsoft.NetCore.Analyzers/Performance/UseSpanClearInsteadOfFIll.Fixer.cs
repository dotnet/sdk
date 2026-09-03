// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Performance
{
    /// <summary>
    /// CA1855: Use Span.Clear instead of Span.Fill(default)
    /// </summary>
    public abstract class UseSpanClearInsteadOfFillFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(UseSpanClearInsteadOfFillAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span, getInnermostNodeForTie: true);

            var invocationTarget = GetInvocationTarget(node);
            if (invocationTarget == null)
            {
                return;
            }

            var diagnostic = context.Diagnostics[0];
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: MicrosoftNetCoreAnalyzersResources.UseSpanClearInsteadOfFillCodeFixTitle,
                    createChangedDocument: async cancellationToken =>
                    {
                        DocumentEditor editor = await DocumentEditor.CreateAsync(context.Document, cancellationToken).ConfigureAwait(false);
                        SyntaxGenerator generator = editor.Generator;

                        var memberAccess = generator.MemberAccessExpression(invocationTarget, UseSpanClearInsteadOfFillAnalyzer.ClearMethod);
                        var invocation = generator.InvocationExpression(memberAccess);

                        editor.ReplaceNode(node, invocation);
                        return editor.GetChangedDocument();
                    },
                    equivalenceKey: nameof(MicrosoftNetCoreAnalyzersResources.UseSpanClearInsteadOfFillCodeFixTitle)),
                diagnostic);
        }

        protected abstract SyntaxNode? GetInvocationTarget(SyntaxNode node);
    }
}