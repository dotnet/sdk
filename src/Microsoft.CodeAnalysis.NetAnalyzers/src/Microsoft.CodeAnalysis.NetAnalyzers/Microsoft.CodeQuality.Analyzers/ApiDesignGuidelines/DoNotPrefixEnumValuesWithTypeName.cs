// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Options;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1712: <inheritdoc cref="DoNotPrefixEnumValuesWithTypeNameTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotPrefixEnumValuesWithTypeNameAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1712";
        private const int PercentValuesPrefixedThreshold = 75; // The percent of an enum's values that must appear to be prefixed in order for a diagnostic to be reported on the enum. This value comes from the original FxCop rule's implementation.

        internal static readonly DiagnosticDescriptor Rule =
            DiagnosticDescriptorHelper.Create(
                RuleId,
                CreateLocalizableResourceString(nameof(DoNotPrefixEnumValuesWithTypeNameTitle)),
                CreateLocalizableResourceString(nameof(DoNotPrefixEnumValuesWithTypeNameMessage)),
                DiagnosticCategory.Naming,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: CreateLocalizableResourceString(nameof(DoNotPrefixEnumValuesWithTypeNameDescription)),
                isPortedFxCopRule: true,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext context)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;
            IEnumerable<ISymbol> enumValues;
            if (symbol.TypeKind != TypeKind.Enum || !(enumValues = symbol.GetMembers().Where(m => m.Kind == SymbolKind.Field && !m.IsImplicitlyDeclared)).Any())
            {
                return;
            }

            var prefixedValues = enumValues.Where(m => m.Name.StartsWith(symbol.Name, StringComparison.OrdinalIgnoreCase));
            int percentPrefixed = 100 * prefixedValues.Count() / enumValues.Count();

            var triggerOption = context.Options.GetEnumValuesPrefixTriggerOption(Rule, symbol, context.Compilation, EnumValuesPrefixTrigger.Heuristic);

            if (triggerOption == EnumValuesPrefixTrigger.AnyEnumValue ||
                (triggerOption == EnumValuesPrefixTrigger.AllEnumValues && percentPrefixed == 100) ||
                (triggerOption == EnumValuesPrefixTrigger.Heuristic && percentPrefixed >= PercentValuesPrefixedThreshold))
            {
                foreach (var value in prefixedValues)
                {
                    context.ReportDiagnostic(value.CreateDiagnostic(Rule, symbol.Name));
                }
            }
        }
    }
}
