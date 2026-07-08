// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1040: <inheritdoc cref="AvoidEmptyInterfacesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidEmptyInterfacesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1040";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidEmptyInterfacesTitle)),
            CreateLocalizableResourceString(nameof(AvoidEmptyInterfacesMessage)),
            DiagnosticCategory.Design,
            RuleLevel.CandidateForRemoval,
            CreateLocalizableResourceString(nameof(AvoidEmptyInterfacesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeInterface, SymbolKind.NamedType);
        }

        private static void AnalyzeInterface(SymbolAnalysisContext context)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;

            if (symbol.TypeKind == TypeKind.Interface &&
                context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation) &&
                symbol.GetMembers().IsEmpty &&
                !symbol.AllInterfaces.SelectMany(s => s.GetMembers()).Any())
            {
                context.ReportDiagnostic(symbol.CreateDiagnostic(Rule));
            }
        }
    }
}