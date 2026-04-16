// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    /// <summary>
    /// CA2236: Call base class methods on ISerializable types
    /// </summary>
    public abstract class CallBaseClassMethodsOnISerializableTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2236";

        /*internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(CallBaseClassMethodsOnISerializableTypesTitle)),
            CreateLocalizableResourceString(nameof(CallBaseClassMethodsOnISerializableTypesMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(CallBaseClassMethodsOnISerializableTypesDescription)),
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