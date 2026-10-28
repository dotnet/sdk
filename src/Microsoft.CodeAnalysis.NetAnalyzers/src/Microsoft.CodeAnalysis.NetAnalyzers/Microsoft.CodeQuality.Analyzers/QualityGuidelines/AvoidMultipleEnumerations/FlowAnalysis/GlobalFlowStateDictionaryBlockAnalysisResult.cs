// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    using GlobalFlowStateDictionaryAnalysisData = DictionaryAnalysisData<AnalysisEntity, GlobalFlowStateDictionaryAnalysisValue>;

    internal class GlobalFlowStateDictionaryBlockAnalysisResult : AbstractBlockAnalysisResult
    {
        public GlobalFlowStateDictionaryBlockAnalysisResult(BasicBlock basicBlock, GlobalFlowStateDictionaryAnalysisData data) : base(basicBlock)
        {
            Data = data.ToImmutableDictionary();
        }

        public ImmutableDictionary<AnalysisEntity, GlobalFlowStateDictionaryAnalysisValue> Data { get; }
    }
}
