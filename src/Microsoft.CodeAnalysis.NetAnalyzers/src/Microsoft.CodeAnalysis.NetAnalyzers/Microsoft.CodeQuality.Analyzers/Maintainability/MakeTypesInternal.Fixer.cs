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

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    public abstract class MakeTypesInternalFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            RegisterCodeFix(
                context,
                MicrosoftCodeQualityAnalyzersResources.MakeTypesInternalCodeFixTitle,
                MicrosoftCodeQualityAnalyzersResources.MakeTypesInternalCodeFixTitle);
            return Task.CompletedTask;
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            // Types nest, so an enclosing type's replacement has to be built from the node as already rewritten -
            // rebuilding it from the original would re-emit a nested type from its pre-fix form.
            editor.ReplaceNode(node, (currentNode, _) => MakeInternal(currentNode).WithTriviaFrom(currentNode));
            return Task.CompletedTask;
        }

        protected abstract SyntaxNode MakeInternal(SyntaxNode node);

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(MakeTypesInternal.RuleId);
    }
}