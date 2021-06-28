// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
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

                            PooledHashSet<IOperation> rootOperationsNeedingAnalysis = PooledHashSet<IOperation>.GetInstance();

                            operationBlockStartContext.RegisterOperationAction(
                                operationAnalysisContext =>
                                {
                                    IPropertyReferenceOperation propertyReferenceOperation = (IPropertyReferenceOperation)operationAnalysisContext.Operation;
                                    if (sourceInfoSymbolMap.IsSourceProperty(propertyReferenceOperation.Property))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(propertyReferenceOperation.GetRoot());
                                        }
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
                                            lock (rootOperationsNeedingAnalysis)
                                            {
                                                rootOperationsNeedingAnalysis.Add(parameterReferenceOperation.GetRoot());
                                            }
                                        }
                                    },
                                    OperationKind.ParameterReference);
                            }

                            operationBlockStartContext.RegisterOperationAction(
                                operationAnalysisContext =>
                                {
                                    IInvocationOperation invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                                    if (sourceInfoSymbolMap.IsSourceMethod(
                                            invocationOperation.TargetMethod,
                                            invocationOperation.Arguments,
                                            pointsToFactory,
                                            valueContentFactory,
                                            out _))
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            rootOperationsNeedingAnalysis.Add(invocationOperation.GetRoot());
                                        }
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
                                            lock (rootOperationsNeedingAnalysis)
                                            {
                                                rootOperationsNeedingAnalysis.Add(operationAnalysisContext.Operation.GetRoot());
                                            }
                                        }
                                    },
                                    OperationKind.ArrayInitializer);
                            }

                            operationBlockStartContext.RegisterOperationBlockEndAction(
                                operationBlockAnalysisContext =>
                                {
                                    try
                                    {
                                        lock (rootOperationsNeedingAnalysis)
                                        {
                                            if (!rootOperationsNeedingAnalysis.Any())
                                            {
                                                return;
                                            }

                                            if (controlFlowGraphFactory.Value == null)
                                            {
                                                return;
                                            }

                                            foreach (IOperation rootOperation in rootOperationsNeedingAnalysis)
                                            {
                                                TaintedDataAnalysisResult? taintedDataAnalysisResult = TaintedDataAnalysis.TryGetOrComputeResult(
                                                    controlFlowGraphFactory.Value,
                                                    operationBlockAnalysisContext.Compilation,
                                                    operationBlockAnalysisContext.OwningSymbol,
                                                    operationBlockAnalysisContext.Options,
                                                    TaintedDataEnteringSinkDescriptor,
                                                    sourceInfoSymbolMap,
                                                    taintedDataConfig.GetSanitizerSymbolMap(this.SinkKind),
                                                    sinkInfoSymbolMap,
                                                    operationBlockAnalysisContext.CancellationToken);
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
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        rootOperationsNeedingAnalysis.Free(compilationContext.CancellationToken);
                                    }
                                });
                        });
                });
        }
    }
}