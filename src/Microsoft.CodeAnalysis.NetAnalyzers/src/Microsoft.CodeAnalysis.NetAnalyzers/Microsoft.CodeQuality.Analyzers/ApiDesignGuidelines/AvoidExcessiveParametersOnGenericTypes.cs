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
    /// CA1005: <inheritdoc cref="AvoidExcessiveParametersOnGenericTypesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidExcessiveParametersOnGenericTypes : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1005";
        internal const int MaximumNumberOfTypeParameters = 2;

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidExcessiveParametersOnGenericTypesTitle)),
            CreateLocalizableResourceString(nameof(AvoidExcessiveParametersOnGenericTypesMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            CreateLocalizableResourceString(nameof(AvoidExcessiveParametersOnGenericTypesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInAggressiveMode: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(context =>
            {
                var namedType = (INamedTypeSymbol)context.Symbol;

                // FxCop compat: only analyze externally visible symbols by default.
                if (!context.Options.MatchesConfiguredVisibility(Rule, namedType, context.Compilation))
                {
                    return;
                }

                if (namedType.IsGenericType &&
                    namedType.TypeParameters.Length > MaximumNumberOfTypeParameters)
                {
                    context.ReportDiagnostic(namedType.CreateDiagnostic(Rule, namedType.Name, MaximumNumberOfTypeParameters));
                }
            }, SymbolKind.NamedType);
        }
    }
}
