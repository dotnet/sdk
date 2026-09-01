// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
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
    /// CA1853: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.DoNotGuardDictionaryRemoveByContainsKeyTitle"/>
    /// CA1868: <inheritdoc cref="MicrosoftNetCoreAnalyzersResources.DoNotGuardSetAddOrRemoveByContainsTitle"/>
    /// </summary>
    public abstract class DoNotGuardCallFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            DoNotGuardCallAnalyzer.DoNotGuardDictionaryRemoveByContainsKeyRuleId,
            DoNotGuardCallAnalyzer.DoNotGuardSetAddOrRemoveByContainsRuleId);

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (TryGetGuardedCallInElse(root, context.Diagnostics[0]) is null)
            {
                return;
            }

            RegisterCodeFix(
                context,
                MicrosoftNetCoreAnalyzersResources.RemoveRedundantGuardCallCodeFixTitle,
                context.Diagnostics[0].Descriptor.Id);
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            if (TryGetGuardedCallInElse(editor.OriginalRoot, diagnostic) is not bool guardedCallInElse ||
                editor.OriginalRoot.FindNode(diagnostic.AdditionalLocations[0].SourceSpan) is not SyntaxNode conditionalSyntax)
            {
                return Task.CompletedTask;
            }

            // Both shapes of the fix re-emit statements taken from inside the conditional, so they have to be
            // read off the conditional as the fixes before this one left it rather than off the original tree.
            editor.ReplaceNode(
                conditionalSyntax,
                (currentConditional, generator) => ReplaceConditionWithChild(currentConditional, guardedCallInElse, generator));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Reports whether the guarded call sits in the conditional's <c>else</c> branch, or
        /// <see langword="null"/> when the shape is not one the fix handles.
        /// </summary>
        private bool? TryGetGuardedCallInElse(SyntaxNode root, Diagnostic diagnostic)
        {
            if (diagnostic.AdditionalLocations.Count < 2 ||
                root.FindNode(diagnostic.AdditionalLocations[0].SourceSpan) is not SyntaxNode conditionalSyntax ||
                root.FindNode(diagnostic.AdditionalLocations[1].SourceSpan) is not SyntaxNode childStatementSyntax ||
                !SyntaxSupportedByFixer(conditionalSyntax, childStatementSyntax))
            {
                return null;
            }

            return IsInElseBranch(childStatementSyntax);
        }

        protected abstract bool SyntaxSupportedByFixer(SyntaxNode conditionalSyntax, SyntaxNode childStatementSyntax);

        protected abstract bool IsInElseBranch(SyntaxNode childStatementSyntax);

        /// <summary>
        /// Rewrites <paramref name="currentConditional"/> into the guarded call alone, or - when the conditional
        /// has an <c>else</c> - into the other branch guarded by the negated call.
        /// </summary>
        protected abstract SyntaxNode ReplaceConditionWithChild(SyntaxNode currentConditional, bool guardedCallInElse, SyntaxGenerator generator);
    }
}
