// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;
using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelpers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        internal abstract class AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor : GlobalFlowStateDictionaryFlowOperationVisitor
        {
            private readonly WellKnownSymbolsInfo _wellKnownSymbolsInfo;

            protected AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor(
                GlobalFlowStateDictionaryAnalysisContext analysisContext,
                WellKnownSymbolsInfo wellKnownSymbolsInfo) : base(analysisContext)
            {
                _wellKnownSymbolsInfo = wellKnownSymbolsInfo;
            }

            protected abstract bool IsExpressionOfForEachStatement(SyntaxNode node);

            public override GlobalFlowStateDictionaryAnalysisValue VisitParameterReference(IParameterReferenceOperation operation, object? argument)
            {
                var value = base.VisitParameterReference(operation, argument);
                return VisitLocalOrParameter(operation.Parameter.Type?.OriginalDefinition, operation, value);
            }

            public override GlobalFlowStateDictionaryAnalysisValue VisitLocalReference(ILocalReferenceOperation operation, object? argument)
            {
                var value = base.VisitLocalReference(operation, argument);
                return VisitLocalOrParameter(operation.Local.Type?.OriginalDefinition, operation, value);
            }

            private GlobalFlowStateDictionaryAnalysisValue VisitLocalOrParameter(ITypeSymbol? typeSymbol, IOperation parameterOrLocalOperation, GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                if (!IsDeferredType(typeSymbol, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                {
                    return defaultValue;
                }

                if (!IsOperationEnumeratedByMethodInvocation(parameterOrLocalOperation, _wellKnownSymbolsInfo)
                    && !IsGetEnumeratorOfForEachLoopInvoked(parameterOrLocalOperation))
                {
                    return defaultValue;
                }

                if (DataFlowAnalysisContext.PointsToAnalysisResult == null)
                {
                    return defaultValue;
                }

                var pointToResult = DataFlowAnalysisContext.PointsToAnalysisResult[parameterOrLocalOperation.Kind, parameterOrLocalOperation.Syntax];
                if (pointToResult.Kind != PointsToAbstractValueKind.KnownLocations)
                {
                    return defaultValue;
                }

                if (pointToResult.Locations.Any(
                    l => !IsDeferredType(l.LocationType?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes)))
                {
                    return defaultValue;
                }

                var allEntities = CollectEntitiesForOperation(DataFlowAnalysisContext.PointsToAnalysisResult, parameterOrLocalOperation);
                if (allEntities.IsEmpty)
                {
                    return defaultValue;
                }

                return CreateAndUpdateAnalysisValue(parameterOrLocalOperation, allEntities, defaultValue);
            }

            private ImmutableArray<IDeferredTypeEntity> CollectEntitiesForOperation(
                PointsToAnalysisResult pointsToAnalysisResult, IOperation parameterOrLocalOperation)
            {
                var queue = new Queue<IOperation>();
                queue.Enqueue(parameterOrLocalOperation);
                var resultBuilder = ArrayBuilder<IDeferredTypeEntity>.GetInstance();

                while (queue.Count > 0)
                {
                    var currentOperation = queue.Dequeue();
                    if (currentOperation is IParameterReferenceOperation or ILocalReferenceOperation)
                    {
                        var result = pointsToAnalysisResult[currentOperation];
                        if (result.Kind != PointsToAbstractValueKind.KnownLocations || result.Locations.IsEmpty)
                        {
                            continue;
                        }

                        if (result.Locations.Count == 1)
                        {
                            var location = result.Locations.Single();
                            var creationOperation = location.Creation;
                            if (creationOperation == null && location.Symbol != null)
                            {
                                resultBuilder.Add(new DeferredTypeEntity(location.Symbol, null));
                            }

                            if (creationOperation is IInvocationOperation invocationOperation)
                            {
                                if (invocationOperation.Arguments.Any(argument => IsDeferredExecutingInvocation(invocationOperation, argument, _wellKnownSymbolsInfo)))
                                {
                                    foreach (var argument in invocationOperation.Arguments)
                                    {
                                        if (IsDeferredExecutingInvocation(invocationOperation, argument, _wellKnownSymbolsInfo))
                                        {
                                            queue.Enqueue(argument.Value);
                                        }
                                    }
                                }
                                else
                                {
                                    resultBuilder.Add(new DeferredTypeEntity(null, invocationOperation));
                                }
                            }

                            if (creationOperation is IParameterReferenceOperation or ILocalReferenceOperation
                                && IsDeferredType(creationOperation.Type?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                            {
                                queue.Enqueue(creationOperation);
                            }
                        }
                        else
                        {
                            resultBuilder.Add(DeferredTypeEntitySet.Create(result.Locations));
                        }
                    }

                    if (currentOperation is IConversionOperation conversionOperation
                        && IsValidImplicitConversion(currentOperation, _wellKnownSymbolsInfo))
                    {
                        queue.Enqueue(conversionOperation.Operand);
                    }

                    if (currentOperation is IInvocationOperation invocationOperation1
                        && !invocationOperation1.Arguments.Any(arg => IsDeferredExecutingInvocation(invocationOperation1, arg, _wellKnownSymbolsInfo)))
                    {
                        resultBuilder.Add(new DeferredTypeEntity(null, invocationOperation1));
                    }
                }

                return resultBuilder.ToImmutableAndFree();
            }

            private GlobalFlowStateDictionaryAnalysisValue CreateAndUpdateAnalysisValue(
                IOperation parameterOrLocalOperation,
                ImmutableArray<IDeferredTypeEntity> entities,
                GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                var analysisValueForNewEntity = CreateAnalysisValue(entities, parameterOrLocalOperation, defaultValue);
                UpdateGlobalValue(analysisValueForNewEntity);
                return analysisValueForNewEntity;
            }

            private static GlobalFlowStateDictionaryAnalysisValue CreateAnalysisValue(
                ImmutableArray<IDeferredTypeEntity> entities,
                IOperation parameterOrLocalReferenceOperation,
                GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                var trackingEntitiesBuilder = PooledDictionary<IDeferredTypeEntity, TrackingInvocationSet>.GetInstance();

                foreach (var analysisEntity in entities)
                {
                    trackingEntitiesBuilder.Add(analysisEntity,
                        new TrackingInvocationSet(ImmutableHashSet.Create(parameterOrLocalReferenceOperation),
                        InvocationCount.One));
                }

                var analysisValue = new GlobalFlowStateDictionaryAnalysisValue(
                    trackingEntitiesBuilder.ToImmutableDictionaryAndFree(),
                    GlobalFlowStateDictionaryAnalysisValueKind.Known);

                return defaultValue.Kind == GlobalFlowStateDictionaryAnalysisValueKind.Known
                    ? GlobalFlowStateDictionaryAnalysisValue.Merge(analysisValue, defaultValue)
                    : analysisValue;
            }

            private void UpdateGlobalValue(GlobalFlowStateDictionaryAnalysisValue value)
            {
                if (value.Kind == GlobalFlowStateDictionaryAnalysisValueKind.Known)
                {
                    var newState = GlobalFlowStateDictionaryAnalysisValue.Merge(GlobalState, value);
                    SetAbstractValue(GlobalEntity, newState);
                }
            }

            private bool IsGetEnumeratorOfForEachLoopInvoked(IOperation operation)
            {
                RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation);
                var operationToCheck = SkipDeferredAndConversionMethodIfNeeded(operation, _wellKnownSymbolsInfo);

                // Make sure it has IEnumerable type, not some other types like list, array, etc...
                if (!IsDeferredType(operationToCheck.Type?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                {
                    return false;
                }

                // Check 1: Operation would be invoked by GetEnumerator method
                // Check 2: Make sure the linked syntax node is the expression of ForEachLoop. It can't be done by finding IForEachLoopOperation,
                // because the Operation in CFG doesn't have that information. (CFG will convert the for each operation to control flow blocks)
                return operationToCheck.Parent is IInvocationOperation invocationOperation
                   && _wellKnownSymbolsInfo.GetEnumeratorMethods.Contains(invocationOperation.TargetMethod.OriginalDefinition)
                   && IsExpressionOfForEachStatement(invocationOperation.Syntax);
            }
        }
    }
}