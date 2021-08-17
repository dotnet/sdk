// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1815: Override equals and operator equals on value types
    /// </summary>
    public abstract class OverrideEqualsAndOperatorEqualsOnValueTypesFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer.RuleId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode enclosingNode = root.FindNode(context.Span);
            SyntaxNode declaration = generator.GetDeclaration(enclosingNode);
            if (declaration == null)
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol typeSymbol)
            {
                return;
            }

            Diagnostic diagnostic = context.Diagnostics.First();
            string title = MicrosoftCodeQualityAnalyzersResources.OverrideEqualsAndOperatorEqualsOnValueTypesTitle;
            context.RegisterCodeFix(
                new MyCodeAction(
                    title,
                    async ct => await ImplementMissingMembersAsync(declaration, typeSymbol, context.Document, context.CancellationToken).ConfigureAwait(false),
                    equivalenceKey: title),
                diagnostic);
        }

        private static async Task<Document> ImplementMissingMembersAsync(
            SyntaxNode declaration,
            INamedTypeSymbol typeSymbol,
            Document document,
            CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            var generator = editor.Generator;

            if (!typeSymbol.OverridesEquals())
            {
                var equalsMethod = generator.DefaultEqualsOverrideDeclaration(
                    editor.SemanticModel.Compilation, typeSymbol);

                editor.AddMember(declaration, equalsMethod);
            }

            if (!typeSymbol.OverridesGetHashCode())
            {
                var getHashCodeMethod = generator.DefaultGetHashCodeOverrideDeclaration(
                    editor.SemanticModel.Compilation);

                editor.AddMember(declaration, getHashCodeMethod);
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.EqualityOperatorName))
            {
                var equalityOperator = generator.DefaultOperatorEqualityDeclaration(typeSymbol);

                editor.AddMember(declaration, equalityOperator);
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.InequalityOperatorName))
            {
                var inequalityOperator = generator.DefaultOperatorInequalityDeclaration(typeSymbol);

                editor.AddMember(declaration, inequalityOperator);
            }

            return editor.GetChangedDocument();
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }
    }
}