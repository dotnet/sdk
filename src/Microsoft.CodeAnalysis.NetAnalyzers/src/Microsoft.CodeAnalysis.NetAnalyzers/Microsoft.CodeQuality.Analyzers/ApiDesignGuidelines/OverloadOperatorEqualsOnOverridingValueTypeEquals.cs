// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2231: <inheritdoc cref="OverloadOperatorEqualsOnOverridingValueTypeEqualsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2231";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(OverloadOperatorEqualsOnOverridingValueTypeEqualsTitle)),
            CreateLocalizableResourceString(nameof(OverloadOperatorEqualsOnOverridingValueTypeEqualsMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(OverloadOperatorEqualsOnOverridingValueTypeEqualsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(context =>
            {
                var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
                if (namedTypeSymbol.IsValueType &&
                    !(namedTypeSymbol.IsRefLikeType && namedTypeSymbol.TypeKind == TypeKind.Struct) &&
                    context.Options.MatchesConfiguredVisibility(Rule, namedTypeSymbol, context.Compilation) &&
                    namedTypeSymbol.OverridesEquals() &&
                    !namedTypeSymbol.ImplementsEqualityOperators())
                {
                    context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(Rule));
                }
            },
            SymbolKind.NamedType);
        }
    }
}
