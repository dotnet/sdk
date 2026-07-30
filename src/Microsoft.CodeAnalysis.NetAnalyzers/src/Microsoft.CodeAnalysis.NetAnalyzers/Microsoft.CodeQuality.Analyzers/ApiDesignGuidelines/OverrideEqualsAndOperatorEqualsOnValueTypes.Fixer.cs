// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1815: Override equals and operator equals on value types
    /// </summary>
    public abstract class OverrideEqualsAndOperatorEqualsOnValueTypesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.RuleId);

        // The analyzer reports a missing Equals override and missing equality operators separately, both on
        // the type, so one declaration can carry two diagnostics. The fix adds everything the type is
        // missing, so it has to run once per declaration rather than once per diagnostic.
        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<HashSet<SyntaxNode>>(
                static _ => new HashSet<SyntaxNode>(),
                ImplementMissingMembersAsync);

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;
            string title = MicrosoftCodeQualityAnalyzersResources.OverrideEqualsAndOperatorEqualsOnValueTypesTitle;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken =>
                    {
                        HashSet<SyntaxNode> fixedDeclarations = new();
                        return SyntaxEditorFixAllProvider.ApplyFixesAsync(
                            document,
                            diagnostics,
                            (doc, diagnostic, editor, token) => ImplementMissingMembersAsync(doc, diagnostic, editor, fixedDeclarations, token),
                            cancellationToken);
                    },
                    title),
                diagnostics);

            return Task.CompletedTask;
        }

        private static async Task ImplementMissingMembersAsync(
            Document document,
            Diagnostic diagnostic,
            SyntaxEditor editor,
            HashSet<SyntaxNode> fixedDeclarations,
            CancellationToken cancellationToken)
        {
            SyntaxNode enclosingNode = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
            SyntaxNode declaration = editor.Generator.GetDeclaration(enclosingNode);
            if (declaration == null || !fixedDeclarations.Add(declaration))
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol typeSymbol)
            {
                return;
            }

            SyntaxGenerator generator = editor.Generator;

            if (!typeSymbol.OverridesEquals())
            {
                editor.AddMember(declaration, generator.DefaultEqualsOverrideDeclaration(model.Compilation, typeSymbol));
            }

            if (!typeSymbol.OverridesGetHashCode())
            {
                editor.AddMember(declaration, generator.DefaultGetHashCodeOverrideDeclaration(model.Compilation));
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.EqualityOperatorName))
            {
                editor.AddMember(declaration, generator.DefaultOperatorEqualityDeclaration(typeSymbol));
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.InequalityOperatorName))
            {
                editor.AddMember(declaration, generator.DefaultOperatorInequalityDeclaration(typeSymbol));
            }
        }
    }
}