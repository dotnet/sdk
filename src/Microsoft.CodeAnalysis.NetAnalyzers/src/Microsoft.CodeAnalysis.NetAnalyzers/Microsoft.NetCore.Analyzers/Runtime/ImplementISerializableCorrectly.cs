// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA2240: Implement ISerializable correctly
    /// </summary>
    public abstract class ImplementISerializableCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2240";

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(ImplementISerializableCorrectlyTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(ImplementISerializableCorrectlyDescription));

        internal static readonly DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementISerializableCorrectlyMessageDefault)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MakeVisibleRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementISerializableCorrectlyMessageMakeVisible)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);

        internal static readonly DiagnosticDescriptor MakeOverridableRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(ImplementISerializableCorrectlyMessageMakeOverridable)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX ? ImmutableArray.Create(DefaultRule, MakeVisibleRule, MakeOverridableRule) : ImmutableArray<DiagnosticDescriptor>.Empty;

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