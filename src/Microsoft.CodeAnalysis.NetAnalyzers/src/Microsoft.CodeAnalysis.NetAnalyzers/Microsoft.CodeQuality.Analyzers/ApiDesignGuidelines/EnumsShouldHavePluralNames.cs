// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1714: Flags enums should have plural names
    /// CA1717: Only Flags enums should have plural names
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class EnumsShouldHavePluralNamesAnalyzer : DiagnosticAnalyzer
    {
        #region CA1714
        internal const string RuleId_Plural = "CA1714";

        private static readonly LocalizableString s_localizableTitle_CA1714 =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.FlagsEnumsShouldHavePluralNamesTitle),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage_CA1714 =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.FlagsEnumsShouldHavePluralNamesMessage),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableDescription_CA1714 =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.FlagsEnumsShouldHavePluralNamesDescription),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule_CA1714 =
            new DiagnosticDescriptor(
                RuleId_Plural,
                s_localizableTitle_CA1714,
                s_localizableMessage_CA1714,
                DiagnosticCategory.Naming,
                DiagnosticHelpers.DefaultDiagnosticSeverity,
                isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                description: s_localizableDescription_CA1714,
                helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1714-flags-enums-should-have-plural-names",
                customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        #endregion

        #region CA1717
        internal const string RuleId_NoPlural = "CA1717";

        private static readonly LocalizableString s_localizableTitle_CA1717 =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.OnlyFlagsEnumsShouldHavePluralNamesTitle),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage_CA1717 =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.OnlyFlagsEnumsShouldHavePluralNamesMessage),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableDescription_CA1717 =
            new LocalizableResourceString(
                nameof(MicrosoftCodeQualityAnalyzersResources.OnlyFlagsEnumsShouldHavePluralNamesDescription),
                MicrosoftCodeQualityAnalyzersResources.ResourceManager,
                typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule_CA1717 =
            new DiagnosticDescriptor(
                RuleId_NoPlural,
                s_localizableTitle_CA1717,
                s_localizableMessage_CA1717,
                DiagnosticCategory.Naming,
                DiagnosticHelpers.DefaultDiagnosticSeverity,
                isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                description: s_localizableDescription_CA1717,
                helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1717-only-flagsattribute-enums-should-have-plural-names",
                customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        #endregion

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule_CA1714, Rule_CA1717);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? flagsAttribute = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemFlagsAttribute);
                if (flagsAttribute == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, flagsAttribute), SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol flagsAttribute)
        {
            var symbol = (INamedTypeSymbol)context.Symbol;
            if (symbol.TypeKind != TypeKind.Enum)
            {
                return;
            }

            var reportCA1714 = symbol.MatchesConfiguredVisibility(context.Options, Rule_CA1714, context.CancellationToken);
            var reportCA1717 = symbol.MatchesConfiguredVisibility(context.Options, Rule_CA1717, context.CancellationToken);
            if (!reportCA1714 && !reportCA1717)
            {
                return;
            }

            if (symbol.Name.EndsWith("i", StringComparison.OrdinalIgnoreCase) || symbol.Name.EndsWith("ae", StringComparison.OrdinalIgnoreCase))
            {
                // Skip words ending with 'i' and 'ae' to avoid flagging irregular plurals.
                // Humanizer does not recognize these as plurals, such as 'formulae', 'trophi', etc.
                return;
            }

            if (!symbol.Name.IsASCII())
            {
                // Skip non-ASCII names.
                return;
            }

            bool hasFlagsAttribute = symbol.GetAttributes().Any(a => a.AttributeClass.Equals(flagsAttribute));
            if (hasFlagsAttribute)
            {
                if (reportCA1714 && !IsPlural(symbol.Name)) // Checking Rule CA1714
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(Rule_CA1714, symbol.OriginalDefinition.Locations.First(), symbol.Name));
                }
            }
            else
            {
                if (reportCA1717 && IsPlural(symbol.Name)) // Checking Rule CA1717
                {
                    context.ReportDiagnostic(symbol.CreateDiagnostic(Rule_CA1717, symbol.OriginalDefinition.Locations.First(), symbol.Name));
                }
            }
        }

        private static bool IsPlural(string word)
            => word.Equals(word.Pluralize(inputIsKnownToBeSingular: false), StringComparison.OrdinalIgnoreCase);
    }
}

