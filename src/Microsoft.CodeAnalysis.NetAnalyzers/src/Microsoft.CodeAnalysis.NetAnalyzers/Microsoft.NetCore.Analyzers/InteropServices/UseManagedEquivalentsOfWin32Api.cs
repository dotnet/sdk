// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    /// <summary>
    /// CA2205: Use managed equivalents of win32 api
    /// </summary>
    public abstract class UseManagedEquivalentsOfWin32ApiAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2205";

        /*internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseManagedEquivalentsOfWin32ApiTitle)),
            CreateLocalizableResourceString(nameof(UseManagedEquivalentsOfWin32ApiMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.CandidateForRemoval,
            description: CreateLocalizableResourceString(nameof(UseManagedEquivalentsOfWin32ApiDescription)),
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