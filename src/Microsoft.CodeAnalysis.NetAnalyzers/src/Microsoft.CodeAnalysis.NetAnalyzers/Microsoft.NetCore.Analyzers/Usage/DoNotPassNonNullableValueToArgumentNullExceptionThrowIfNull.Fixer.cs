// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Usage
{
    public abstract class DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullFixer<TInvocationExpression> : CodeFixProvider
        where TInvocationExpression : SyntaxNode
    {
        protected const string HasValue = nameof(Nullable<int>.HasValue);
        protected const string ArgumentNullException = nameof(System.ArgumentNullException);

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NonNullableValueRuleId,
            DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NullableStructRuleId
        );

        // One title per rule, so a fix-all pass has to apply only the one the user invoked it from.
        public override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                (document, diagnostic, editor, equivalenceKey, cancellationToken) =>
                {
                    ApplyFix(diagnostic, editor, equivalenceKey);
                    return Task.CompletedTask;
                });

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root.FindNode(context.Span, getInnermostNodeForTie: true) is not TInvocationExpression { Parent: not null })
            {
                return;
            }

            Document document = context.Document;

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                if (GetTitle(diagnostic.Id) is not string title)
                {
                    continue;
                }

                ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(diagnostic);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (_, diagnostic, editor, _) =>
                            {
                                ApplyFix(diagnostic, editor, title);
                                return Task.CompletedTask;
                            },
                            cancellationToken),
                        equivalenceKey: title),
                    diagnostic);
            }
        }

        private void ApplyFix(Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey)
        {
            if (equivalenceKey is not null && equivalenceKey != GetTitle(diagnostic.Id))
            {
                return;
            }

            // Both fixes target the statement the call sits in, and a `ThrowIfNull` call returns void, so no
            // two of this rule's diagnostics can nest and the statement needs no re-reading.
            if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not TInvocationExpression { Parent: SyntaxNode statement } invocation)
            {
                return;
            }

            if (diagnostic.Id == DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NonNullableValueRuleId)
            {
                editor.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
            }
            else
            {
                ReplaceWithNullableStructCheck(invocation, statement, editor);
            }
        }

        private static string? GetTitle(string ruleId) => ruleId switch
        {
            DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NonNullableValueRuleId => MicrosoftNetCoreAnalyzersResources.DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNullCodeFixTitle,
            DoNotPassNonNullableValueToArgumentNullExceptionThrowIfNull.NullableStructRuleId => MicrosoftNetCoreAnalyzersResources.DoNotPassNullableStructToArgumentNullExceptionThrowIfNullCodeFixTitle,
            _ => null,
        };

        /// <summary>
        /// Replaces <paramref name="statement"/> — the statement <paramref name="invocation"/> sits in — with an
        /// explicit <c>HasValue</c> check that throws.
        /// </summary>
        protected abstract void ReplaceWithNullableStructCheck(TInvocationExpression invocation, SyntaxNode statement, SyntaxEditor editor);
    }
}