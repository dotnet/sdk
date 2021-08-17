// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OverrideMethodsOnComparableTypesFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OverrideMethodsOnComparableTypesAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = generator.GetDeclaration(declaration);
            if (declaration == null)
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var typeSymbol = model.GetDeclaredSymbol(declaration, context.CancellationToken) as INamedTypeSymbol;
            if (typeSymbol?.TypeKind is not TypeKind.Class and
                not TypeKind.Struct)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.ImplementComparable;
            context.RegisterCodeFix(
                new MyCodeAction(title,
                    async ct => await ImplementComparableAsync(context.Document, declaration, typeSymbol, ct).ConfigureAwait(false),
                    equivalenceKey: title),
                context.Diagnostics);
        }

        private static async Task<Document> ImplementComparableAsync(Document document, SyntaxNode declaration, INamedTypeSymbol typeSymbol, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            if (!typeSymbol.OverridesEquals())
            {
                var equalsMethod = generator.DefaultEqualsOverrideDeclaration(editor.SemanticModel.Compilation, typeSymbol);

                editor.AddMember(declaration, equalsMethod);
            }

            if (!typeSymbol.OverridesGetHashCode())
            {
                var getHashCodeMethod = generator.DefaultGetHashCodeOverrideDeclaration(editor.SemanticModel.Compilation);

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

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.LessThanOperatorName))
            {
                var lessThanOperator = generator.DefaultOperatorLessThanDeclaration(typeSymbol);

                editor.AddMember(declaration, lessThanOperator);
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.LessThanOrEqualOperatorName))
            {
                var lessThanOrEqualOperator = generator.DefaultOperatorLessThanOrEqualDeclaration(typeSymbol);

                editor.AddMember(declaration, lessThanOrEqualOperator);
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.GreaterThanOperatorName))
            {
                var greaterThanOperator = generator.DefaultOperatorGreaterThanDeclaration(typeSymbol);

                editor.AddMember(declaration, greaterThanOperator);
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.GreaterThanOrEqualOperatorName))
            {
                var greaterThanOrEqualOperator = generator.DefaultOperatorGreaterThanOrEqualDeclaration(typeSymbol);

                editor.AddMember(declaration, greaterThanOrEqualOperator);
            }

            return editor.GetChangedDocument();
        }

        // Needed for Telemetry (https://github.com/dotnet/roslyn-analyzers/issues/192)
        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
