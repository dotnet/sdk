// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1707: Identifiers should not contain underscores
    /// </summary>
    public abstract class IdentifiersShouldNotContainUnderscoresFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(IdentifiersShouldNotContainUnderscoresAnalyzer.RuleId);

        protected abstract string GetNewName(string name);
        protected abstract SyntaxNode GetDeclarationNode(SyntaxNode node);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var node = root.FindNode(context.Span);
            if (node == null)
            {
                return;
            }

            var symbol = model.GetDeclaredSymbol(GetDeclarationNode(node), context.CancellationToken);
            if (symbol == null)
            {
                return;
            }

            var newName = GetNewName(symbol.Name);
            if (newName.Length == 0)
            {
                return;
            }

            // Make sure there is no symbol with the same name already exists.
            if (!model.LookupSymbols(context.Span.Start, symbol.ContainingType, newName).IsEmpty)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.IdentifiersShouldNotContainUnderscoresCodeFixTitle;
            context.RegisterCodeFix(new MyCodeAction(title,
                                        ct => Renamer.RenameSymbolAsync(context.Document.Project.Solution, symbol, newName, null, ct),
                                        equivalenceKey: title),
                                    context.Diagnostics);
        }

        protected static string RemoveUnderscores(string name)
        {
            var builder = new StringBuilder();
            bool isPreviousUnderscore = false;
            foreach (char c in name)
            {
                if (c == '_')
                {
                    isPreviousUnderscore = true;
                    continue;
                }

                builder.Append(isPreviousUnderscore ? char.ToUpper(c, CultureInfo.InvariantCulture) : c);
                isPreviousUnderscore = false;
            }

            return builder.ToString();
        }

        private class MyCodeAction : SolutionChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Solution>> createChangedSolution, string equivalenceKey)
                : base(title, createChangedSolution, equivalenceKey)
            {
            }
        }
    }
}
