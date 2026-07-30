// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.NetAnalyzers;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OverrideMethodsOnComparableTypesFixer : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(OverrideMethodsOnComparableTypesAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            if (await GetTypeToFixAsync(context.Document, context.Span, context.CancellationToken).ConfigureAwait(false) is null)
            {
                return;
            }

            string title = MicrosoftCodeQualityAnalyzersResources.ImplementComparable;
            RegisterCodeFix(context, title, title);
        }

        private static async Task<INamedTypeSymbol?> GetTypeToFixAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
            SyntaxNode root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = generator.GetDeclaration(root.FindNode(span));
            if (declaration is null)
            {
                return null;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            return model.GetDeclaredSymbol(declaration, cancellationToken) is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } typeSymbol
                ? typeSymbol
                : null;
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxGenerator generator = editor.Generator;
            SyntaxNode declaration = generator.GetDeclaration(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));
            if (declaration is null)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } typeSymbol)
            {
                return;
            }

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

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.LessThanOperatorName))
            {
                editor.AddMember(declaration, generator.DefaultOperatorLessThanDeclaration(typeSymbol));
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.LessThanOrEqualOperatorName))
            {
                editor.AddMember(declaration, generator.DefaultOperatorLessThanOrEqualDeclaration(typeSymbol));
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.GreaterThanOperatorName))
            {
                editor.AddMember(declaration, generator.DefaultOperatorGreaterThanDeclaration(typeSymbol));
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.GreaterThanOrEqualOperatorName))
            {
                editor.AddMember(declaration, generator.DefaultOperatorGreaterThanOrEqualDeclaration(typeSymbol));
            }
        }
    }
}
