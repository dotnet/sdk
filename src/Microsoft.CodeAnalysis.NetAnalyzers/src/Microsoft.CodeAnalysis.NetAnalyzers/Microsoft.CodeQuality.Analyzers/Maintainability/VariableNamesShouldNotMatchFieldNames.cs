// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.Maintainability
{
    /// <summary>
    /// CA1500: Variable names should not match field names
    /// </summary>
    public abstract class VariableNamesShouldNotMatchFieldNamesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1500";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.VariableNamesShouldNotMatchFieldNamesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageLocal = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.VariableNamesShouldNotMatchFieldNamesMessageLocal), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageParameter = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.VariableNamesShouldNotMatchFieldNamesMessageParameter), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.VariableNamesShouldNotMatchFieldNamesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor LocalRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageLocal,
                                                                             DiagnosticCategory.Maintainability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false,
                                                                             isEnabledByDefaultInAggressiveMode: false);
        internal static DiagnosticDescriptor ParameterRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageParameter,
                                                                             DiagnosticCategory.Maintainability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false,
                                                                             isEnabledByDefaultInAggressiveMode: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray<DiagnosticDescriptor>.Empty;
        //ImmutableArray.Create(LocalRule, ParameterRule);

#pragma warning disable RS1025 // Configure generated code analysis
        public override void Initialize(AnalysisContext analysisContext)
#pragma warning restore RS1025 // Configure generated code analysis
        {
            analysisContext.EnableConcurrentExecution();

            // TODO: Configure generated code analysis.
            //analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }
}