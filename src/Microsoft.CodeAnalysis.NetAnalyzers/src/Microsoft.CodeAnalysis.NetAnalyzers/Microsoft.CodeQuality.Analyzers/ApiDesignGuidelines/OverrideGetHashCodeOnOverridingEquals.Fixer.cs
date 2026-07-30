// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2218: Override GetHashCode on overriding Equals
    /// </summary>
    public abstract class OverrideGetHashCodeOnOverridingEqualsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.OverrideGetHashCodeOnOverridingEqualsCodeActionTitle;
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode typeDeclaration = editor.Generator.GetDeclaration(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));
            if (typeDeclaration == null)
            {
                return;
            }

            // CONSIDER: Do we need to confirm that System.Object.GetHashCode isn't shadowed in a base type?

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            editor.AddMember(typeDeclaration, editor.Generator.DefaultGetHashCodeOverrideDeclaration(model.Compilation));
        }
    }
}