// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2119: Seal methods that satisfy private interfaces
    /// </summary>    
    [ExportCodeFixProvider(LanguageNames.CSharp /*, LanguageNames.VisualBasic*/), Shared]  // note: disabled VB until SyntaxGenerator.WithStatements works
    public sealed class SealMethodsThatSatisfyPrivateInterfacesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(SealMethodsThatSatisfyPrivateInterfacesAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var gen = SyntaxGenerator.GetGenerator(context.Document);

            foreach (var dx in context.Diagnostics)
            {
                if (dx.Location.IsInSource)
                {
                    var root = await dx.Location.SourceTree.GetRootAsync(context.CancellationToken).ConfigureAwait(false);
                    var declarationNode = gen.GetDeclaration(root.FindToken(dx.Location.SourceSpan.Start).Parent);
                    if (declarationNode != null)
                    {
                        var solution = context.Document.Project.Solution;
                        var symbol = model.GetDeclaredSymbol(declarationNode, context.CancellationToken);

                        if (symbol != null)
                        {
                            if (symbol is not INamedTypeSymbol)
                            {
                                if (symbol.IsOverride)
                                {
                                    context.RegisterCodeFix(new ChangeModifierAction(MicrosoftCodeQualityAnalyzersResources.MakeMemberNotOverridable, "MakeMemberNotOverridable", solution, symbol, DeclarationModifiers.From(symbol) + DeclarationModifiers.Sealed), dx);
                                }
                                else if (symbol.IsVirtual)
                                {
                                    context.RegisterCodeFix(new ChangeModifierAction(MicrosoftCodeQualityAnalyzersResources.MakeMemberNotOverridable, "MakeMemberNotOverridable", solution, symbol, DeclarationModifiers.From(symbol) - DeclarationModifiers.Virtual), dx);
                                }
                                else if (symbol.IsAbstract)
                                {
                                    context.RegisterCodeFix(new ChangeModifierAction(MicrosoftCodeQualityAnalyzersResources.MakeMemberNotOverridable, "MakeMemberNotOverridable", solution, symbol, DeclarationModifiers.From(symbol) - DeclarationModifiers.Abstract), dx);
                                }

                                // trigger containing type code fixes below
                                symbol = symbol.ContainingType;
                            }

                            // if the diagnostic identified a type then it is the containing type of the member
                            if (symbol is INamedTypeSymbol type)
                            {
                                // cannot make abstract type sealed because they cannot be constructed
                                if (!type.IsAbstract)
                                {
                                    context.RegisterCodeFix(new ChangeModifierAction(MicrosoftCodeQualityAnalyzersResources.MakeDeclaringTypeSealed, "MakeDeclaringTypeSealed", solution, type, DeclarationModifiers.From(type) + DeclarationModifiers.Sealed), dx);
                                }

                                context.RegisterCodeFix(new ChangeAccessibilityAction(MicrosoftCodeQualityAnalyzersResources.MakeDeclaringTypeInternal, "MakeDeclaringTypeInternal", solution, type, Accessibility.Internal), dx);
                            }
                        }
                    }
                }
            }
        }

        private abstract class ChangeSymbolAction : CodeAction
        {
            protected ChangeSymbolAction(string title, string equivalenceKey, Solution solution, ISymbol symbol)
            {
                Title = title;
                EquivalenceKey = equivalenceKey;
                Solution = solution;
                Symbol = symbol;
            }

            public override string Title { get; }
            public override string EquivalenceKey { get; }
            public Solution Solution { get; }
            public ISymbol Symbol { get; }
        }

        private class ChangeModifierAction : ChangeSymbolAction
        {
            private readonly DeclarationModifiers _newModifiers;

            public ChangeModifierAction(string title, string equivalenceKey, Solution solution, ISymbol symbol, DeclarationModifiers newModifiers)
                : base(title, equivalenceKey, solution, symbol)
            {
                _newModifiers = newModifiers;
            }

            protected override async Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
            {
                var editor = SymbolEditor.Create(this.Solution);
                await editor.EditAllDeclarationsAsync(this.Symbol, (e, d) =>
                {
                    e.SetModifiers(d, _newModifiers);

                    if (this.Symbol.IsAbstract && !_newModifiers.IsAbstract && this.Symbol.Kind == SymbolKind.Method)
                    {
                        e.ReplaceNode(d, (_d, g) => g.WithStatements(_d, Array.Empty<SyntaxNode>()));
                    }
                }
                , cancellationToken).ConfigureAwait(false);
                return editor.ChangedSolution;
            }
        }

        private class ChangeAccessibilityAction : ChangeSymbolAction
        {
            private readonly Accessibility _newAccessibility;

            public ChangeAccessibilityAction(string title, string equivalenceKey, Solution solution, ISymbol symbol, Accessibility newAccessibilty)
                : base(title, equivalenceKey, solution, symbol)
            {
                _newAccessibility = newAccessibilty;
            }

            protected override async Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
            {
                var editor = SymbolEditor.Create(this.Solution);
                await editor.EditAllDeclarationsAsync(this.Symbol, (e, d) => e.SetAccessibility(d, _newAccessibility), cancellationToken).ConfigureAwait(false);
                return editor.ChangedSolution;
            }
        }
    }
}