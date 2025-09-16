// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
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

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(VariableNamesShouldNotMatchFieldNamesTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(VariableNamesShouldNotMatchFieldNamesDescription));

        internal static readonly DiagnosticDescriptor LocalRule = DiagnosticDescriptorHelper.Create(
            RuleId,
             s_localizableTitle,
             CreateLocalizableResourceString(nameof(VariableNamesShouldNotMatchFieldNamesMessageLocal)),
             DiagnosticCategory.Maintainability,
             RuleLevel.Disabled,
             description: s_localizableDescription,
             isPortedFxCopRule: true,
             isDataflowRule: false,
             isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor ParameterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(VariableNamesShouldNotMatchFieldNamesMessageParameter)),
            DiagnosticCategory.Maintainability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //ImmutableArray.Create(LocalRule, ParameterRule);

#pragma warning disable RS1025 // Configure generated code analysis
        public override void Initialize(AnalysisContext context)
#pragma warning restore RS1025 // Configure generated code analysis
        {
            context.EnableConcurrentExecution();

            // TODO: Configure generated code analysis.
            //analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        }
    }
}