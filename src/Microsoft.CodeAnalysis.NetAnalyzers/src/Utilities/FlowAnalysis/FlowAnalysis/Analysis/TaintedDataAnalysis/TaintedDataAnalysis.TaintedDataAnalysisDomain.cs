// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis
{
    using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

    internal partial class TaintedDataAnalysis
    {
        private sealed class TaintedDataAnalysisDomain : PredicatedAnalysisDataDomain<TaintedDataAnalysisData, TaintedDataAbstractValue>
        {
            public TaintedDataAnalysisDomain(MapAbstractDomain<AnalysisEntity, TaintedDataAbstractValue> coreDataAnalysisDomain)
                : base(coreDataAnalysisDomain)
            {
            }
        }
    }
}