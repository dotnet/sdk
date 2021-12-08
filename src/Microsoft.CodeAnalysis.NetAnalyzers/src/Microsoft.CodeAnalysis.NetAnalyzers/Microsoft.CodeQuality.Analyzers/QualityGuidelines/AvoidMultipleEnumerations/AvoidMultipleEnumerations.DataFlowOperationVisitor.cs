// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
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
    internal partial class AvoidMultipleEnumerations
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

            private GlobalFlowStateDictionaryAnalysisValue VisitLocalOrParameter(ITypeSymbol? typeSymbol, IOperation parameterOrLocalReferenceOperation, GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                RoslynDebug.Assert(parameterOrLocalReferenceOperation is IParameterReferenceOperation or ILocalReferenceOperation);
                if (!IsDeferredType(typeSymbol, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                {
                    return defaultValue;
                }

                if (!IsOperationEnumeratedByMethodInvocation(parameterOrLocalReferenceOperation, _wellKnownSymbolsInfo)
                    && !IsGetEnumeratorOfForEachLoopInvoked(parameterOrLocalReferenceOperation))
                {
                    return defaultValue;
                }

                if (DataFlowAnalysisContext.PointsToAnalysisResult == null)
                {
                    return defaultValue;
                }

                var pointToResult = DataFlowAnalysisContext.PointsToAnalysisResult[parameterOrLocalReferenceOperation.Kind, parameterOrLocalReferenceOperation.Syntax];
                if (pointToResult.Kind != PointsToAbstractValueKind.KnownLocations)
                {
                    return defaultValue;
                }

                if (pointToResult.Locations.Any(
                    l => !IsDeferredType(l.LocationType?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes)))
                {
                    return defaultValue;
                }

                return VisitDeferTypeEntities(
                    DataFlowAnalysisContext.PointsToAnalysisResult,
                    parameterOrLocalReferenceOperation,
                    defaultValue);
            }

            /// <summary>
            /// Visit all the possible deferred type entities referenced by <param name="parameterOrLocalOperation"/>.
            /// </summary>
            private GlobalFlowStateDictionaryAnalysisValue VisitDeferTypeEntities(
                PointsToAnalysisResult pointsToAnalysisResult,
                IOperation parameterOrLocalOperation,
                GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                RoslynDebug.Assert(parameterOrLocalOperation is IParameterReferenceOperation or ILocalReferenceOperation);
                // With the initial operation as the root, expand it if
                // the operation is parameter or local reference. It has only one pointToAnalysisResult, and it is a deferred execution invocation.
                // Visit its argument.
                // e.g.
                // var a = b.Concat(c);
                // a.ElementAt(10);
                // When we visit 'a.Element(10)' and look back to 'b.Concat(c)', also try to visit 'b' and 'c'.
                //
                // Update the analysis value when reach one of the following nodes.
                // 1. A parameter or local that has symbol, but no creationOperation.
                // e.g.
                // void Bar(IEnumerable<int> b, IEnumerable<int> c)
                // {
                //      var a = b.Concat(c);
                //      a.ElementAt(10);
                // }
                // When 'a.ElementAt(10)' is called, 'b' and 'c' are enumerated once.
                //
                // 2. Invocation operation that returns a deferred type.
                // e.g.
                // void Bar()
                // {
                //       var a = Enumerable.Range(1, 1);
                //       var b = Enumerable.Range(2, 2);
                //       var c = a.Concat(b);
                //       c.ElementAt(10);
                // }
                // When 'c.ElementAt(10)' is called, then 'Enumerable.Range(1, 1)', 'Enumerable.Range(2, 2)' and 'a.Concat(b)' are enumerated.
                // 3. A parameter or local reference operation with multiple AbstractLocations. Stop expanding the tree at this node
                // because we don't know how to proceed.
                // e.g.
                // void Bar(bool flag)
                // {
                //       var a = flag ? Enumerable.Range(1, 1) : Enumerable.Range(2, 2);
                //       a.ElementAt(10);
                // }
                var queue = new Queue<IOperation>();
                queue.Enqueue(parameterOrLocalOperation);
                var resultAnalysisValue = defaultValue;
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

                        // Expand if there is only one AbstractLocation for this operation.
                        if (result.Locations.Count == 1)
                        {
                            var location = result.Locations.Single();
                            var creationOperation = location.Creation;
                            // Node 1: A parameter or local that has symbol, but no creation operation.
                            if (creationOperation == null
                                && location.Symbol != null
                                && IsDeferredType(location.LocationType?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                            {
                                var analysisValue = CreateAndUpdateAnalysisValue(currentOperation, new DeferredTypeSymbolEntity(location.Symbol), defaultValue);
                                resultAnalysisValue = GlobalFlowStateDictionaryAnalysisValue.Merge(resultAnalysisValue, analysisValue);
                                continue;
                            }

                            if (creationOperation is IInvocationOperation invocationCreationOperation)
                            {
                                // Try to expand the argument of this invocation operation.
                                ExpandInvocationOperation(invocationCreationOperation, _wellKnownSymbolsInfo, queue);

                                var creationMethod = invocationCreationOperation.TargetMethod.ReducedFrom ?? invocationCreationOperation.TargetMethod;
                                // Make sure this creation operation is not 'AsEnumerable', which only do a cast, and do not create new IEnumerable type.
                                if (!_wellKnownSymbolsInfo.NoEffectLinqChainMethods.Contains(creationMethod.OriginalDefinition)
                                    && IsDeferredType(invocationCreationOperation.Type?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                                {
                                    // Node 2: Invocation operation that returns a deferred type.
                                    var analysisValue =
                                        CreateAndUpdateAnalysisValue(currentOperation, new DeferredTypeCreationEntity(invocationCreationOperation), defaultValue);

                                    resultAnalysisValue = GlobalFlowStateDictionaryAnalysisValue.Merge(resultAnalysisValue, analysisValue);
                                }

                                continue;
                            }
                        }
                        else
                        {
                            // Make sure all the locations are pointing to a deferred type.
                            if (result.Locations.Any(
                                l => !IsDeferredType(l.LocationType?.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes)))
                            {
                                continue;
                            }

                            // Node 3: A parameter or local reference operation with multiple AbstractLocations.
                            var analysisValue = CreateAndUpdateAnalysisValue(
                                currentOperation,
                                new DeferredTypeEntitySet(result.Locations),
                                defaultValue);

                            resultAnalysisValue = GlobalFlowStateDictionaryAnalysisValue.Merge(resultAnalysisValue, analysisValue);
                        }
                    }

                    // Make sure we iterate into the nested operations.
                    // e.g.
                    // var a = b.Concat(c).Concat(d.Concat(e));
                    // Make sure 'd.Concat(e)' is expanded so that 'd' and 'e' could be found.
                    if (currentOperation is IInvocationOperation invocationOperation)
                    {
                        ExpandInvocationOperation(invocationOperation, _wellKnownSymbolsInfo, queue);
                    }

                    // Expand the implicit conversion operation if it is converting a deferred type to another deferred type.
                    // This might happen in such case:
                    // var c = a.OrderBy(i => i).Concat(b)
                    // The tree would be:
                    //                             a.OrderBy(i => i).Concat(b) (root)
                    //                              /                        \
                    //                          ArgumentOperation          ArgumentOperation
                    //                             /                             \
                    //                          Conversion *(expand this node)    b
                    //                            /
                    //                         a.OrderBy(i => i)
                    //                           /
                    //                         ArgumentOperation
                    //                          /
                    //                         a
                    if (currentOperation is IConversionOperation conversionOperation)
                    {
                        ExpandConversionOperation(conversionOperation, _wellKnownSymbolsInfo, queue);
                    }
                }

                return resultAnalysisValue;
            }

            private void ExpandInvocationOperation(
                IInvocationOperation invocationOperation,
                WellKnownSymbolsInfo wellKnownSymbolsInfo,
                Queue<IOperation> queue)
            {
                // Check the arguments of this invocation to see if this is a deferred executing method.
                // e.g.
                // var a = b.Concat(c);
                // When we looking at the creation of 'a', we want to find both 'b' and 'c'
                foreach (var argument in invocationOperation.Arguments)
                {
                    if (IsDeferredExecutingInvocation(invocationOperation, argument, wellKnownSymbolsInfo))
                    {
                        queue.Enqueue(argument.Value);
                    }
                }

                // Also check it's invocation instance if the extension method could be used in reduced form.
                // e.g.
                // Dim a = b.Concat(c)
                // We need enqueue the invocation instance (which is 'b') if the target method is a reduced extension method
                if (IsInvocationDeferredExecutingInvocationInstance(invocationOperation, wellKnownSymbolsInfo))
                {
                    queue.Enqueue(invocationOperation.Instance);
                }
            }

            private static void ExpandConversionOperation(
                IConversionOperation conversionOperation,
                WellKnownSymbolsInfo wellKnownSymbolsInfo,
                Queue<IOperation> queue)
            {
                if (IsValidImplicitConversion(conversionOperation, wellKnownSymbolsInfo))
                {
                    queue.Enqueue(conversionOperation.Operand);
                }
            }

            private GlobalFlowStateDictionaryAnalysisValue CreateAndUpdateAnalysisValue(
                IOperation parameterOrLocalOperation,
                IDeferredTypeEntity entity,
                GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                var analysisValueForNewEntity = CreateAnalysisValue(entity, parameterOrLocalOperation, defaultValue);
                UpdateGlobalValue(analysisValueForNewEntity);
                return analysisValueForNewEntity;
            }

            private static GlobalFlowStateDictionaryAnalysisValue CreateAnalysisValue(
                IDeferredTypeEntity entity,
                IOperation parameterOrLocalReferenceOperation,
                GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                var operationsSetBuilder = PooledHashSet<IOperation>.GetInstance();
                operationsSetBuilder.Add(parameterOrLocalReferenceOperation);
                var newInvocationSet = new TrackingInvocationSet(
                    operationsSetBuilder.ToImmutableAndFree(),
                    InvocationCount.One);

                var trackedEntitiesBuilder = PooledDictionary<IDeferredTypeEntity, TrackingInvocationSet>.GetInstance();
                trackedEntitiesBuilder.Add(entity, newInvocationSet);

                var analysisValue = new GlobalFlowStateDictionaryAnalysisValue(
                    trackedEntitiesBuilder.ToImmutableDictionaryAndFree(),
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