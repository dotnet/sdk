// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    using GlobalFlowStateDictionaryAnalysisData = DictionaryAnalysisData<AnalysisEntity, GlobalFlowStateDictionaryAnalysisValue>;
    using GlobalFlowStateDictionaryAnalysisResult = DataFlowAnalysisResult<GlobalFlowStateDictionaryBlockAnalysisResult, GlobalFlowStateDictionaryAnalysisValue>;

    internal abstract class GlobalFlowStateDictionaryFlowOperationVisitor : GlobalFlowStateDataFlowOperationVisitor<
        GlobalFlowStateDictionaryAnalysisContext,
        GlobalFlowStateDictionaryAnalysisResult,
        GlobalFlowStateDictionaryAnalysisValue>
    {
        protected GlobalFlowStateDictionaryFlowOperationVisitor(
            GlobalFlowStateDictionaryAnalysisContext analysisContext) : base(analysisContext, true)
        {
        }

        protected override bool Equals(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2)
            => GlobalFlowStateDictionaryAnalysis.Domain.Equals(value1, value2);

        protected override GlobalFlowStateDictionaryAnalysisValue GetAbstractDefaultValue(ITypeSymbol? type)
            => GlobalFlowStateDictionaryAnalysisValue.Empty;

        protected override GlobalFlowStateDictionaryAnalysisData GetExitBlockOutputData(GlobalFlowStateDictionaryAnalysisResult analysisResult)
            => new(analysisResult.EntryBlockOutput.Data);

        protected override GlobalFlowStateDictionaryAnalysisData MergeAnalysisData(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2)
            => GlobalFlowStateDictionaryAnalysis.Domain.Merge(value1, value2);

        protected sealed override GlobalFlowStateDictionaryAnalysisData MergeAnalysisData(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2, BasicBlock forBlock)
            => HasPredicatedGlobalState && forBlock.DominatesPredecessors(DataFlowAnalysisContext.ControlFlowGraph) ?
            GlobalFlowStateDictionaryAnalysis.Domain.Intersect(value1, value2, GlobalFlowStateDictionaryAnalysisValueDomain.Intersect) :
            GlobalFlowStateDictionaryAnalysis.Domain.Merge(value1, value2);

        protected override GlobalFlowStateDictionaryAnalysisData MergeAnalysisDataForBackEdge(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2, BasicBlock forBlock)
            => GlobalFlowStateDictionaryAnalysis.Domain.Merge(value1, value2);

        protected sealed override void SetAbstractValue(GlobalFlowStateDictionaryAnalysisData analysisData, AnalysisEntity analysisEntity, GlobalFlowStateDictionaryAnalysisValue value)
        {
            if (value.Kind is GlobalFlowStateDictionaryAnalysisValueKind.Known or GlobalFlowStateDictionaryAnalysisValueKind.Empty)
            {
                analysisData[analysisEntity] = value;
            }
        }
    }
}
