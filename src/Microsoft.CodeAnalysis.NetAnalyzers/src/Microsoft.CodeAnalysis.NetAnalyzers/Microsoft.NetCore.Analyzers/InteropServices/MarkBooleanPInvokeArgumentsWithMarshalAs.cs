// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    /// <summary>
    /// CA1414: Mark boolean PInvoke arguments with MarshalAs
    /// </summary>
    public abstract class MarkBooleanPInvokeArgumentsWithMarshalAsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1414";

        /*private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(MarkBooleanPInvokeArgumentsWithMarshalAsTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(MarkBooleanPInvokeArgumentsWithMarshalAsDescription));

        internal static readonly DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(MarkBooleanPInvokeArgumentsWithMarshalAsMessageDefault)),
            DiagnosticCategory.Interoperability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);
        internal static readonly DiagnosticDescriptor ReturnRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(MarkBooleanPInvokeArgumentsWithMarshalAsMessageReturn)),
            DiagnosticCategory.Interoperability,
            RuleLevel.Disabled,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInFxCopAnalyzers: false);*/

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray<DiagnosticDescriptor>.Empty;
        //ImmutableArray.Create(DefaultRule, ReturnRule);

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