﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1012: Abstract classes should not have public constructors
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class AbstractTypesShouldNotHaveConstructorsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(AbstractTypesShouldNotHaveConstructorsAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode node = root.FindNode(context.Span);

            string title = MicrosoftCodeQualityAnalyzersResources.AbstractTypesShouldNotHavePublicConstructorsCodeFix;
            context.RegisterCodeFix(CodeAction.Create(title,
                                        async ct => await ChangeAccessibilityCodeFixAsync(context.Document, root, node, ct).ConfigureAwait(false),
                                        equivalenceKey: title),
                                    context.Diagnostics);
        }

        private static SyntaxNode? GetDeclaration(ISymbol symbol, CancellationToken cancellationToken)
        {
            return (!symbol.DeclaringSyntaxReferences.IsEmpty) ? symbol.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) : null;
        }

        private static async Task<Document> ChangeAccessibilityCodeFixAsync(Document document, SyntaxNode root, SyntaxNode nodeToFix, CancellationToken cancellationToken)
        {
            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var classSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(nodeToFix, cancellationToken)!;
            List<SyntaxNode> instanceConstructors = classSymbol.InstanceConstructors.Where(t => t.DeclaredAccessibility == Accessibility.Public).Select(t => GetDeclaration(t, cancellationToken)).WhereNotNull().ToList();
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
            SyntaxNode newRoot = root.ReplaceNodes(instanceConstructors, (original, rewritten) => generator.WithAccessibility(original, Accessibility.Protected));
            return document.WithSyntaxRoot(newRoot);
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
