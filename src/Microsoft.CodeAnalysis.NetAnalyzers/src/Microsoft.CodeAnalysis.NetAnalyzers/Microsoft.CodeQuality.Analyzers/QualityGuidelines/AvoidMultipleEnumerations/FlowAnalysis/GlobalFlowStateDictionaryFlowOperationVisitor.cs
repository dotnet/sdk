// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
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

        public override GlobalFlowStateDictionaryAnalysisData GetEmptyAnalysisData()
            => new();

        public sealed override (GlobalFlowStateDictionaryAnalysisData output, bool isFeasibleBranch) FlowBranch(BasicBlock fromBlock, BranchWithInfo branch, GlobalFlowStateDictionaryAnalysisData input)
        {
            EnsureInitialized(input);
            return base.FlowBranch(fromBlock, branch, input);
        }

        protected override bool Equals(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2)
            => GlobalFlowStateDictionaryAnalysis.Domain.Equals(value1, value2);

        protected override GlobalFlowStateDictionaryAnalysisValue GetAbstractDefaultValue(ITypeSymbol type)
            => GlobalFlowStateDictionaryAnalysisValue.Empty;

        protected override GlobalFlowStateDictionaryAnalysisData GetClonedAnalysisData(GlobalFlowStateDictionaryAnalysisData analysisData)
            => new(analysisData);

        protected override GlobalFlowStateDictionaryAnalysisData GetExitBlockOutputData(GlobalFlowStateDictionaryAnalysisResult analysisResult)
            => new(analysisResult.EntryBlockOutput.Data);

        protected override GlobalFlowStateDictionaryAnalysisData GetTrimmedCurrentAnalysisData(IEnumerable<AnalysisEntity> withEntities)
            => GetTrimmedCurrentAnalysisDataHelper(withEntities, CurrentAnalysisData, SetAbstractValue);

        protected override GlobalFlowStateDictionaryAnalysisData MergeAnalysisData(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2)
            => GlobalFlowStateDictionaryAnalysis.Domain.Merge(value1, value2);

        protected sealed override GlobalFlowStateDictionaryAnalysisData MergeAnalysisData(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2, BasicBlock forBlock)
            => HasPredicatedGlobalState && forBlock.DominatesPredecessors(DataFlowAnalysisContext.ControlFlowGraph) ?
            GlobalFlowStateDictionaryAnalysis.Domain.Intersect(value1, value2, GlobalFlowStateDictionaryAnalysisValueDomain.Intersect) :
            GlobalFlowStateDictionaryAnalysis.Domain.Merge(value1, value2);

        protected override GlobalFlowStateDictionaryAnalysisData MergeAnalysisDataForBackEdge(GlobalFlowStateDictionaryAnalysisData value1, GlobalFlowStateDictionaryAnalysisData value2, BasicBlock forBlock)
            => GlobalFlowStateDictionaryAnalysis.Domain.Merge(value1, value2);

        protected override void SetAbstractValue(AnalysisEntity analysisEntity, GlobalFlowStateDictionaryAnalysisValue value)
            => SetAbstractValue(CurrentAnalysisData, analysisEntity, value);

        private static void SetAbstractValue(GlobalFlowStateDictionaryAnalysisData analysisData, AnalysisEntity analysisEntity, GlobalFlowStateDictionaryAnalysisValue value)
        {
            if (value.Kind == GlobalFlowStateDictionaryAnalysisValueKind.Known)
            {
                analysisData[analysisEntity] = value;
            }
        }
    }
}
