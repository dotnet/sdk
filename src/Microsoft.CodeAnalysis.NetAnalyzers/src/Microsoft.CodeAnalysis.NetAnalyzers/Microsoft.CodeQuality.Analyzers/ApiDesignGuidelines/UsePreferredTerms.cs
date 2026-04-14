// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1726: Use preferred terms
    /// </summary>
    public abstract class UsePreferredTermsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1726";

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(UsePreferredTermsTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(UsePreferredTermsDescription));

        internal static readonly DiagnosticDescriptor AssemblyRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageAssembly)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor NamespaceRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageNamespace)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MemberParameterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageMemberParameter)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor DelegateParameterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageDelegateParameter)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor TypeTypeParameterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageTypeTypeParameter)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MethodTypeParameterRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageMethodTypeParameter)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor TypeRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageType)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MemberRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageMember)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor AssemblyNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageAssemblyNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor NamespaceNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageNamespaceNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MemberParameterNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageMemberParameterNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor DelegateParameterNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageDelegateParameterNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor TypeTypeParameterNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageTypeTypeParameterNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MethodTypeParameterNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageMethodTypeParameterNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor TypeNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageTypeNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MemberNoAlternateRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UsePreferredTermsMessageMemberNoAlternate)),
            DiagnosticCategory.Naming,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //ImmutableArray.Create(AssemblyRule, NamespaceRule, MemberParameterRule, DelegateParameterRule, TypeTypeParameterRule, MethodTypeParameterRule, TypeRule, MemberRule, AssemblyNoAlternateRule, NamespaceNoAlternateRule, MemberParameterNoAlternateRule, DelegateParameterNoAlternateRule, TypeTypeParameterNoAlternateRule, MethodTypeParameterNoAlternateRule, TypeNoAlternateRule, MemberNoAlternateRule);

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