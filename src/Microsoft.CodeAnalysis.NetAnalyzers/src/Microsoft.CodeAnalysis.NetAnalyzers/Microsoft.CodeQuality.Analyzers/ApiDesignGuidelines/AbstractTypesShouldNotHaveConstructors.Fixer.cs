// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1012: Abstract classes should not have public constructors
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AbstractTypesShouldNotHaveConstructorsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AbstractTypesShouldNotHaveConstructorsAnalyzer.RuleId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.AbstractTypesShouldNotHavePublicConstructorsCodeFix;
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        private static SyntaxNode? GetDeclaration(ISymbol symbol, SyntaxTree tree, CancellationToken cancellationToken)
        {
            SyntaxReference? reference = symbol.DeclaringSyntaxReferences.FirstOrDefault(r => r.SyntaxTree == tree);
            return reference?.GetSyntax(cancellationToken);
        }

        protected sealed override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode nodeToFix = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (model.GetDeclaredSymbol(nodeToFix, cancellationToken) is not INamedTypeSymbol classSymbol)
            {
                return;
            }

            // A partial class can declare constructors in another document, which this editor cannot reach.
            SyntaxTree tree = editor.OriginalRoot.SyntaxTree;
            foreach (SyntaxNode constructor in classSymbol.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .Select(c => GetDeclaration(c, tree, cancellationToken))
                .WhereNotNull())
            {
                editor.SetAccessibility(constructor, Accessibility.Protected);
            }
        }
    }
}
