// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1034: Nested types should not be visible
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class NestedTypesShouldNotBeVisibleAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1034";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.NestedTypesShouldNotBeVisibleTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.NestedTypesShouldNotBeVisibleMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageVisualBasicModule = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.NestedTypesShouldNotBeVisibleMessageVisualBasicModule), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.NestedTypesShouldNotBeVisibleDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.CandidateForRemoval,     // Need to validate cases where people want it.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor VisualBasicModuleRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageVisualBasicModule,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.CandidateForRemoval,     // Need to validate cases where people want it.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, VisualBasicModuleRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(
                compilationStartContext =>
                {
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartContext.Compilation);

                    INamedTypeSymbol? enumeratorType = wellKnownTypeProvider.Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerator);
                    INamedTypeSymbol? dataSetType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataSet);
                    INamedTypeSymbol? dataTableType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataTable);
                    INamedTypeSymbol? dataRowType = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataRow);

                    compilationStartContext.RegisterSymbolAction(
                        symbolAnalysisContext =>
                        {
                            var nestedType = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                            INamedTypeSymbol containingType = nestedType.ContainingType;
                            if (containingType == null)
                            {
                                return;
                            }

                            // Do not report diagnostic for compiler generated nested types for delegate declaration
                            if (nestedType.TypeKind == TypeKind.Delegate)
                            {
                                return;
                            }

                            // The Framework Design Guidelines (see 4.9 Nested Types) say that it is okay
                            // to expose nested types for advanced customization and subclassing scenarios,
                            // so, following FxCop's implementation of this rule, we allow protected and
                            // internal nested types.
                            if (nestedType.DeclaredAccessibility != Accessibility.Public)
                            {
                                return;
                            }

                            // Even if the nested type is declared public, don't complain if it's within
                            // a type that's not visible outside the assembly.
                            if (!containingType.IsExternallyVisible())
                            {
                                return;
                            }

                            // By the design guidelines, nested enumerators are exempt.
                            if (nestedType.AllInterfaces.Contains(enumeratorType))
                            {
                                return;
                            }

                            // FxCop allowed public nested enums to accommodate .NET types such as
                            // Environment.SpecialFolders.
                            if (nestedType.TypeKind == TypeKind.Enum)
                            {
                                return;
                            }

                            // Allow builder pattern
                            if (nestedType.Name.EndsWith("Builder", StringComparison.Ordinal) &&
                                containingType.ContainingType == null &&
                                !containingType.InstanceConstructors.Any(ctor => ctor.IsExternallyVisible()))
                            {
                                return;
                            }

                            if (IsDataSetSpecialCase(containingType, nestedType, dataSetType, dataTableType, dataRowType))
                            {
                                return;
                            }

                            DiagnosticDescriptor descriptor = containingType.TypeKind == TypeKind.Module
                                ? VisualBasicModuleRule
                                : DefaultRule;

                            symbolAnalysisContext.ReportDiagnostic(nestedType.CreateDiagnostic(descriptor, nestedType.Name));
                        },
                        SymbolKind.NamedType);
                });
        }

        // When you use the Visual Studio Dataset Designer to add a DataTable to a DataSet, the
        // designer generates two public nested types within the DataSet: a DataTable and a DataRow.
        // Since these are generated code, we don't want to fire on them.
        private static bool IsDataSetSpecialCase(
            INamedTypeSymbol containingType,
            INamedTypeSymbol nestedType,
            INamedTypeSymbol? dataSetType,
            INamedTypeSymbol? dataTableType,
            INamedTypeSymbol? dataRowType)
        {
            if (!containingType.GetBaseTypes().Contains(dataSetType))
            {
                return false;
            }

            var nestedTypeBases = nestedType.GetBaseTypes().ToList();
            return dataTableType != null && nestedTypeBases.Contains(dataTableType) ||
                dataRowType != null && nestedTypeBases.Contains(dataRowType);
        }
    }
}