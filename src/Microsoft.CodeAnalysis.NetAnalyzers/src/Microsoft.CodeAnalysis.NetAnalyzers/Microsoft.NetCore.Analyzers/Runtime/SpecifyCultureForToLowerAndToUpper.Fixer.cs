// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    public abstract class SpecifyCultureForToLowerAndToUpperFixerBase : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(SpecifyCultureForToLowerAndToUpperAnalyzer.RuleId);

        // Two alternative fixes for the same diagnostic, so a fix-all pass has to apply only the one the
        // user invoked it from.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<string?>(
                static fixAllContext => fixAllContext.CodeActionEquivalenceKey,
                (document, diagnostic, editor, equivalenceKey, cancellationToken) => ApplyFixAsync(document, diagnostic, editor, equivalenceKey, cancellationToken));

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetRequiredSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (!ShouldFix(root.FindNode(context.Span)))
            {
                return;
            }

            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;

            RegisterCodeFix(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture, nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture));
            RegisterCodeFix(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion, nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion));

            void RegisterCodeFix(string title, string equivalenceKey)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        cancellationToken => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (document, diagnostic, editor, cancellationToken) => ApplyFixAsync(document, diagnostic, editor, equivalenceKey, cancellationToken),
                            cancellationToken),
                        equivalenceKey),
                    diagnostics);
            }
        }

        private async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, string? equivalenceKey, CancellationToken cancellationToken)
        {
            SyntaxNode node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);

            if (!ShouldFix(node))
            {
                return;
            }

            if (equivalenceKey is null or nameof(MicrosoftNetCoreAnalyzersResources.SpecifyCurrentCulture))
            {
                SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

                if (GetNodeToSpecifyCurrentCultureOn(node, model, cancellationToken) is SyntaxNode target)
                {
                    SyntaxNode argument = editor.Generator.Argument(CreateCurrentCultureMemberAccess(editor.Generator, model));

                    // `x.ToLower().ToLower()` diagnoses both calls, and the outer fix re-emits the inner one,
                    // so the receiver is read off the node as an inner fix has already rewritten it.
                    editor.ReplaceNode(target, (currentNode, generator) => SpecifyCurrentCulture(currentNode, argument, generator));
                }
            }

            if (equivalenceKey is null or nameof(MicrosoftNetCoreAnalyzersResources.UseInvariantVersion))
            {
                if (GetMemberAccessToMakeInvariant(node) is SyntaxNode memberAccess)
                {
                    editor.ReplaceNode(memberAccess, (currentNode, generator) => UseInvariantVersion(currentNode, generator));
                }
            }
        }

        protected static SyntaxNode CreateCurrentCultureMemberAccess(SyntaxGenerator generator, SemanticModel model)
        {
            var cultureInfoType = model.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemGlobalizationCultureInfo)!;
            return generator.MemberAccessExpression(
                generator.TypeExpressionForStaticMemberAccess(cultureInfoType),
                generator.IdentifierName("CurrentCulture"));
        }

        protected static string GetReplacementMethodName(string currentMethodName) => currentMethodName switch
        {
            SpecifyCultureForToLowerAndToUpperAnalyzer.ToLowerMethodName => "ToLowerInvariant",
            SpecifyCultureForToLowerAndToUpperAnalyzer.ToUpperMethodName => "ToUpperInvariant",
            _ => currentMethodName,
        };

        protected abstract bool ShouldFix(SyntaxNode node);

        /// <summary>
        /// Returns the node <see cref="SpecifyCurrentCulture"/> replaces, or <see langword="null"/> when
        /// <paramref name="node"/> is not a shape the fix handles.
        /// </summary>
        protected abstract SyntaxNode? GetNodeToSpecifyCurrentCultureOn(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken);

        /// <summary>
        /// Rewrites <paramref name="currentNode"/> — the node <see cref="GetNodeToSpecifyCurrentCultureOn"/>
        /// returned, as an inner fix has left it — to pass <paramref name="currentCultureArgument"/>.
        /// </summary>
        protected abstract SyntaxNode SpecifyCurrentCulture(SyntaxNode currentNode, SyntaxNode currentCultureArgument, SyntaxGenerator generator);

        /// <summary>
        /// Returns the member access <see cref="UseInvariantVersion"/> renames, or <see langword="null"/> when
        /// <paramref name="node"/> is not a shape the fix handles.
        /// </summary>
        protected abstract SyntaxNode? GetMemberAccessToMakeInvariant(SyntaxNode node);

        /// <summary>
        /// Renames the method on <paramref name="currentMemberAccess"/> to its invariant counterpart.
        /// </summary>
        protected abstract SyntaxNode UseInvariantVersion(SyntaxNode currentMemberAccess, SyntaxGenerator generator);
    }
}
