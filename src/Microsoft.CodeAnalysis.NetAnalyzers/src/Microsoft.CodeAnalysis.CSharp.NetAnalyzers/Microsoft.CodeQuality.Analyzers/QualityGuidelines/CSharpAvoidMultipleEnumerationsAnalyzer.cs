// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed partial class CSharpAvoidMultipleEnumerationsAnalyzer : AvoidMultipleEnumerations
    {
        protected override GlobalFlowStateDictionaryFlowOperationVisitor CreateOperationVisitor(
            GlobalFlowStateDictionaryAnalysisContext context,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
            => new CSharpInvocationCountValueSetFlowStateDictionaryFlowOperationVisitor(
                context,
                wellKnownSymbolsInfo);

        protected override AvoidMultipleEnumerationsHelper AvoidMultipleEnumerationsHelper { get; } = CSharpAvoidMultipleEnumerationsHelper.Instance;
    }
}