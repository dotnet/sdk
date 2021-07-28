// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1036: A public or protected type implements the System.IComparable interface and
    /// does not override Object.Equals or does not overload the language-specific operator
    /// for equality, inequality, less than, less than or equal, greater than or
    /// greater than or equal. The rule does not report a violation if the type inherits
    /// only an implementation of the interface.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class OverrideMethodsOnComparableTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1036";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverrideMethodsOnComparableTypesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageEquals = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverrideMethodsOnComparableTypesMessageEquals), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageOperators = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverrideMethodsOnComparableTypesMessageOperator), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageBoth = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverrideMethodsOnComparableTypesMessageBoth), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.OverrideMethodsOnComparableTypesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor RuleEquals = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                  s_localizableTitle,
                                                                                  s_localizableMessageEquals,
                                                                                  DiagnosticCategory.Design,
                                                                                  RuleLevel.IdeHidden_BulkConfigurable,
                                                                                  description: s_localizableDescription,
                                                                                  isPortedFxCopRule: true,
                                                                                  isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleOperator = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                  s_localizableTitle,
                                                                                  s_localizableMessageOperators,
                                                                                  DiagnosticCategory.Design,
                                                                                  RuleLevel.IdeHidden_BulkConfigurable,
                                                                                  description: s_localizableDescription,
                                                                                  isPortedFxCopRule: true,
                                                                                  isDataflowRule: false);

        internal static readonly DiagnosticDescriptor RuleBoth = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                  s_localizableTitle,
                                                                                  s_localizableMessageBoth,
                                                                                  DiagnosticCategory.Design,
                                                                                  RuleLevel.IdeHidden_BulkConfigurable,
                                                                                  description: s_localizableDescription,
                                                                                  isPortedFxCopRule: true,
                                                                                  isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleBoth, RuleEquals, RuleOperator);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                INamedTypeSymbol? comparableType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIComparable);
                INamedTypeSymbol? genericComparableType = compilationContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIComparable1);

                // Even if one of them is available, we should continue analysis.
                if (comparableType == null && genericComparableType == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(context =>
                {
                    AnalyzeSymbol(context, comparableType, genericComparableType);
                },
                SymbolKind.NamedType);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol? comparableType, INamedTypeSymbol? genericComparableType)
        {
            // Note all the descriptors/rules for this analyzer have the same ID and category and hence
            // will always have identical configured visibility.
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
            if (namedTypeSymbol.TypeKind == TypeKind.Interface ||
                namedTypeSymbol.TypeKind == TypeKind.Enum ||
                !context.Options.MatchesConfiguredVisibility(RuleBoth, namedTypeSymbol, context.Compilation))
            {
                return;
            }

            if (IsComparableWithBaseNotComparable(namedTypeSymbol, comparableType, genericComparableType))
            {
                var overridesEquals = namedTypeSymbol.OverridesEquals();
                string comparisonOperatorsString = GetNeededComparisonOperators(namedTypeSymbol);

                if (!overridesEquals && comparisonOperatorsString.Length != 0)
                {
                    context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleBoth, namedTypeSymbol.Name, comparisonOperatorsString));
                }
                else if (!overridesEquals)
                {
                    context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleEquals, namedTypeSymbol.Name));
                }
                else if (comparisonOperatorsString.Length != 0)
                {
                    context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(RuleOperator, namedTypeSymbol.Name, comparisonOperatorsString));
                }
            }
        }

        private static bool IsComparableWithBaseNotComparable(INamedTypeSymbol namedTypeSymbol, INamedTypeSymbol? comparableType, INamedTypeSymbol? genericComparableType)
        {
            if (!IsComparableCore(namedTypeSymbol, comparableType, genericComparableType))
            {
                return false;
            }

            foreach (var baseType in namedTypeSymbol.GetBaseTypes())
            {
                if (IsComparableCore(baseType, comparableType, genericComparableType))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsComparableCore(INamedTypeSymbol namedTypeSymbol, INamedTypeSymbol? comparableType, INamedTypeSymbol? genericComparableType)
            => namedTypeSymbol.AllInterfaces.Any(t => t.Equals(comparableType) ||
                                                    (t.ConstructedFrom?.Equals(genericComparableType) ?? false));

        private static string GetNeededComparisonOperators(INamedTypeSymbol symbol)
        {
            StringBuilder? sb = null;
            void Append(string @operator)
            {
                if (sb == null)
                {
                    sb = new StringBuilder();
                }
                else
                {
                    sb.Append(", ");
                }

                sb.Append(@operator);
            }

            if (!symbol.ImplementsOperator(WellKnownMemberNames.EqualityOperatorName))
            {
                Append(symbol.Language == LanguageNames.CSharp ? "==" : "=");
            }

            if (!symbol.ImplementsOperator(WellKnownMemberNames.InequalityOperatorName))
            {
                Append(symbol.Language == LanguageNames.CSharp ? "!=" : "<>");
            }

            if (!symbol.ImplementsOperator(WellKnownMemberNames.LessThanOperatorName))
            {
                Append("<");
            }

            if (!symbol.ImplementsOperator(WellKnownMemberNames.LessThanOrEqualOperatorName))
            {
                Append("<=");
            }

            if (!symbol.ImplementsOperator(WellKnownMemberNames.GreaterThanOperatorName))
            {
                Append(">");
            }

            if (!symbol.ImplementsOperator(WellKnownMemberNames.GreaterThanOrEqualOperatorName))
            {
                Append(">=");
            }

            return sb?.ToString() ?? string.Empty;
        }
    }
}
