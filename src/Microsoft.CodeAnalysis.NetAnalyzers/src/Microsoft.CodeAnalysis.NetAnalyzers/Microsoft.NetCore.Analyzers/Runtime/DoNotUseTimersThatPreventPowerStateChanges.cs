// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1601: Do not use timers that prevent power state changes
    /// </summary>
    public abstract class DoNotUseTimersThatPreventPowerStateChangesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1601";

        /*internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseTimersThatPreventPowerStateChangesTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseTimersThatPreventPowerStateChangesMessage)),
            DiagnosticCategory.Mobility,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotUseTimersThatPreventPowerStateChangesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //ImmutableArray.Create(Rule);

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