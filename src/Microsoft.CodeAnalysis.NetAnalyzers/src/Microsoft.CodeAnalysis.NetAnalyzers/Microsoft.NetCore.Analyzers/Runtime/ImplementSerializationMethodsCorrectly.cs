// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2238: Implement serialization methods correctly
    /// </summary>
    public abstract class ImplementSerializationMethodsCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2238";

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyDescription));

        internal static readonly DiagnosticDescriptor VisibilityRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyMessageVisibility)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor ReturnTypeRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyMessageReturnType)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor ParametersRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyMessageParameters)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor GenericRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyMessageGeneric)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor StaticRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementSerializationMethodsCorrectlyMessageStatic)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX ? ImmutableArray.Create(VisibilityRule, ReturnTypeRule, ParametersRule, GenericRule, StaticRule) : ImmutableArray<DiagnosticDescriptor>.Empty;

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