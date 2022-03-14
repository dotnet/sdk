// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
