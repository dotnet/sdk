// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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

        /*private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageVisibility = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyMessageVisibility), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageReturnType = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyMessageReturnType), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageParameters = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyMessageParameters), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageGeneric = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyMessageGeneric), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageStatic = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyMessageStatic), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.ImplementSerializationMethodsCorrectlyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor VisibilityRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageVisibility,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor ReturnTypeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageReturnType,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor ParametersRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageParameters,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor GenericRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageGeneric,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor StaticRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageStatic,
                                                                             DiagnosticCategory.Usage,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray<DiagnosticDescriptor>.Empty;
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