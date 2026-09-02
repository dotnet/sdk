// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow
{
    /// <summary>
    /// Marker interface for analysis results from execution of <see cref="DataFlowAnalysis"/> on a control flow graph.
    /// Primarily exists for specifying constraints on analysis result type parameters.
    /// </summary>
    public interface IDataFlowAnalysisResult<TAbstractAnalysisValue>
    {
        ControlFlowGraph ControlFlowGraph { get; }
        (TAbstractAnalysisValue Value, PredicateValueKind PredicateValueKind)? ReturnValueAndPredicateKind { get; }
        object? AnalysisDataForUnhandledThrowOperations { get; }
        object? TaskWrappedValuesMap { get; }
        LambdaAndLocalFunctionAnalysisInfo LambdaAndLocalFunctionAnalysisInfo { get; }
    }
}
