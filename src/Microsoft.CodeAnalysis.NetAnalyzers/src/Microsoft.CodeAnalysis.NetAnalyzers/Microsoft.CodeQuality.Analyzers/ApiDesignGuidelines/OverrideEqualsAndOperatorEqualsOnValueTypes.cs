// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1815: <inheritdoc cref="OverrideEqualsAndOperatorEqualsOnValueTypesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class OverrideEqualsAndOperatorEqualsOnValueTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1815";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(OverrideEqualsAndOperatorEqualsOnValueTypesTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(OverrideEqualsAndOperatorEqualsOnValueTypesDescription));

        internal static readonly DiagnosticDescriptor EqualsRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(OverrideEqualsAndOperatorEqualsOnValueTypesMessageEquals)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,    // Records may make this rule less painful
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor OpEqualityRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(OverrideEqualsAndOperatorEqualsOnValueTypesMessageOpEquality)),
            DiagnosticCategory.Performance,
            RuleLevel.Disabled,    // Records may make this rule less painful
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(EqualsRule, OpEqualityRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var iEnumerator = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIEnumerator);
                var genericIEnumerator = compilationStartContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsGenericIEnumerator1);

                compilationStartContext.RegisterSymbolAction(context =>
                {
                    var namedType = (INamedTypeSymbol)context.Symbol;

                    // FxCop compat:
                    //  1. Do not fire for enums.
                    //  2. Do not fire for enumerators.
                    //  3. Do not fire for value types without members.
                    //  4. Externally visible types by default.
                    //  5. Do not fire for ref struct.
                    // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                    // will always have identical configured visibility.
                    if (!namedType.IsValueType ||
                        namedType.TypeKind == TypeKind.Enum ||
                        (namedType.TypeKind == TypeKind.Struct && namedType.IsRefLikeType) ||
                        !context.Options.MatchesConfiguredVisibility(EqualsRule, namedType, context.Compilation) ||
                        !namedType.GetMembers().Any(m => !m.IsConstructor()))
                    {
                        return;
                    }

                    // Enumerators are often ValueTypes to prevent heap allocation when enumerating
                    if (iEnumerator != null && namedType.DerivesFromOrImplementsAnyConstructionOf(iEnumerator) ||
                        genericIEnumerator != null && namedType.DerivesFromOrImplementsAnyConstructionOf(genericIEnumerator))
                    {
                        return;
                    }

                    if (!namedType.OverridesEquals())
                    {
                        context.ReportDiagnostic(namedType.CreateDiagnostic(EqualsRule, namedType.Name));
                    }

                    if (!namedType.ImplementsEqualityOperators())
                    {
                        context.ReportDiagnostic(namedType.CreateDiagnostic(OpEqualityRule, namedType.Name));
                    }
                }, SymbolKind.NamedType);
            });
        }
    }
}