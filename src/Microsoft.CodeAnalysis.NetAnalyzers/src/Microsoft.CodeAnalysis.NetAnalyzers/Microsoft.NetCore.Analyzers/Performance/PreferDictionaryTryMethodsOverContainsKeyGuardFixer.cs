// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;

namespace Microsoft.NetCore.Analyzers.Performance
{
    public abstract class PreferDictionaryTryMethodsOverContainsKeyGuardFixer : CodeFixProvider
    {
        protected const string Value = "value";
        protected const string TryGetValue = nameof(TryGetValue);
        protected const string TryAdd = nameof(TryAdd);

        protected const string TryGetValueEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryGetValueCodeFixTitle);
        protected const string TryAddEquivalenceKey = nameof(MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryAddValueCodeFixTitle);

        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryGetValueRuleId,
            PreferDictionaryTryMethodsOverContainsKeyGuardAnalyzer.PreferTryAddRuleId
        );

        protected static string PreferDictionaryTryGetValueCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryGetValueCodeFixTitle;

        protected static string PreferDictionaryTryAddValueCodeFixTitle => MicrosoftNetCoreAnalyzersResources.PreferDictionaryTryAddValueCodeFixTitle;

        public sealed override FixAllProvider GetFixAllProvider()
            => SyntaxEditorFixAllProvider.Create<FixAllState>(context => new FixAllState(context.CodeActionEquivalenceKey), ApplyFixAsync);

        /// <summary>
        /// Registers an action applying <see cref="ApplyFixAsync"/> to every diagnostic in
        /// <paramref name="context"/>, through the same editor and state a fix-all pass would use.
        /// </summary>
        protected void RegisterCodeFix(CodeFixContext context, string title, string equivalenceKey)
        {
            Document document = context.Document;
            ImmutableArray<Diagnostic> diagnostics = context.Diagnostics;
            var state = new FixAllState(equivalenceKey);

            CodeAction codeAction = CodeAction.Create(
                title,
                (cancellationToken) => SyntaxEditorFixAllProvider.ApplyFixesAsync(
                    document,
                    diagnostics,
                    (fixDocument, fixDiagnostic, editor, token) => ApplyFixAsync(fixDocument, fixDiagnostic, editor, state, token),
                    cancellationToken),
                equivalenceKey
            );
            context.RegisterCodeFix(codeAction, diagnostics);
        }

        protected abstract Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, FixAllState state, CancellationToken cancellationToken);

        /// <summary>
        /// The state shared by every fix applied to one document.
        /// </summary>
        protected sealed class FixAllState
        {
            private readonly List<(ISymbol? Scope, string Name)> _introducedNames = new();

            public FixAllState(string? equivalenceKey)
            {
                EquivalenceKey = equivalenceKey;
            }

            /// <summary>
            /// The key of the action being applied. <see cref="SyntaxEditorFixAllProvider"/> does not filter
            /// diagnostics by it, so a fixer offering more than one action has to do so itself.
            /// </summary>
            public string? EquivalenceKey { get; }

            /// <summary>
            /// The locals a previously applied fix introduced into the member containing
            /// <paramref name="position"/>. They are invisible to <paramref name="semanticModel"/>, which is
            /// bound to the document as it was before any fix ran, so without this two guards in one member
            /// would both introduce a local named <see cref="Value"/>.
            /// </summary>
            public ISet<string> GetReservedNames(SemanticModel semanticModel, int position, CancellationToken cancellationToken)
            {
                ISymbol? scope = GetScope(semanticModel, position, cancellationToken);
                var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach ((ISymbol? introducedScope, string name) in _introducedNames)
                {
                    if (SymbolEqualityComparer.Default.Equals(introducedScope, scope))
                    {
                        reserved.Add(name);
                    }
                }

                return reserved;
            }

            public void RecordIntroducedName(SemanticModel semanticModel, int position, string name, CancellationToken cancellationToken)
            {
                _introducedNames.Add((GetScope(semanticModel, position, cancellationToken), name));
            }

            /// <summary>
            /// The member a local declared at <paramref name="position"/> shares its name space with. Neither
            /// language lets a local shadow one declared further out in the same member, so a lambda or a
            /// local function resolves to the member containing it.
            /// </summary>
            private static ISymbol? GetScope(SemanticModel semanticModel, int position, CancellationToken cancellationToken)
            {
                ISymbol? symbol = semanticModel.GetEnclosingSymbol(position, cancellationToken);

                while (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction } method)
                {
                    symbol = method.ContainingSymbol;
                }

                return symbol;
            }
        }
    }
}