// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsDescription));

        internal static readonly DiagnosticDescriptor SystemGCCollectRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemGCCollect)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemThreadingThreadResumeRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemThreadingThreadResume)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemThreadingThreadSuspendRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemThreadingThreadSuspend)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemTypeInvokeMemberRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemTypeInvokeMember)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor CoInitializeSecurityRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageCoInitializeSecurity)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor CoSetProxyBlanketRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageCoSetProxyBlanket)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemRuntimeInteropServicesSafeHandleDangerousGetHandleRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemRuntimeInteropServicesSafeHandleDangerousGetHandle)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemReflectionAssemblyLoadFromRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemReflectionAssemblyLoadFrom)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemReflectionAssemblyLoadFileRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemReflectionAssemblyLoadFile)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor SystemReflectionAssemblyLoadWithPartialNameRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(AvoidCallingProblematicMethodsMessageSystemReflectionAssemblyLoadWithPartialName)),
            DiagnosticCategory.Reliability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
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