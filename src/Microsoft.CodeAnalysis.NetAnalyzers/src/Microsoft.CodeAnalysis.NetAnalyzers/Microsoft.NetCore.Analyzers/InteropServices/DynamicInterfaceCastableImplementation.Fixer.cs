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

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public abstract class DynamicInterfaceCastableImplementationFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(
                DynamicInterfaceCastableImplementationAnalyzer.InterfaceMembersMissingImplementationRuleId,
                DynamicInterfaceCastableImplementationAnalyzer.MembersDeclaredOnImplementationTypeMustBeStaticRuleId);

        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(context => context.CodeActionEquivalenceKey, ApplyFixAsync);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span, getInnermostNodeForTie: true);
            SyntaxNode declaration = generator.GetDeclaration(enclosingNode);
            if (declaration == null || !CodeFixSupportsDeclaration(declaration))
            {
                return;
            }

            foreach (Diagnostic diagnostic in context.Diagnostics)
            {
                if (GetEquivalenceKey(diagnostic) is not string equivalenceKey)
                {
                    continue;
                }

                ImmutableArray<Diagnostic> diagnostics = ImmutableArray.Create(diagnostic);
                Document document = context.Document;

                context.RegisterCodeFix(
                    CodeAction.Create(
                        GetTitle(equivalenceKey),
                        cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (doc, diag, editor, token) => ApplyFixAsync(doc, diag, editor, equivalenceKey, token),
                            cancellationToken),
                        equivalenceKey),
                    diagnostic);
            }
        }

        private static string? GetEquivalenceKey(Diagnostic diagnostic)
        {
            if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.InterfaceMembersMissingImplementationRuleId)
            {
                return nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation);
            }

            if (diagnostic.Id == DynamicInterfaceCastableImplementationAnalyzer.MembersDeclaredOnImplementationTypeMustBeStaticRuleId
                && diagnostic.Properties.ContainsKey(DynamicInterfaceCastableImplementationAnalyzer.NonStaticMemberIsMethodKey))
            {
                return nameof(MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStatic);
            }

            return null;
        }

        private static string GetTitle(string equivalenceKey)
            => equivalenceKey == nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation)
                ? MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation
                : MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStatic;

        private async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            if (GetEquivalenceKey(diagnostic) is not string key
                || (equivalenceKey is not null && key != equivalenceKey))
            {
                return;
            }

            SyntaxNode enclosingNode = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            SyntaxNode declaration = SyntaxGenerator.GetGenerator(document).GetDeclaration(enclosingNode);
            if (declaration == null || !CodeFixSupportsDeclaration(declaration))
            {
                return;
            }

            if (key == nameof(MicrosoftNetCoreAnalyzersResources.ImplementInterfacesOnDynamicCastableImplementation))
            {
                await ImplementInterfacesOnDynamicCastableImplementationAsync(declaration, document, editor, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await MakeMemberDeclaredOnImplementationTypeStaticAsync(declaration, document, editor, cancellationToken).ConfigureAwait(false);
            }
        }

        protected static SyntaxAnnotation CreatePossibleInvalidCodeWarning()
        {
            return WarningAnnotation.Create(MicrosoftNetCoreAnalyzersResources.MakeMethodDeclaredOnImplementationTypeStaticMayProduceInvalidCode);
        }

        protected abstract bool CodeFixSupportsDeclaration(SyntaxNode declaration);

        protected abstract Task ImplementInterfacesOnDynamicCastableImplementationAsync(
            SyntaxNode declaration,
            Document document,
            SyntaxEditor editor,
            CancellationToken cancellationToken);

        protected abstract Task MakeMemberDeclaredOnImplementationTypeStaticAsync(
            SyntaxNode declaration,
            Document document,
            SyntaxEditor editor,
            CancellationToken cancellationToken);
    }
}
