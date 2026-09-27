// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    using ValueContentAnalysisResult = DataFlowAnalysisResult<ValueContentBlockAnalysisResult, ValueContentAbstractValue>;

    /// <summary>
    /// Base class to aid in implementing tainted data analyzers.
    /// </summary>
    public abstract class SourceTriggeredTaintedDataAnalyzerBase : DiagnosticAnalyzer
    {
        // The engine defaults to three nested method calls; a higher prepass cap avoids unbounded recursion
        // without pruning any path that its configurable analysis could follow.
        private const int MaxPrepassCallDepth = 32;

        /// <summary>
        /// <see cref="DiagnosticDescriptor"/> for when tainted data enters a sink.
        /// </summary>
        /// <remarks>Format string arguments are:
        /// 0. Sink symbol.
        /// 1. Method name containing the code where the tainted data enters the sink.
        /// 2. Source symbol.
        /// 3. Method name containing the code where the tainted data came from the source.
        /// </remarks>
        protected abstract DiagnosticDescriptor TaintedDataEnteringSinkDescriptor { get; }

        /// <summary>
        /// Kind of tainted data sink.
        /// </summary>
        protected abstract SinkKind SinkKind { get; }

        protected virtual bool RequiresReachableSink => true;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(TaintedDataEnteringSinkDescriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationContext) =>
                {
                    Compilation compilation = compilationContext.Compilation;
                    TaintedDataConfig taintedDataConfig = TaintedDataConfig.GetOrCreate(compilation);
                    TaintedDataSymbolMap<SourceInfo> sourceInfoSymbolMap = taintedDataConfig.GetSourceSymbolMap(this.SinkKind);
                    if (sourceInfoSymbolMap.IsEmpty)
                    {
                        return;
                    }

                    TaintedDataSymbolMap<SinkInfo> sinkInfoSymbolMap = taintedDataConfig.GetSinkSymbolMap(this.SinkKind);
                    if (sinkInfoSymbolMap.IsEmpty)
                    {
                        return;
                    }

                    ConcurrentDictionary<IMethodSymbol, bool>? sinkReachabilityCache = RequiresReachableSink
                        ? new ConcurrentDictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default)
                        : null;
                    ConcurrentDictionary<IMethodSymbol, bool>? valueContentAnalysisCache = RequiresReachableSink && sourceInfoSymbolMap.RequiresValueContentAnalysis
                        ? new ConcurrentDictionary<IMethodSymbol, bool>(SymbolEqualityComparer.Default)
                        : null;
                    compilationContext.RegisterOperationBlockStartAction(
                        operationBlockStartContext =>
                        {
                            ISymbol owningSymbol = operationBlockStartContext.OwningSymbol;
                            AnalyzerOptions options = operationBlockStartContext.Options;
                            CancellationToken cancellationToken = operationBlockStartContext.CancellationToken;
                            if (options.IsConfiguredToSkipAnalysis(TaintedDataEnteringSinkDescriptor, owningSymbol, compilation))
                            {
                                return;
                            }

                            Lazy<bool>? mayReachSink = sinkReachabilityCache is not null ? new Lazy<bool>(() =>
                                HasReachableSink(operationBlockStartContext.OperationBlocks, compilation, sinkInfoSymbolMap,
                                    this.SinkKind, sinkReachabilityCache, cancellationToken)) : null;

                            WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);
                            Lazy<ControlFlowGraph?> controlFlowGraphFactory = new Lazy<ControlFlowGraph?>(
                                () => operationBlockStartContext.OperationBlocks.GetControlFlowGraph());
                            Lazy<PointsToAnalysisResult?> pointsToFactory = new Lazy<PointsToAnalysisResult?>(
                                () =>
                                {
                                    if (controlFlowGraphFactory.Value == null)
                                    {
                                        return null;
                                    }

                                    InterproceduralAnalysisConfiguration interproceduralAnalysisConfiguration = InterproceduralAnalysisConfiguration.Create(
                                                                    options,
                                                                    SupportedDiagnostics,
                                                                    controlFlowGraphFactory.Value,
                                                                    operationBlockStartContext.Compilation,
                                                                    defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.ContextSensitive);
                                    return PointsToAnalysis.TryGetOrComputeResult(
                                                                controlFlowGraphFactory.Value,
                                                                owningSymbol,
                                                                options,
                                                                wellKnownTypeProvider,
                                                                PointsToAnalysisKind.Complete,
                                                                interproceduralAnalysisConfiguration,
                                                                interproceduralAnalysisPredicate: null);
                                });
                            Lazy<(PointsToAnalysisResult?, ValueContentAnalysisResult?)> valueContentFactory = new Lazy<(PointsToAnalysisResult?, ValueContentAnalysisResult?)>(
                                () =>
                                {
                                    if (controlFlowGraphFactory.Value == null)
                                    {
                                        return (null, null);
                                    }

                                    InterproceduralAnalysisConfiguration interproceduralAnalysisConfiguration = InterproceduralAnalysisConfiguration.Create(
                                                                    options,
                                                                    SupportedDiagnostics,
                                                                    controlFlowGraphFactory.Value,
                                                                    operationBlockStartContext.Compilation,
                                                                    defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.ContextSensitive);
                                    ValueContentAnalysisResult? valuecontentAnalysisResult = ValueContentAnalysis.TryGetOrComputeResult(
                                                                    controlFlowGraphFactory.Value,
                                                                    owningSymbol,
                                                                    options,
                                                                    wellKnownTypeProvider,
                                                                    PointsToAnalysisKind.Complete,
                                                                    interproceduralAnalysisConfiguration,
                                                                    out _,
                                                                    out PointsToAnalysisResult? p);

                                    return (p, valuecontentAnalysisResult);
                                });

                            int hasSource = 0;

                            operationBlockStartContext.RegisterOperationAction(
                                operationAnalysisContext =>
                                {
                                    IPropertyReferenceOperation propertyReferenceOperation = (IPropertyReferenceOperation)operationAnalysisContext.Operation;
                                    if (sourceInfoSymbolMap.IsSourceProperty(propertyReferenceOperation.Property))
                                    {
                                        Interlocked.Exchange(ref hasSource, 1);
                                    }
                                },
                                OperationKind.PropertyReference);

                            if (sourceInfoSymbolMap.RequiresParameterReferenceAnalysis)
                            {
                                operationBlockStartContext.RegisterOperationAction(
                                    operationAnalysisContext =>
                                    {
                                        IParameterReferenceOperation parameterReferenceOperation = (IParameterReferenceOperation)operationAnalysisContext.Operation;
                                        if (sourceInfoSymbolMap.IsSourceParameter(parameterReferenceOperation.Parameter, wellKnownTypeProvider))
                                        {
                                            Interlocked.Exchange(ref hasSource, 1);
                                        }
                                    },
                                    OperationKind.ParameterReference);
                            }

                            operationBlockStartContext.RegisterOperationAction(
                                operationAnalysisContext =>
                                {
                                    IInvocationOperation invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                                    if (mayReachSink is not null &&
                                        sourceInfoSymbolMap.GetInfosForType(invocationOperation.TargetMethod.ContainingType).Any() &&
                                        !mayReachSink.Value)
                                    {
                                        return;
                                    }

                                    if (sourceInfoSymbolMap.IsSourceMethod(
                                            invocationOperation.TargetMethod,
                                            invocationOperation.Arguments,
                                            pointsToFactory,
                                            valueContentFactory,
                                            out _))
                                    {
                                        Interlocked.Exchange(ref hasSource, 1);
                                    }
                                },
                                OperationKind.Invocation);

                            if (TaintedDataConfig.HasTaintArraySource(SinkKind))
                            {
                                operationBlockStartContext.RegisterOperationAction(
                                    operationAnalysisContext =>
                                    {
                                        IArrayInitializerOperation arrayInitializerOperation = (IArrayInitializerOperation)operationAnalysisContext.Operation;
                                        if (arrayInitializerOperation.GetAncestor<IArrayCreationOperation>(OperationKind.ArrayCreation)?.Type is IArrayTypeSymbol arrayTypeSymbol
                                            && sourceInfoSymbolMap.IsSourceConstantArrayOfType(arrayTypeSymbol, arrayInitializerOperation))
                                        {
                                            Interlocked.Exchange(ref hasSource, 1);
                                        }
                                    },
                                    OperationKind.ArrayInitializer);
                            }

                            operationBlockStartContext.RegisterOperationBlockEndAction(
                                operationBlockAnalysisContext =>
                                {
                                    if (Volatile.Read(ref hasSource) == 0 || mayReachSink is { Value: false })
                                    {
                                        return;
                                    }

                                    if (controlFlowGraphFactory.Value == null)
                                    {
                                        return;
                                    }

                                    TaintedDataSymbolMap<SanitizerInfo> sanitizerInfoSymbolMap = taintedDataConfig.GetSanitizerSymbolMap(this.SinkKind);
                                    // Computed source values and branch predicates may need value-content flow,
                                    // even when every literal array element is already known.
                                    bool performValueContentAnalysis = valueContentAnalysisCache is null ||
                                        sanitizerInfoSymbolMap.RequiresValueContentAnalysis ||
                                        sinkInfoSymbolMap.RequiresValueContentAnalysis ||
                                        (sourceInfoSymbolMap.RequiresValueContentAnalysis &&
                                            MayRequireValueContentAnalysis(operationBlockStartContext.OperationBlocks,
                                                compilation, sourceInfoSymbolMap, valueContentAnalysisCache,
                                                cancellationToken));
                                    TaintedDataAnalysisResult? taintedDataAnalysisResult = TaintedDataAnalysis.TryGetOrComputeResult(
                                        controlFlowGraphFactory.Value,
                                        operationBlockAnalysisContext.Compilation,
                                        operationBlockAnalysisContext.OwningSymbol,
                                        operationBlockAnalysisContext.Options,
                                        TaintedDataEnteringSinkDescriptor,
                                        sourceInfoSymbolMap,
                                        sanitizerInfoSymbolMap,
                                        sinkInfoSymbolMap,
                                        performValueContentAnalysis);
                                    if (taintedDataAnalysisResult == null)
                                    {
                                        return;
                                    }

                                    foreach (TaintedDataSourceSink sourceSink in taintedDataAnalysisResult.TaintedDataSourceSinks)
                                    {
                                        if (!sourceSink.SinkKinds.Contains(this.SinkKind))
                                        {
                                            continue;
                                        }

                                        foreach (SymbolAccess sourceOrigin in sourceSink.SourceOrigins)
                                        {
                                            // Something like:
                                            // CA3001: Potential SQL injection vulnerability was found where '{0}' in method '{1}' may be tainted by user-controlled data from '{2}' in method '{3}'.
                                            Diagnostic diagnostic = Diagnostic.Create(
                                                this.TaintedDataEnteringSinkDescriptor,
                                                sourceSink.Sink.Location,
                                                additionalLocations: new Location[] { sourceOrigin.Location },
                                                messageArgs: new object[] {
                                            sourceSink.Sink.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                            sourceSink.Sink.AccessingMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                            sourceOrigin.Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                            sourceOrigin.AccessingMethod.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)});
                                            operationBlockAnalysisContext.ReportDiagnostic(diagnostic);
                                        }
                                    }
                                });
                        });
                });
        }

        private static bool HasReachableSink(
            ImmutableArray<IOperation> operationBlocks,
            Compilation compilation,
            TaintedDataSymbolMap<SinkInfo> sinkInfoSymbolMap,
            SinkKind sinkKind,
            ConcurrentDictionary<IMethodSymbol, bool> sinkReachabilityCache,
            CancellationToken cancellationToken)
        {
            using PooledHashSet<IMethodSymbol> visitingMethods = PooledHashSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default);
            foreach (IOperation operationBlock in operationBlocks)
            {
                if (HasReachableSink(operationBlock, compilation, sinkInfoSymbolMap, sinkKind, sinkReachabilityCache,
                    visitingMethods, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasReachableSink(
            IOperation root,
            Compilation compilation,
            TaintedDataSymbolMap<SinkInfo> sinkInfoSymbolMap,
            SinkKind sinkKind,
            ConcurrentDictionary<IMethodSymbol, bool> sinkReachabilityCache,
            PooledHashSet<IMethodSymbol> visitingMethods,
            CancellationToken cancellationToken)
        {
            foreach (IOperation operation in root.DescendantsAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (operation)
                {
                    case IInvocationOperation invocation:
                        if (IsSinkMethod(invocation.TargetMethod, sinkInfoSymbolMap, sinkKind) ||
                            MethodMayReachSink(invocation.TargetMethod, compilation, sinkInfoSymbolMap, sinkKind,
                                sinkReachabilityCache, visitingMethods, cancellationToken))
                        {
                            return true;
                        }

                        break;

                    case IObjectCreationOperation creation when creation.Constructor is IMethodSymbol constructor:
                        if (IsSinkMethod(constructor, sinkInfoSymbolMap, sinkKind) ||
                            MethodMayReachSink(constructor, compilation, sinkInfoSymbolMap, sinkKind,
                                sinkReachabilityCache, visitingMethods, cancellationToken))
                        {
                            return true;
                        }

                        break;

                    case IPropertyReferenceOperation property:
                        if (IsSinkProperty(property.Property, sinkInfoSymbolMap, sinkKind) ||
                            (property.Property.GetMethod is IMethodSymbol getter &&
                                MethodMayReachSink(getter, compilation, sinkInfoSymbolMap, sinkKind,
                                    sinkReachabilityCache, visitingMethods, cancellationToken)) ||
                            (property.Property.SetMethod is IMethodSymbol setter &&
                                MethodMayReachSink(setter, compilation, sinkInfoSymbolMap, sinkKind,
                                    sinkReachabilityCache, visitingMethods, cancellationToken)))
                        {
                            return true;
                        }

                        break;

                    case IPropertyInitializerOperation initializer:
                        if (initializer.InitializedProperties.Any(property => IsSinkProperty(property, sinkInfoSymbolMap, sinkKind)))
                        {
                            return true;
                        }

                        break;

                    case IMethodReferenceOperation reference:
                        if (IsSinkMethod(reference.Method, sinkInfoSymbolMap, sinkKind) ||
                            MethodMayReachSink(reference.Method, compilation, sinkInfoSymbolMap, sinkKind,
                                sinkReachabilityCache, visitingMethods, cancellationToken))
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
                    case IConversionOperation { OperatorMethod: not null }:
                    case IBinaryOperation { OperatorMethod: not null }:
                    case IUnaryOperation { OperatorMethod: not null }:
                    case ICompoundAssignmentOperation { OperatorMethod: not null }:
                    case IIncrementOrDecrementOperation { OperatorMethod: not null }:
                        // These calls may dispatch to a sink even when there is no ordinary invocation operation.
                        return true;
                }
            }

            return false;
        }

        private static bool MethodMayReachSink(
            IMethodSymbol method,
            Compilation compilation,
            TaintedDataSymbolMap<SinkInfo> sinkInfoSymbolMap,
            SinkKind sinkKind,
            ConcurrentDictionary<IMethodSymbol, bool> sinkReachabilityCache,
            PooledHashSet<IMethodSymbol> visitingMethods,
            CancellationToken cancellationToken)
        {
            if (method.MethodKind == MethodKind.DelegateInvoke)
            {
                return true;
            }

            // The dataflow engine cannot analyze metadata bodies or dispatch to virtual implementations.
            if (!SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, compilation.Assembly) ||
                method.IsAbstract || method.IsVirtual || method.IsOverride || method.IsImplicitlyDeclared)
            {
                return false;
            }

            method = method.OriginalDefinition;
            if (sinkReachabilityCache.TryGetValue(method, out bool canReachSink))
            {
                return canReachSink;
            }

            if (visitingMethods.Count >= MaxPrepassCallDepth || !visitingMethods.Add(method))
            {
                // Do not prune a recursive call chain without proving it cannot reach a sink.
                return true;
            }

            try
            {
                IBlockOperation? block = method.GetTopmostOperationBlock(compilation, cancellationToken);
                canReachSink = block is null ||
                    HasReachableSink(block.GetRoot(), compilation, sinkInfoSymbolMap, sinkKind, sinkReachabilityCache,
                        visitingMethods, cancellationToken);
                sinkReachabilityCache.TryAdd(method, canReachSink);
                return canReachSink;
            }
            finally
            {
                visitingMethods.Remove(method);
            }
        }

        private static bool IsSinkMethod(IMethodSymbol method, TaintedDataSymbolMap<SinkInfo> sinkInfoSymbolMap, SinkKind sinkKind)
        {
            foreach (SinkInfo sinkInfo in sinkInfoSymbolMap.GetInfosForType(method.ContainingType))
            {
                if (!sinkInfo.SinkKinds.Contains(sinkKind))
                {
                    continue;
                }

                if (method.MethodKind == MethodKind.Constructor &&
                    sinkInfo.IsAnyStringParameterInConstructorASink &&
                    method.Parameters.Any(parameter => parameter.Type.SpecialType == SpecialType.System_String))
                {
                    return true;
                }

                if (sinkInfo.SinkMethodParameters.TryGetValue(method.MetadataName, out ImmutableHashSet<string>? sinkParameters) &&
                    method.Parameters.Any(parameter => sinkParameters.Contains(parameter.MetadataName)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSinkProperty(IPropertySymbol property, TaintedDataSymbolMap<SinkInfo> sinkInfoSymbolMap, SinkKind sinkKind)
        {
            return sinkInfoSymbolMap.GetInfosForType(property.ContainingType)
                .Any(sinkInfo => sinkInfo.SinkKinds.Contains(sinkKind) && sinkInfo.SinkProperties.Contains(property.MetadataName));
        }

        private static bool MayRequireValueContentAnalysis(
            ImmutableArray<IOperation> operationBlocks,
            Compilation compilation,
            TaintedDataSymbolMap<SourceInfo> sourceInfoSymbolMap,
            ConcurrentDictionary<IMethodSymbol, bool> cache,
            CancellationToken cancellationToken)
        {
            using PooledHashSet<IMethodSymbol> visitingMethods = PooledHashSet<IMethodSymbol>.GetInstance(SymbolEqualityComparer.Default);
            return operationBlocks.Any(block =>
                MayRequireValueContentAnalysis(block, compilation, sourceInfoSymbolMap, cache, visitingMethods, cancellationToken));
        }

        private static bool MayRequireValueContentAnalysis(
            IOperation root,
            Compilation compilation,
            TaintedDataSymbolMap<SourceInfo> sourceInfoSymbolMap,
            ConcurrentDictionary<IMethodSymbol, bool> cache,
            PooledHashSet<IMethodSymbol> visitingMethods,
            CancellationToken cancellationToken)
        {
            foreach (IOperation operation in root.DescendantsAndSelf())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (operation)
                {
                    case IArrayInitializerOperation initializer
                        when initializer.GetAncestor<IArrayCreationOperation>(OperationKind.ArrayCreation)?.Type is IArrayTypeSymbol arrayType
                            && sourceInfoSymbolMap.IsSourceConstantArrayOfType(arrayType, initializer)
                            && initializer.ElementValues.Any(element => !element.ConstantValue.HasValue):
                        // Nonconstant elements can become literals through value propagation.
                        return true;

                    case IInvocationOperation invocation:
                        if (IsValueContentSource(invocation.TargetMethod, invocation.Arguments, sourceInfoSymbolMap) ||
                            MethodMayRequireValueContentAnalysis(invocation.TargetMethod, compilation, sourceInfoSymbolMap,
                                cache, visitingMethods, cancellationToken))
                        {
                            return true;
                        }

                        break;

                    case IObjectCreationOperation creation when creation.Constructor is IMethodSymbol constructor:
                        if (IsValueContentSource(constructor, creation.Arguments, sourceInfoSymbolMap) ||
                            MethodMayRequireValueContentAnalysis(constructor, compilation, sourceInfoSymbolMap,
                                cache, visitingMethods, cancellationToken))
                        {
                            return true;
                        }

                        break;

                    case IPropertyReferenceOperation property:
                        if ((property.Property.GetMethod is IMethodSymbol getter &&
                                MethodMayRequireValueContentAnalysis(getter, compilation, sourceInfoSymbolMap,
                                    cache, visitingMethods, cancellationToken)) ||
                            (property.Property.SetMethod is IMethodSymbol setter &&
                                MethodMayRequireValueContentAnalysis(setter, compilation, sourceInfoSymbolMap,
                                    cache, visitingMethods, cancellationToken)))
                        {
                            return true;
                        }

                        break;

                    case IMethodReferenceOperation reference:
                        if (sourceInfoSymbolMap.GetInfosForType(reference.Method.ContainingType)
                                .Any(source => source.TaintedMethodsNeedsValueContentAnalysis.Count > 0) ||
                            MethodMayRequireValueContentAnalysis(reference.Method, compilation, sourceInfoSymbolMap,
                                cache, visitingMethods, cancellationToken))
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

        private static bool IsValueContentSource(
            IMethodSymbol method,
            ImmutableArray<IArgumentOperation> arguments,
            TaintedDataSymbolMap<SourceInfo> sourceInfoSymbolMap)
        {
            return sourceInfoSymbolMap.GetInfosForType(method.ContainingType)
                .Any(source => source.TaintedMethodsNeedsValueContentAnalysis
                    .Any(entry => entry.MethodMatcher(method.Name, arguments)));
        }

        private static bool MethodMayRequireValueContentAnalysis(
            IMethodSymbol method,
            Compilation compilation,
            TaintedDataSymbolMap<SourceInfo> sourceInfoSymbolMap,
            ConcurrentDictionary<IMethodSymbol, bool> cache,
            PooledHashSet<IMethodSymbol> visitingMethods,
            CancellationToken cancellationToken)
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
            if (cache.TryGetValue(method, out bool requiresValueContent))
            {
                return requiresValueContent;
            }

            if (visitingMethods.Count >= MaxPrepassCallDepth || !visitingMethods.Add(method))
            {
                return true;
            }

            try
            {
                IBlockOperation? block = method.GetTopmostOperationBlock(compilation, cancellationToken);
                requiresValueContent = block is null ||
                    MayRequireValueContentAnalysis(block.GetRoot(), compilation, sourceInfoSymbolMap, cache,
                        visitingMethods, cancellationToken);
                cache.TryAdd(method, requiresValueContent);
                return requiresValueContent;
            }
            finally
            {
                visitingMethods.Remove(method);
            }
        }
    }
}