// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1819: <inheritdoc cref="PropertiesShouldNotReturnArraysTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PropertiesShouldNotReturnArraysAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1819";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PropertiesShouldNotReturnArraysTitle)),
            CreateLocalizableResourceString(nameof(PropertiesShouldNotReturnArraysMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(PropertiesShouldNotReturnArraysDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Property);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var symbol = (IPropertySymbol)context.Symbol;
            if (symbol.Type.TypeKind == TypeKind.Array && !symbol.IsOverride)
            {
                if (context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation) &&
                    !symbol.ContainingType.IsAttribute())
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(Rule));
                }
            }
        }
    }
}
