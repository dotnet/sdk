// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities.FlowAnalysis.Analysis.PropertySetAnalysis
{
    using PropertySetAnalysisData = DictionaryAnalysisData<AbstractLocation, PropertySetAbstractValue>;
    using PropertySetAnalysisDomain = MapAbstractDomain<AbstractLocation, PropertySetAbstractValue>;
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;

    /// <summary>
    /// Dataflow analysis to track <see cref="PropertySetAbstractValue"/> of <see cref="AbstractLocation"/>/<see cref="IOperation"/> instances.
    /// </summary>
    internal partial class PropertySetAnalysis : ForwardDataFlowAnalysis<PropertySetAnalysisData, PropertySetAnalysisContext, PropertySetAnalysisResult, PropertySetBlockAnalysisResult, PropertySetAbstractValue>
    {
        // The engine defaults to three nested method calls; stay conservative if an unusually long
        // source chain exceeds this larger prepass bound.
        private const int MaxPrepassCallDepth = 32;

        public static readonly PropertySetAnalysisDomain PropertySetAnalysisDomainInstance = new(PropertySetAbstractValueDomain.Default);

        private PropertySetAnalysis(PropertySetAnalysisDomain analysisDomain, PropertySetDataFlowOperationVisitor operationVisitor)
            : base(analysisDomain, operationVisitor)
        {
        }

        /// <summary>
        /// Analyzers should use BatchGetOrComputeHazardousUsages instead. Gets hazardous usages of an object based on a set of its properties.
        /// </summary>
        /// <param name="cfg">Control flow graph of the code.</param>
        /// <param name="compilation">Compilation containing the code.</param>
        /// <param name="owningSymbol">Symbol of the code to examine.</param>
        /// <param name="typeToTrackMetadataNames">Names of the types to track.</param>
        /// <param name="constructorMapper">How constructor invocations map to <see cref="PropertySetAbstractValueKind"/>s.</param>
        /// <param name="propertyMappers">How property assignments map to <see cref="PropertySetAbstractValueKind"/>.</param>
        /// <param name="hazardousUsageEvaluators">When and how to evaluate <see cref="PropertySetAbstractValueKind"/>s to for hazardous usages.</param>
        /// <param name="interproceduralAnalysisConfig">Interprocedural dataflow analysis configuration.</param>
        /// <param name="pessimisticAnalysis">Whether to be pessimistic.</param>
        /// <param name="onValueContentAnalysis">Optional observer for value-content analysis.</param>
        /// <returns>Property set analysis result.</returns>
        internal static PropertySetAnalysisResult? GetOrComputeResult(
            ControlFlowGraph cfg,
            Compilation compilation,
            ISymbol owningSymbol,
            AnalyzerOptions analyzerOptions,
            ImmutableHashSet<string> typeToTrackMetadataNames,
            ConstructorMapper constructorMapper,
            PropertyMapperCollection propertyMappers,
            HazardousUsageEvaluatorCollection hazardousUsageEvaluators,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis = false,
            Action? onValueContentAnalysis = null)
        {
            if (constructorMapper == null)
            {
                throw new ArgumentNullException(nameof(constructorMapper));
            }

            if (propertyMappers == null)
            {
                throw new ArgumentNullException(nameof(propertyMappers));
            }

            if (hazardousUsageEvaluators == null)
            {
                throw new ArgumentNullException(nameof(hazardousUsageEvaluators));
            }

            constructorMapper.Validate(propertyMappers.PropertyValuesCount);

            var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

            PointsToAnalysisResult? pointsToAnalysisResult;
            ValueContentAnalysisResult? valueContentAnalysisResult;
            ImmutableHashSet<INamedTypeSymbol> valueContentConstructorTypes = ImmutableHashSet<INamedTypeSymbol>.Empty;
            bool requiresValueContentAnalysis = false;
            if (constructorMapper.RequiresValueContentAnalysis)
            {
                var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                foreach (string typeName in typeToTrackMetadataNames)
                {
                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(typeName, out INamedTypeSymbol? type))
                    {
                        requiresValueContentAnalysis = true;
                        break;
                    }

                    builder.Add(type);
                }

                valueContentConstructorTypes = builder.ToImmutable();
            }

            // Mapped writes in reachable helpers and branch predicates can both require value-content flow.
            requiresValueContentAnalysis |=
                (constructorMapper.RequiresValueContentAnalysis || propertyMappers.RequiresValueContentAnalysis) &&
                MayRequireValueContentAnalysis(cfg.OriginalOperation, compilation, constructorMapper, propertyMappers,
                    valueContentConstructorTypes);
            if (!requiresValueContentAnalysis)
            {
                pointsToAnalysisResult = PointsToAnalysis.TryGetOrComputeResult(
                    cfg,
                    owningSymbol,
                    analyzerOptions,
                    wellKnownTypeProvider,
                    PointsToAnalysisKind.Complete,
                    interproceduralAnalysisConfig,
                    interproceduralAnalysisPredicate: null,
                    pessimisticAnalysis,
                    performCopyAnalysis: false);
                if (pointsToAnalysisResult == null)
                {
                    return null;
                }

                valueContentAnalysisResult = null;
            }
            else
            {
                onValueContentAnalysis?.Invoke();
                valueContentAnalysisResult = ValueContentAnalysis.TryGetOrComputeResult(
                    cfg,
                    owningSymbol,
                    analyzerOptions,
                    wellKnownTypeProvider,
                    PointsToAnalysisKind.Complete,
                    interproceduralAnalysisConfig,
                    out var copyAnalysisResult,
                    out pointsToAnalysisResult,
                    pessimisticAnalysis,
                    performCopyAnalysis: false);
                if (valueContentAnalysisResult == null)
                {
                    return null;
                }
            }

            var analysisContext = PropertySetAnalysisContext.Create(
                PropertySetAbstractValueDomain.Default,
                wellKnownTypeProvider,
                cfg,
                owningSymbol,
                analyzerOptions,
                interproceduralAnalysisConfig,
                pessimisticAnalysis,
                pointsToAnalysisResult,
                valueContentAnalysisResult,
                TryGetOrComputeResultForAnalysisContext,
                typeToTrackMetadataNames,
                constructorMapper,
                propertyMappers,
                hazardousUsageEvaluators);
            var result = TryGetOrComputeResultForAnalysisContext(analysisContext);
            return result;
        }

        private static bool MayRequireValueContentAnalysis(
            IOperation root,
            Compilation compilation,
            ConstructorMapper constructorMapper,
            PropertyMapperCollection propertyMappers,
            ImmutableHashSet<INamedTypeSymbol> valueContentConstructorTypes)
        {
            using PooledHashSet<IMethodSymbol> visitingMethods = PooledHashSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default);
            using PooledDictionary<IMethodSymbol, bool> cache = PooledDictionary<IMethodSymbol, bool>.GetInstance(SymbolEqualityComparer.Default);
            return MayRequireValueContentAnalysis(root, compilation, constructorMapper, propertyMappers,
                valueContentConstructorTypes, cache, visitingMethods);
        }

        private static bool MayRequireValueContentAnalysis(
            IOperation root,
            Compilation compilation,
            ConstructorMapper constructorMapper,
            PropertyMapperCollection propertyMappers,
            ImmutableHashSet<INamedTypeSymbol> valueContentConstructorTypes,
            PooledDictionary<IMethodSymbol, bool> cache,
            PooledHashSet<IMethodSymbol> visitingMethods)
        {
            foreach (IOperation operation in root.DescendantsAndSelf())
            {
                switch (operation)
                {
                    case IPropertyReferenceOperation property:
                        if ((propertyMappers.TryGetPropertyMapper(property.Property.Name, out PropertyMapper? mapper, out _) &&
                                mapper.RequiresValueContentAnalysis) ||
                            (property.Property.GetMethod is IMethodSymbol getter &&
                                MethodMayRequireValueContentAnalysis(getter, compilation, constructorMapper, propertyMappers,
                                    valueContentConstructorTypes, cache, visitingMethods)) ||
                            (property.Property.SetMethod is IMethodSymbol setter &&
                                MethodMayRequireValueContentAnalysis(setter, compilation, constructorMapper, propertyMappers,
                                    valueContentConstructorTypes, cache, visitingMethods)))
                        {
                            return true;
                        }

                        break;

                    case IPropertyInitializerOperation initializer:
                        if (initializer.InitializedProperties.Any(property =>
                            propertyMappers.TryGetPropertyMapper(property.Name, out PropertyMapper? mapper, out _) &&
                            mapper.RequiresValueContentAnalysis))
                        {
                            return true;
                        }

                        break;

                    case IInvocationOperation invocation:
                        if ((invocation.TargetMethod.MethodKind == MethodKind.Constructor &&
                                valueContentConstructorTypes.Any(type =>
                                    invocation.TargetMethod.ContainingType.GetBaseTypesAndThis().Contains(type)) &&
                                constructorMapper.MapWithoutValueContent?.Invoke(invocation.TargetMethod) is null) ||
                            MethodMayRequireValueContentAnalysis(invocation.TargetMethod, compilation, constructorMapper, propertyMappers,
                                valueContentConstructorTypes, cache, visitingMethods))
                        {
                            return true;
                        }

                        break;

                    case IObjectCreationOperation creation when creation.Constructor is IMethodSymbol constructor:
                        if ((creation.Type is ITypeSymbol createdType &&
                                valueContentConstructorTypes.Any(type => createdType.GetBaseTypesAndThis().Contains(type)) &&
                                constructorMapper.MapWithoutValueContent?.Invoke(constructor) is null) ||
                            MethodMayRequireValueContentAnalysis(constructor, compilation, constructorMapper, propertyMappers,
                                valueContentConstructorTypes, cache, visitingMethods))
                        {
                            return true;
                        }

                        break;

                    case IMethodReferenceOperation reference:
                        if (MethodMayRequireValueContentAnalysis(reference.Method, compilation, constructorMapper, propertyMappers,
                            valueContentConstructorTypes, cache, visitingMethods))
                        {
                            return true;
                        }

                        break;

                    case IDynamicInvocationOperation:
                    case IForEachLoopOperation:
                    case IUsingOperation:
                    case IUsingDeclarationOperation:
                    case IDeconstructionAssignmentOperation:
                    case ILockOperation lockOperation when lockOperation.LockedValue.Type is ITypeSymbol lockedType &&
                        SymbolEqualityComparer.Default.Equals(lockedType, compilation.GetTypeByMetadataName("System.Threading.Lock")):
                    case IConditionalOperation conditional when !conditional.Condition.ConstantValue.HasValue:
                    case ILoopOperation:
                    case ISwitchOperation:
                    case ISwitchExpressionOperation:
                    case IConditionalAccessOperation:
                    case ICoalesceOperation:
                    case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr }:
                    case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.And or BinaryOperatorKind.Or }:
                    case IDynamicMemberReferenceOperation:
                    case IDynamicIndexerAccessOperation:
                    case IDynamicObjectCreationOperation:
                    case IConversionOperation { OperatorMethod: not null }:
                    case IBinaryOperation { OperatorMethod: not null }:
                    case IUnaryOperation { OperatorMethod: not null }:
                    case ICompoundAssignmentOperation { OperatorMethod: not null }:
                    case IIncrementOrDecrementOperation { OperatorMethod: not null }:
                        return true;
                }
            }

            return false;
        }

        private static bool MethodMayRequireValueContentAnalysis(
            IMethodSymbol method,
            Compilation compilation,
            ConstructorMapper constructorMapper,
            PropertyMapperCollection propertyMappers,
            ImmutableHashSet<INamedTypeSymbol> valueContentConstructorTypes,
            PooledDictionary<IMethodSymbol, bool> cache,
            PooledHashSet<IMethodSymbol> visitingMethods)
        {
            if (method.MethodKind == MethodKind.DelegateInvoke)
            {
                return true;
            }

            if (!SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, compilation.Assembly))
            {
                return false;
            }

            if (method.IsAbstract || method.IsVirtual || method.IsOverride)
            {
                return true;
            }

            if (method.IsImplicitlyDeclared)
            {
                return false;
            }

            method = method.OriginalDefinition;
            if (cache.TryGetValue(method, out bool needsValueContent))
            {
                return needsValueContent;
            }

            if (visitingMethods.Count >= MaxPrepassCallDepth || !visitingMethods.Add(method))
            {
                return true;
            }

            try
            {
                IBlockOperation? block = method.GetTopmostOperationBlock(compilation);
                needsValueContent = block is null ||
                    MayRequireValueContentAnalysis(block.GetRoot(), compilation, constructorMapper, propertyMappers, valueContentConstructorTypes,
                        cache, visitingMethods);
                cache.Add(method, needsValueContent);
                return needsValueContent;
            }
            finally
            {
                visitingMethods.Remove(method);
            }
        }

        /// <summary>
        /// Gets hazardous usages of an object based on a set of its properties.
        /// </summary>
        /// <param name="compilation">Compilation containing the code.</param>
        /// <param name="rootOperationsNeedingAnalysis">Root operations of code blocks to analyze.</param>
        /// <param name="typeToTrackMetadataName">Name of the type to track.</param>
        /// <param name="constructorMapper">How constructor invocations map to <see cref="PropertySetAbstractValueKind"/>s.</param>
        /// <param name="propertyMappers">How property assignments map to <see cref="PropertySetAbstractValueKind"/>.</param>
        /// <param name="hazardousUsageEvaluators">When and how to evaluate <see cref="PropertySetAbstractValueKind"/>s to for hazardous usages.</param>
        /// <param name="interproceduralAnalysisConfig">Interprocedural dataflow analysis configuration.</param>
        /// <param name="pessimisticAnalysis">Whether to be pessimistic.</param>
        /// <param name="onValueContentAnalysis">Optional observer for value-content analysis.</param>
        /// <returns>Dictionary of <see cref="Location"/> and <see cref="IMethodSymbol"/> pairs mapping to the kind of hazardous usage (Flagged or MaybeFlagged).  The method in the key is null for return/initialization statements.</returns>
        /// <remarks>Unlike <see cref="GetOrComputeResult"/>, this overload also performs DFA on all descendant local and anonymous functions.</remarks>
        public static PooledDictionary<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult>? BatchGetOrComputeHazardousUsages(
            Compilation compilation,
            IEnumerable<(IOperation Operation, ISymbol ContainingSymbol)> rootOperationsNeedingAnalysis,
            AnalyzerOptions analyzerOptions,
            string typeToTrackMetadataName,
            ConstructorMapper constructorMapper,
            PropertyMapperCollection propertyMappers,
            HazardousUsageEvaluatorCollection hazardousUsageEvaluators,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis = false,
            Action? onValueContentAnalysis = null)
        {
            return BatchGetOrComputeHazardousUsages(
                compilation,
                rootOperationsNeedingAnalysis,
                analyzerOptions,
                new string[] { typeToTrackMetadataName }.ToImmutableHashSet(),
                constructorMapper,
                propertyMappers,
                hazardousUsageEvaluators,
                interproceduralAnalysisConfig,
                pessimisticAnalysis,
                onValueContentAnalysis);
        }

        /// <summary>
        /// Gets hazardous usages of an object based on a set of its properties.
        /// </summary>
        /// <param name="compilation">Compilation containing the code.</param>
        /// <param name="rootOperationsNeedingAnalysis">Root operations of code blocks to analyze.</param>
        /// <param name="typeToTrackMetadataNames">Names of the types to track.</param>
        /// <param name="constructorMapper">How constructor invocations map to <see cref="PropertySetAbstractValueKind"/>s.</param>
        /// <param name="propertyMappers">How property assignments map to <see cref="PropertySetAbstractValueKind"/>.</param>
        /// <param name="hazardousUsageEvaluators">When and how to evaluate <see cref="PropertySetAbstractValueKind"/>s to for hazardous usages.</param>
        /// <param name="interproceduralAnalysisConfig">Interprocedural dataflow analysis configuration.</param>
        /// <param name="pessimisticAnalysis">Whether to be pessimistic.</param>
        /// <param name="onValueContentAnalysis">Optional observer for value-content analysis.</param>
        /// <returns>Dictionary of <see cref="Location"/> and <see cref="IMethodSymbol"/> pairs mapping to the kind of hazardous usage (Flagged or MaybeFlagged).  The method in the key is null for return/initialization statements.</returns>
        /// <remarks>Unlike <see cref="GetOrComputeResult"/>, this overload also performs DFA on all descendant local and anonymous functions.</remarks>
        public static PooledDictionary<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult>? BatchGetOrComputeHazardousUsages(
            Compilation compilation,
            IEnumerable<(IOperation Operation, ISymbol ContainingSymbol)> rootOperationsNeedingAnalysis,
            AnalyzerOptions analyzerOptions,
            ImmutableHashSet<string> typeToTrackMetadataNames,
            ConstructorMapper constructorMapper,
            PropertyMapperCollection propertyMappers,
            HazardousUsageEvaluatorCollection hazardousUsageEvaluators,
            InterproceduralAnalysisConfiguration interproceduralAnalysisConfig,
            bool pessimisticAnalysis = false,
            Action? onValueContentAnalysis = null)
        {
            PooledDictionary<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult>? allResults = null;
            foreach ((IOperation Operation, ISymbol ContainingSymbol) in rootOperationsNeedingAnalysis)
            {
                var success = Operation.TryGetEnclosingControlFlowGraph(out ControlFlowGraph? enclosingControlFlowGraph);
                Debug.Assert(success);
                if (enclosingControlFlowGraph == null)
                {
                    Debug.Fail("Expected non-null CFG");
                    continue;
                }

                PropertySetAnalysisResult? enclosingResult = InvokeDfaAndAccumulateResults(
                    enclosingControlFlowGraph,
                    ContainingSymbol);
                if (enclosingResult == null)
                {
                    continue;
                }

                // Also look at local functions and lambdas that weren't visited via interprocedural analysis.
                foreach (IMethodSymbol localFunctionSymbol in enclosingControlFlowGraph.LocalFunctions)
                {
                    if (!enclosingResult.VisitedLocalFunctions.Contains(localFunctionSymbol))
                    {
                        InvokeDfaAndAccumulateResults(
                            enclosingControlFlowGraph.GetLocalFunctionControlFlowGraph(localFunctionSymbol),
                            localFunctionSymbol);
                    }
                }

                foreach (IFlowAnonymousFunctionOperation flowAnonymousFunctionOperation in
                    enclosingControlFlowGraph.DescendantOperations<IFlowAnonymousFunctionOperation>(
                        OperationKind.FlowAnonymousFunction))
                {
                    if (!enclosingResult.VisitedLambdas.Contains(flowAnonymousFunctionOperation))
                    {
                        InvokeDfaAndAccumulateResults(
                            enclosingControlFlowGraph.GetAnonymousFunctionControlFlowGraph(flowAnonymousFunctionOperation),
                            flowAnonymousFunctionOperation.Symbol);
                    }
                }
            }

            return allResults;

            // Merges results from single PropertySet DFA invocation into allResults.
            PropertySetAnalysisResult? InvokeDfaAndAccumulateResults(ControlFlowGraph cfg, ISymbol owningSymbol)
            {
                PropertySetAnalysisResult? propertySetAnalysisResult =
                    PropertySetAnalysis.GetOrComputeResult(
                        cfg,
                        compilation,
                        owningSymbol,
                        analyzerOptions,
                        typeToTrackMetadataNames,
                        constructorMapper,
                        propertyMappers,
                        hazardousUsageEvaluators,
                        interproceduralAnalysisConfig,
                        pessimisticAnalysis,
                        onValueContentAnalysis);
                if (propertySetAnalysisResult == null || propertySetAnalysisResult.HazardousUsages.IsEmpty)
                {
                    return propertySetAnalysisResult;
                }

                allResults ??= PooledDictionary<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult>.GetInstance();

                foreach (KeyValuePair<(Location Location, IMethodSymbol? Method), HazardousUsageEvaluationResult> kvp
                    in propertySetAnalysisResult.HazardousUsages)
                {
                    if (allResults.TryGetValue(kvp.Key, out HazardousUsageEvaluationResult existingValue))
                    {
                        allResults[kvp.Key] = PropertySetAnalysis.MergeHazardousUsageEvaluationResult(existingValue, kvp.Value);
                    }
                    else
                    {
                        allResults.Add(kvp.Key, kvp.Value);
                    }
                }

                return propertySetAnalysisResult;
            }
        }

        /// <summary>
        /// When there are multiple hazardous usage evaluations for the same exact code, this prioritizes Flagged over MaybeFlagged, and MaybeFlagged over Unflagged.
        /// </summary>
        /// <param name="r1">First evaluation result.</param>
        /// <param name="r2">Second evaluation result.</param>
        /// <returns>Prioritized result.</returns>
        public static HazardousUsageEvaluationResult MergeHazardousUsageEvaluationResult(HazardousUsageEvaluationResult r1, HazardousUsageEvaluationResult r2)
        {
            if (r1 == HazardousUsageEvaluationResult.Flagged || r2 == HazardousUsageEvaluationResult.Flagged)
            {
                return HazardousUsageEvaluationResult.Flagged;
            }
            else if (r1 == HazardousUsageEvaluationResult.MaybeFlagged || r2 == HazardousUsageEvaluationResult.MaybeFlagged)
            {
                return HazardousUsageEvaluationResult.MaybeFlagged;
            }
            else
            {
                return HazardousUsageEvaluationResult.Unflagged;
            }
        }

        private static PropertySetAnalysisResult? TryGetOrComputeResultForAnalysisContext(PropertySetAnalysisContext analysisContext)
        {
            var operationVisitor = new PropertySetDataFlowOperationVisitor(analysisContext);
            var analysis = new PropertySetAnalysis(PropertySetAnalysisDomainInstance, operationVisitor);
            return analysis.TryGetOrComputeResultCore(analysisContext, cacheResult: true);
        }

        protected override PropertySetAnalysisResult ToResult(
            PropertySetAnalysisContext analysisContext,
            DataFlowAnalysisResult<PropertySetBlockAnalysisResult, PropertySetAbstractValue> dataFlowAnalysisResult)
        {
            PropertySetDataFlowOperationVisitor visitor = (PropertySetDataFlowOperationVisitor)this.OperationVisitor;
            visitor.ProcessExitBlock(dataFlowAnalysisResult.ExitBlockOutput);
            return new PropertySetAnalysisResult(
                dataFlowAnalysisResult,
                visitor.HazardousUsages,
                visitor.VisitedLocalFunctions,
                visitor.VisitedLambdas);
        }

        protected override PropertySetBlockAnalysisResult ToBlockResult(BasicBlock basicBlock, PropertySetAnalysisData blockAnalysisData)
            => new(basicBlock, blockAnalysisData);
    }
}
