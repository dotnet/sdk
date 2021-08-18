// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1725: Parameter names should match base declaration
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    [Shared]
    public sealed class ParameterNamesShouldMatchBaseDeclarationFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(ParameterNamesShouldMatchBaseDeclarationAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            SemanticModel semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                SyntaxNode node = syntaxRoot.FindNode(context.Span);
                ISymbol declaredSymbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);

                if (declaredSymbol.Kind != SymbolKind.Parameter)
                {
                    continue;
                }

                string newName = diagnostic.Properties[ParameterNamesShouldMatchBaseDeclarationAnalyzer.NewNamePropertyName];
                context.RegisterCodeFix(
                    CodeAction.Create(
                        string.Format(CultureInfo.CurrentCulture, MicrosoftCodeQualityAnalyzersResources.RenameToTitle, newName),
                        cancellationToken => GetUpdatedDocumentForParameterRenameAsync(context.Document, declaredSymbol, newName, cancellationToken),
                        nameof(ParameterNamesShouldMatchBaseDeclarationFixer)),
                    diagnostic);
            }
        }

        private static async Task<Document> GetUpdatedDocumentForParameterRenameAsync(Document document, ISymbol parameter, string newName, CancellationToken cancellationToken)
        {
            Solution newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, parameter, newName, null, cancellationToken).ConfigureAwait(false);
            return newSolution.GetDocument(document.Id)!;
        }
    }
}