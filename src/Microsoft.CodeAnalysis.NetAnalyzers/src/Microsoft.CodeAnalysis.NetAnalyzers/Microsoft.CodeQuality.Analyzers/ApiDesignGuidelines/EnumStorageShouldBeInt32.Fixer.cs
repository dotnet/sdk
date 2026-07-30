// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1028: Enum Storage should be Int32
    /// </summary>
    public abstract class EnumStorageShouldBeInt32Fixer : SyntaxEditorBasedCodeFixProvider
    {
        protected abstract SyntaxNode? GetTargetNode(SyntaxNode node);

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(EnumStorageShouldBeInt32Analyzer.RuleId);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.EnumStorageShouldBeInt32Title;
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        protected sealed override Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            SyntaxNode enumDeclarationNode = editor.Generator.GetDeclaration(node, DeclarationKind.Enum);

            // Find the target syntax node to replace. Was not able to find a language neutral way of doing this. So using the language specific methods
            SyntaxNode? targetNode = GetTargetNode(enumDeclarationNode);
            if (targetNode != null)
            {
                editor.RemoveNode(targetNode, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia | SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.KeepEndOfLine);
            }

            return Task.CompletedTask;
        }
    }
}
