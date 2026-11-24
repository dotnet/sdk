// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Analyzer.Utilities.Extensions
{
    internal static partial class OperationBlocksExtensions
    {
        public static ControlFlowGraph? GetControlFlowGraph(this ImmutableArray<IOperation> operationBlocks)
        {
            foreach (var operationRoot in operationBlocks)
            {
                if (operationRoot.TryGetEnclosingControlFlowGraph(out var cfg))
                {
                    return cfg;
                }
            }

            return null;
        }

        public static ControlFlowGraph? GetControlFlowGraph(this OperationBlockAnalysisContext context)
            => GetControlFlowGraph(context.OperationBlocks, context, static (state, root) => state.GetControlFlowGraph(root));

#pragma warning disable RS1012 // Start action has no registered actions.
        public static ControlFlowGraph? GetControlFlowGraph(this OperationBlockStartAnalysisContext context)
            => GetControlFlowGraph(context.OperationBlocks, context, static (state, root) => state.GetControlFlowGraph(root));
#pragma warning restore RS1012 // Start action has no registered actions.

        private static ControlFlowGraph? GetControlFlowGraph<TContext>(
            ImmutableArray<IOperation> operationBlocks, TContext context, Func<TContext, IOperation, ControlFlowGraph> getControlFlowGraph)
        {
            foreach (var operationRoot in operationBlocks)
            {
                if (operationRoot.TryGetEnclosingControlFlowGraph(out var cfg,
                    (context, operationRoot, getControlFlowGraph),
                    static state => state.getControlFlowGraph(state.context, state.operationRoot)))
                {
                    return cfg;
                }
            }

            return null;
        }
    }
}
