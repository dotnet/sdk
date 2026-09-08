// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    /// <summary>
    /// CA1301: Avoid duplicate accelerators
    /// </summary>
    public abstract class AvoidDuplicateAcceleratorsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1301";

        /*internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidDuplicateAcceleratorsTitle)),
            CreateLocalizableResourceString(nameof(AvoidDuplicateAcceleratorsMessage)),
            DiagnosticCategory.Globalization,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(AvoidDuplicateAcceleratorsDescription)),
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