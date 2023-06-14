// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    using GlobalFlowStateDictionaryAnalysisData = DictionaryAnalysisData<AnalysisEntity, GlobalFlowStateDictionaryAnalysisValue>;

    internal class GlobalFlowStateDictionaryBlockAnalysisResult(BasicBlock basicBlock, GlobalFlowStateDictionaryAnalysisData data) : AbstractBlockAnalysisResult(basicBlock)
    {
        public ImmutableDictionary<AnalysisEntity, GlobalFlowStateDictionaryAnalysisValue> Data { get; } = data.ToImmutableDictionary();
    }
}
