// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.ApiReview
{
    /// <summary>
    /// CA2001: Avoid calling problematic methods
    /// </summary>
    public abstract class AvoidCallingProblematicMethodsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2001";

        /*private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageSystemGCCollect = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemGCCollect), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemThreadingThreadResume = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemThreadingThreadResume), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemThreadingThreadSuspend = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemThreadingThreadSuspend), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemTypeInvokeMember = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemTypeInvokeMember), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCoInitializeSecurity = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageCoInitializeSecurity), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageCoSetProxyBlanket = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageCoSetProxyBlanket), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemRuntimeInteropServicesSafeHandleDangerousGetHandle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemRuntimeInteropServicesSafeHandleDangerousGetHandle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemReflectionAssemblyLoadFrom = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemReflectionAssemblyLoadFrom), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemReflectionAssemblyLoadFile = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemReflectionAssemblyLoadFile), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageSystemReflectionAssemblyLoadWithPartialName = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsMessageSystemReflectionAssemblyLoadWithPartialName), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidCallingProblematicMethodsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor SystemGCCollectRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemGCCollect,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemThreadingThreadResumeRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemThreadingThreadResume,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemThreadingThreadSuspendRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemThreadingThreadSuspend,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemTypeInvokeMemberRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemTypeInvokeMember,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor CoInitializeSecurityRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageCoInitializeSecurity,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor CoSetProxyBlanketRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageCoSetProxyBlanket,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemRuntimeInteropServicesSafeHandleDangerousGetHandleRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemRuntimeInteropServicesSafeHandleDangerousGetHandle,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemReflectionAssemblyLoadFromRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemReflectionAssemblyLoadFrom,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemReflectionAssemblyLoadFileRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemReflectionAssemblyLoadFile,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);
        internal static DiagnosticDescriptor SystemReflectionAssemblyLoadWithPartialNameRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageSystemReflectionAssemblyLoadWithPartialName,
                                                                             DiagnosticCategory.Reliability,
                                                                             RuleLevel.Disabled,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false,
                                                                             isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray<DiagnosticDescriptor>.Empty;
        // ImmutableArray.Create(SystemGCCollectRule, SystemThreadingThreadResumeRule, SystemThreadingThreadSuspendRule, SystemTypeInvokeMemberRule, CoInitializeSecurityRule, CoSetProxyBlanketRule, SystemRuntimeInteropServicesSafeHandleDangerousGetHandleRule, SystemReflectionAssemblyLoadFromRule, SystemReflectionAssemblyLoadFileRule, SystemReflectionAssemblyLoadWithPartialNameRule);

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