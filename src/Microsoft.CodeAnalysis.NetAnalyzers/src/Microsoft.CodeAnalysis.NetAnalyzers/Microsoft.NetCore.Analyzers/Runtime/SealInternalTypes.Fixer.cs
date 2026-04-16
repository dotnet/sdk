// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class SealInternalTypesFixer : CodeFixProvider
    {
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var codeAction = CodeAction.Create(
                MicrosoftNetCoreAnalyzersResources.SealInternalTypesCodeFixTitle,
                SealClassDeclarationsAsync,
                nameof(MicrosoftNetCoreAnalyzersResources.SealInternalTypesCodeFixTitle));
            context.RegisterCodeFix(codeAction, context.Diagnostics);
            return Task.CompletedTask;

            //  Local functions

            async Task<Solution> SealClassDeclarationsAsync(CancellationToken token)
            {
                var solutionEditor = new SolutionEditor(context.Document.Project.Solution);
                await SealDeclarationAt(solutionEditor, context.Diagnostics[0].Location, token).ConfigureAwait(false);

                foreach (var location in context.Diagnostics[0].AdditionalLocations)
                    await SealDeclarationAt(solutionEditor, location, token).ConfigureAwait(false);

                return solutionEditor.GetChangedSolution();
            }

            static async Task SealDeclarationAt(SolutionEditor solutionEditor, Location location, CancellationToken token)
            {
                var solution = solutionEditor.OriginalSolution;
                var document = solution.GetDocument(location.SourceTree);

                if (document is null)
                    return;

                var documentEditor = await solutionEditor.GetDocumentEditorAsync(document.Id, token).ConfigureAwait(false);
                var root = await document.GetRequiredSyntaxRootAsync(token).ConfigureAwait(false);
                var declaration = root.FindNode(location.SourceSpan);
                var newModifiers = documentEditor.Generator.GetModifiers(declaration).WithIsSealed(true);
                var newDeclaration = documentEditor.Generator.WithModifiers(declaration, newModifiers);
                documentEditor.ReplaceNode(declaration, newDeclaration);
            }
        }

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(SealInternalTypes.RuleId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
    }
}
