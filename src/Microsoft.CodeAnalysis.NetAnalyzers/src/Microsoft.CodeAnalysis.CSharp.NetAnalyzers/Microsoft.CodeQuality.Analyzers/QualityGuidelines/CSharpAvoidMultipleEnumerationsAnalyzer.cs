// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities.FlowAnalysis.Analysis.InvocationCountAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed partial class CSharpAvoidMultipleEnumerationsAnalyzer : AvoidMultipleEnumerations
    {
        internal override InvocationCountDataFlowOperationVisitor CreateOperationVisitor(
            InvocationCountAnalysisContext context,
            ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
            ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
            IMethodSymbol? getEnumeratorMethod)
            => new CSharpInvocationCountValueSetFlowOperationVisitor(context, wellKnownDelayExecutionMethods, wellKnownEnumerationMethods, getEnumeratorMethod);
    }
}