// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Tasks
{
    /// <summary>CA2012: Use ValueTasks correctly.</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseValueTasksCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2012";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseValueTasksCorrectlyTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseValueTasksCorrectlyDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static readonly DiagnosticDescriptor GeneralRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseValueTasksCorrectlyMessage_General), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UnconsumedRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseValueTasksCorrectlyMessage_Unconsumed), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor DoubleConsumptionRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseValueTasksCorrectlyMessage_DoubleConsumption), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor AccessingIncompleteResultRule = DiagnosticDescriptorHelper.Create(RuleId,
            s_localizableTitle,
            new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseValueTasksCorrectlyMessage_AccessingIncompleteResult), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(GeneralRule, UnconsumedRule, DoubleConsumptionRule, AccessingIncompleteResultRule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var typeProvider = WellKnownTypeProvider.GetOrCreate(compilationContext.Compilation);

                // Get the target ValueTask / ValueTask<T> types. If they don't exist, nothing more to do.
                if (!typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask, out var valueTaskType) ||
                    !typeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask1, out var valueTaskOfTType))
                {
                    return;
                }

                // Get the type for System.Diagnostics.Debug. If we can't find it, that's ok, we just won't use it.
                var debugType = typeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsDebug);

                // Process all invocations. This analyzer works by finding all method invocations that return ValueTasks,
                // and then analyzing those invocations and their surroundings.
                compilationContext.RegisterOperationAction(operationContext =>
                {
                    var operation = operationContext.Operation;
                    var invocation = (IInvocationOperation)operation;

                    // Does the method return ValueTask?  If not, we're done.
                    if (!valueTaskType.Equals(invocation.TargetMethod.ReturnType) &&
                        !valueTaskOfTType.Equals(invocation.TargetMethod.ReturnType.OriginalDefinition))
                    {
                        return;
                    }

                    // The only method that returns a ValueTask on which we allow unlimited later consumption
                    // is ValueTask.Preserve. Use is rare, but special-case it. If this is Preserve, we're done.
                    if (invocation.TargetMethod.Name == nameof(ValueTask.Preserve) &&
                        (valueTaskType.Equals(invocation.TargetMethod.ContainingType) ||
                         valueTaskOfTType.Equals(invocation.TargetMethod.ContainingType.OriginalDefinition)))
                    {
                        return;
                    }

                    // If this is a method call off of the ValueTask, then check to see if it's special.
                    if (invocation.Parent is IInvocationOperation parentIo)
                    {
                        switch (parentIo.TargetMethod.Name)
                        {
                            case nameof(ValueTask.AsTask):
                            case nameof(ValueTask.Preserve):
                                // Using AsTask to convert to a Task is acceptable consumption.
                                // Use Preserve to enable subsequent unlimited consumption is acceptable.
                                return;

                            case nameof(ValueTask.GetAwaiter) when parentIo.Parent is IInvocationOperation { TargetMethod: { Name: nameof(ValueTaskAwaiter.GetResult) } }:
                                // Warn! Trying to block waiting for a value task isn't supported.
                                operationContext.ReportDiagnostic(invocation.CreateDiagnostic(AccessingIncompleteResultRule));
                                return;

                            case nameof(ValueTask.ConfigureAwait):
                                // ConfigureAwait returns another awaitable. Use that one instead for subsequent analysis.
                                operation = invocation = parentIo;
                                break;
                        }
                    }
                    else if (invocation.Parent is IPropertyReferenceOperation { Property: { Name: nameof(ValueTask<int>.Result) } })
                    {
                        operationContext.ReportDiagnostic(invocation.CreateDiagnostic(AccessingIncompleteResultRule));
                        return;
                    }
                    // Just let any other direct member access fall through to be a general warning.

                    while (operation.Parent != null) // for walking up the operation tree if necessary
                    {
                        switch (operation.Parent.Kind)
                        {
                            case OperationKind.Await:
                            case OperationKind.Return:
                                // The "99% case" is just awaiting the awaitable. Such usage is good.
                                // The "0.9% case" is delegating to and returning another call's returned awaitable. Also good.
                                return;

                            case OperationKind.Argument:
                                // The "0.09% case" is passing the result of a call directly as an argument to another method.
                                // This could later result in a problem, as now there's a parameter inside the callee that's
                                // holding on to the awaitable, and it could await it twice... but the caller is still correct,
                                // and this analyzer does not perform inter-method analysis.
                                var arg = (IArgumentOperation)operation.Parent;
                                var originalType = arg.Parameter.Type.OriginalDefinition;
                                if (originalType.Equals(valueTaskType) || originalType.Equals(valueTaskOfTType))
                                {
                                    // However, it's really only expected when the parameter type is explicitly a ValueTask{<T>};
                                    // if it's just, say, a TValue, we're likely on a bad path, such as storing the instance into
                                    // a collection of some kind, e.g. Dictionary<string, ValueTask>.Add(..., vt).
                                    var originalParameter = arg.Parameter.OriginalDefinition;
                                    if (originalParameter.Type.Kind != SymbolKind.TypeParameter)
                                    {
                                        return;
                                    }
                                }
                                goto default;

                            case OperationKind.ExpressionStatement:
                            case OperationKind.Discard:
                            case OperationKind.DiscardPattern:
                            case OperationKind.SimpleAssignment when operation.Parent is ISimpleAssignmentOperation sao && sao.Target is IDiscardOperation:
                                // Warn! This is a statement or discard. The result should have been used.
                                operationContext.ReportDiagnostic(invocation.CreateDiagnostic(UnconsumedRule));
                                return;

                            case OperationKind.Conversion:
                                var conversion = (IConversionOperation)operation.Parent;
                                if (conversion.Conversion.IsIdentity)
                                {
                                    // Ignore identity conversions, which can pop in from time to time.
                                    operation = operation.Parent;
                                    continue;
                                }
                                goto default;

                            // At this point, we're "in the weeds", but there are still some rare-but-used valid patterns to check for.

                            case OperationKind.Coalesce:
                            case OperationKind.Conditional:
                            case OperationKind.ConditionalAccess:
                            case OperationKind.SwitchExpression:
                            case OperationKind.SwitchExpressionArm:
                                // This is a ternary, null conditional, or switch expression, so consider the parent expression instead.
                                operation = operation.Parent;
                                continue;

                            default:
                                // Handle atypical / difficult cases that require more analysis.
                                HandleAtypicalValueTaskUsage(operationContext, debugType, operation, invocation);
                                return;
                        }
                    }
                }, OperationKind.Invocation);
            });
        }

        /// <summary>Handles more complicated analysis to warn on more complicated erroneous usage patterns.</summary>
        private static void HandleAtypicalValueTaskUsage(OperationAnalysisContext operationContext, INamedTypeSymbol? debugType, IOperation operation, IInvocationOperation invocation)
        {
            if (TryGetLocalSymbolAssigned(operation.Parent, out var valueTypeSymbol, out var startingBlock))
            {
                // At this point, it's very likely misuse and we could warn. However, there are a few advanced
                // patterns where the ValueTask might be stored into a local and very carefully used. As value
                // tasks are more likely to be used in more performance-sensitive code, we don't want a lot of
                // false positives from using such patterns in such code. So, we try to special-case these
                // advanced patterns by looking at the control flow graph and each block in it. Starting from
                // the entry block, we look to see if there's any "consumption" of the value task in a block. If
                // there are multiple consumptions in that block, it's an immediate error, as you can only consume
                // a value task once. If there's only one consumption, but we've already seen a consumption
                // on the path that got us here, then it's also erroneous. Otherwise, we keep following
                // the flow graph. If we try to jump to a block that's already had the awaiter consumed and
                // the current block did as well, that's also erroneous. Along the way, we further track
                // Debug.Assert(vt.Is*) calls, for cases where the developer wants to inform the reader/analyzer that
                // direct access to the result is known to be ok. This is a heuristic, and there are various things
                // that can thwart it, but it's relatively simple, handles most cases well, and minimizes both false
                // positives and false negatives (e.g. we're not tracking copying the local, but that's rare). This
                // part of the analysis is also expensive, but it should be executed only very rarely.

                // Dictionary to track blocks we've already seen. The TValue for each block is the merged
                // flow state for all paths we've followed into that block.
                var seen = new Dictionary<BasicBlock, FlowState>();

                // Stack of blocks still left to investigate, starting with the entry block. Each entry
                // is the block and its flow state at the time we pushed it onto the stack.
                var stack = new Stack<(BasicBlock, FlowState)>();
                stack.Push((startingBlock, default));

                // Process all blocks until their aren't any remaining.
                while (stack.Count > 0)
                {
                    // Get the next block to process. If we've already seen it, skip it.
                    (var block, var blockState) = stack.Pop();
                    if (seen.ContainsKey(block))
                    {
                        continue;
                    }

                    // Analyze the block. This involves:
                    // - Checking if there are any asserts in the block that declare the ValueTask to have already completed.
                    // - Counting how many "consumptions" of the ValueTask there are in the block.
                    int assertsCompletionIndex = FindFirstAssertsCompletion(block, valueTypeSymbol, debugType);
                    int consumptions = CountConsumptions(block, valueTypeSymbol);
                    blockState.ThisConsumption = consumptions == 1;

                    // Check if the analysis reveals a problem for this block.
                    if (consumptions > 1 || (blockState.ThisConsumption && blockState.PreviousConsumption))
                    {
                        // Warn! Either this block consumed it twice, or it consumed it after a previous
                        // block in the flow to here also consumed it.
                        operationContext.ReportDiagnostic(invocation.CreateDiagnostic(DoubleConsumptionRule));
                        return;
                    }
                    else if (!blockState.KnownCompletion && blockState.ThisConsumption)
                    {
                        // There weren't any assertions prior to this block, and this block does have a consumption,
                        // but we don't know what kind. If it's an await, no problem, but if it's a direct result access
                        // that requires knowing that the ValueTask completed, we need to get more specific and find the first
                        // operation that tries such a direct access.
                        int directAccessIndex = FindFirstDirectResultAccess(block, valueTypeSymbol);
                        if (directAccessIndex >= 0 && (assertsCompletionIndex == -1 || assertsCompletionIndex > directAccessIndex))
                        {
                            // Warn! We found a direct result access without any asserts before it to assert it was completed.
                            operationContext.ReportDiagnostic(invocation.CreateDiagnostic(AccessingIncompleteResultRule));
                            return;
                        }
                    }

                    // We've finished analyzing this block, so add it to our tracking dictionary.
                    seen.Add(block, blockState);

                    // We now to follow the successors. When we flow to them, we update their flow state to highlight
                    // whether there were any asserts in this block, such that they can also consider completion asserted.
                    bool setFallthroughKnownCompletion = assertsCompletionIndex != -1;
                    bool setConditionalKnownCompletion = setFallthroughKnownCompletion;

                    // If there's a conditional successor, we not only need to follow it, we need to evaluate the
                    // condition. That condition might be something like "if (vt.IsCompleted)", in which case
                    // we need to flow that completion knowledge (just as with asserts) into the relevant branch.
                    if (block.ConditionalSuccessor?.Destination is BasicBlock conditional)
                    {
                        if (!(setFallthroughKnownCompletion | setConditionalKnownCompletion) ||
                            block.BranchValue != null)
                        {
                            bool? completionCondition = OperationImpliesCompletion(valueTypeSymbol, block.BranchValue);
                            switch (block.ConditionKind)
                            {
                                case ControlFlowConditionKind.WhenTrue when completionCondition == true:
                                case ControlFlowConditionKind.WhenFalse when completionCondition == false:
                                    setConditionalKnownCompletion = true;
                                    break;
                                case ControlFlowConditionKind.WhenTrue when completionCondition == false:
                                case ControlFlowConditionKind.WhenFalse when completionCondition == true:
                                    setFallthroughKnownCompletion = true;
                                    break;
                            }
                        }

                        HandleSuccessor(conditional, setConditionalKnownCompletion);
                    }

                    // If there's a fallback successor, follow it as well. We computed any necessary completion
                    // value previously, so just pass that along.
                    if (block.FallThroughSuccessor?.Destination is BasicBlock fallthrough)
                    {
                        HandleSuccessor(fallthrough, setFallthroughKnownCompletion);
                    }

                    // Processes a successor, determining whether to push it onto the evaluation stack or update
                    // the seen dictionary with the additional information we've gathered.
                    void HandleSuccessor(BasicBlock successor, bool setKnownCompletion)
                    {
                        if (seen.TryGetValue(successor, out var successorConsumption))
                        {
                            // We've previously seen the successor block. Check its state.
                            if (blockState.ThisConsumption && !successorConsumption.PreviousConsumption)
                            {
                                if (successorConsumption.ThisConsumption)
                                {
                                    // Warn! The ValueTask was consumed on another path into this block, and this block
                                    // also consumes it.
                                    operationContext.ReportDiagnostic(invocation.CreateDiagnostic(DoubleConsumptionRule));
                                    return;
                                }

                                // Update its flow state accordingly; a previous path into it hadn't consumed the ValueTask,
                                // but this one did. In contrast, we don't update the KnownCompletion state, because that
                                // requires all paths into the block to have known completion.
                                successorConsumption.PreviousConsumption = true;
                                seen[successor] = successorConsumption;
                            }
                        }
                        else
                        {
                            // Push the successor onto the evaluation stack.
                            var successorState = blockState;
                            successorState.PreviousConsumption |= blockState.ThisConsumption;
                            successorState.KnownCompletion |= setKnownCompletion;
                            stack.Push((successor, successorState));
                        }
                    }
                }

                // Couldn't prove there was a problem, so err on the side of false negatives and don't warn.
                return;
            }

            // Warn! There was some very atypical consumption of the ValueTask.
            operationContext.ReportDiagnostic(invocation.CreateDiagnostic(GeneralRule));
        }

        /// <summary>Represents the flow state for a basic block.</summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
        private struct FlowState
#pragma warning restore CA1815
        {
            /// <summary>Gets or sets whether the ValueTask was known to have completed, either due to an assert or a condition proving it.</summary>
            public bool KnownCompletion { get; set; }
            /// <summary>Gets or sets whether a previous block in the flow consumed the ValueTask.</summary>
            public bool PreviousConsumption { get; set; }
            /// <summary>Gets or sets whether this block consumed the ValueTask.</summary>
            public bool ThisConsumption { get; set; }
        }

        private static bool TryGetLocalSymbolAssigned(IOperation? operation, [NotNullWhen(true)] out ISymbol? symbol, [NotNullWhen(true)] out BasicBlock? startingBlock)
        {
            ControlFlowGraph? cfg;
            switch (operation?.Kind)
            {
                case OperationKind.VariableInitializer when operation.Parent is IVariableDeclaratorOperation decl:
                    if (decl.TryGetEnclosingControlFlowGraph(out cfg))
                    {
                        symbol = decl.Symbol;
                        startingBlock = cfg.GetEntry();
                        return true;
                    }
                    break;

                case OperationKind.SimpleAssignment:
                    var assn = (ISimpleAssignmentOperation)operation;
                    if (assn.TryGetEnclosingControlFlowGraph(out cfg))
                    {
                        switch (assn.Target)
                        {
                            case ILocalReferenceOperation local:
                                symbol = local.Local;
                                startingBlock = cfg.GetEntry();
                                return true;

                            case IParameterReferenceOperation parameter:
                                symbol = parameter.Parameter;
                                startingBlock = cfg.GetEntry();
                                return true;
                        }
                    }
                    break;
            }

            symbol = null;
            startingBlock = null;
            return false;
        }

        /// <summary>Counts the number of operations in the block that represent a consumption of the ValueTask, such as awaiting it or calling GetAwaiter().GetResult().</summary>
        /// <param name="block">The block to be searched.</param>
        /// <param name="valueTaskSymbol">The ValueTask symbol for which we're searching.</param>
        /// <returns>
        /// The number of found consumption operations. This is generally 0 or 1, but could be greater than 1 in the case
        /// of bad usage; it stops counting at 2, as there's no need to differentiate any values greater than 1.
        /// </returns>
        private static int CountConsumptions(BasicBlock block, ISymbol valueTaskSymbol)
        {
            int count = 0;
            foreach (var op in block.DescendantOperations())
            {
                if (IsLocalOrParameterSymbolReference(op, valueTaskSymbol) &&
                    op.Parent?.Kind switch
                    {
                        OperationKind.Await => true,
                        OperationKind.Return => true,
                        OperationKind.Argument => true,
                        OperationKind.Invocation => true, // e.g. AsTask()
                        OperationKind.PropertyReference when op.Parent is IPropertyReferenceOperation { Property: { Name: "Result" } } => true,
                        _ => false
                    })
                {
                    if (++count > 1)
                    {
                        // The only relevant values are 0 (no consumptions), 1 (valid consumption), and > 1 (too many consumptions in the same block).
                        // As such, we can stop iterating when we hit > 1.
                        break;
                    }
                }
            }

            return count;
        }

        /// <summary>Finds the first expression statement in the block that does Debug.Assert(vt.Is*).</summary>
        /// <param name="block">The block to be searched.</param>
        /// <param name="valueTaskSymbol">The ValueTask symbol for which we're searching.</param>
        /// <param name="debugType">The type of System.Diagnostics.Debug.</param>
        /// <returns>The index of the first Debug.Assert(vt.Is*) statement, or -1 if none was found.</returns>
        private static int FindFirstAssertsCompletion(BasicBlock block, ISymbol valueTaskSymbol, INamedTypeSymbol? debugType)
        {
            if (debugType != null)
            {
                for (var i = 0; i < block.Operations.Length; i++)
                {
                    if (block.Operations[i] is IExpressionStatementOperation stmt &&
                        stmt.Operation is IInvocationOperation assert &&
                        assert.TargetMethod?.Name == nameof(Debug.Assert) &&
                        assert.TargetMethod.ContainingType.Equals(debugType) &&
                        !assert.Arguments.IsEmpty &&
                        OperationImpliesCompletion(valueTaskSymbol, assert.Arguments[0].Value) == true)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>Finds the first operation in the block containing a direct access to a ValueTask's result, e.g. GetAwaiter().GetResult().</summary>
        /// <param name="block">The block to be searched.</param>
        /// <param name="valueTaskSymbol">The ValueTask symbol for which we're searching.</param>
        /// <returns>The index found, or -1 if none could be found.</returns>
        private static int FindFirstDirectResultAccess(BasicBlock block, ISymbol valueTaskSymbol)
        {
            // First search the body of the block.
            var operations = block.Operations;
            for (int i = 0; i < operations.Length; i++)
            {
                foreach (var op in operations[i].DescendantsAndSelf())
                {
                    if (HasDirectResultAccess(op))
                    {
                        return i;
                    }
                }
            }

            // Also search the branch value.
            if (block.BranchValue != null)
            {
                foreach (var op in block.BranchValue.DescendantsAndSelf())
                {
                    if (HasDirectResultAccess(op))
                    {
                        return operations.Length;
                    }
                }
            }

            return -1;

            // Determines if the operation itself is a direct access to the ValueTask's Result or GetAwaiter().GetResult().
            bool HasDirectResultAccess(IOperation op) =>
                IsLocalOrParameterSymbolReference(op, valueTaskSymbol) &&
                op.Parent?.Kind switch
                {
                    OperationKind.PropertyReference when op.Parent is IPropertyReferenceOperation { Property: { Name: nameof(ValueTask<int>.Result) } } => true,
                    OperationKind.Invocation when op.Parent is IInvocationOperation { TargetMethod: { Name: nameof(ValueTask.GetAwaiter) } } => true,
                    _ => false
                };
        }

        /// <summary>Gets whether <paramref name="op"/> implies that the ValueTask has completed.</summary>
        /// <param name="valueTaskSymbol">The ValueTask symbol for which we're searching.</param>
        /// <param name="op">The operation to examine.</param>
        /// <returns>true if the operation implies the ValueTask has completed, false if the operation implies the ValueTask has not completed, and null if it's undetermined.</returns>
        private static bool? OperationImpliesCompletion(ISymbol valueTaskSymbol, IOperation? op)
        {
            if (op != null)
            {
                switch (op.Kind)
                {
                    case OperationKind.PropertyReference when IsCompletedReference(op as IPropertyReferenceOperation):
                        return true;

                    case OperationKind.Unary when op is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } unary && IsCompletedReference(unary.Operand as IPropertyReferenceOperation):
                        return false;
                }
            }

            return null;

            bool IsCompletedReference(IPropertyReferenceOperation? prop) =>
                prop != null &&
                IsLocalOrParameterSymbolReference(prop.Instance, valueTaskSymbol) &&
                prop.Property.Name switch
                {
                    nameof(ValueTask.IsCompleted) => true,
                    nameof(ValueTask.IsCompletedSuccessfully) => true,
                    nameof(ValueTask.IsFaulted) => true,
                    nameof(ValueTask.IsCanceled) => true,
                    _ => false
                };
        }

        private static bool IsLocalOrParameterSymbolReference(IOperation op, ISymbol valueTaskSymbol) =>
            op?.Kind switch
            {
                OperationKind.LocalReference => ((ILocalReferenceOperation)op).Local.Equals(valueTaskSymbol),
                OperationKind.ParameterReference => ((IParameterReferenceOperation)op).Parameter.Equals(valueTaskSymbol),
                _ => false,
            };
    }
}
