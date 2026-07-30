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

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2231: Overload operator equals on overriding ValueType.Equals
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OverloadOperatorEqualsOnOverridingValueTypeEqualsFixer : SyntaxEditorBasedCodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer.RuleId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            string title = MicrosoftCodeQualityAnalyzersResources.OverloadOperatorEqualsOnOverridingValueTypeEqualsTitle;
            RegisterCodeFix(context, title, title);
            return Task.CompletedTask;
        }

        protected override async Task ApplyFixAsync(Document document, Diagnostic diagnostic, SyntaxEditor editor, CancellationToken cancellationToken)
        {
            SyntaxNode declaration = editor.Generator.GetDeclaration(editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan));
            if (declaration == null)
            {
                return;
            }

            SemanticModel model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol typeSymbol)
            {
                return;
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.EqualityOperatorName))
            {
                editor.AddMember(declaration, editor.Generator.DefaultOperatorEqualityDeclaration(typeSymbol));
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.InequalityOperatorName))
            {
                editor.AddMember(declaration, editor.Generator.DefaultOperatorInequalityDeclaration(typeSymbol));
            }
        }
    }
}
