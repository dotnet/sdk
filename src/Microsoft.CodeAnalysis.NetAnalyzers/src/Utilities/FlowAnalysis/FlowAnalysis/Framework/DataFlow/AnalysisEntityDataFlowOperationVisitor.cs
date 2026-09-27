// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Analyzer.Utilities.PooledObjects.Extensions;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.CopyAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.FlowAnalysis.DataFlow
{
    /// <summary>
    /// Operation visitor to flow the abstract dataflow analysis values for <see cref="AnalysisEntity"/> instances across a given statement in a basic block.
    /// </summary>
    public abstract class AnalysisEntityDataFlowOperationVisitor<TAnalysisData, TAnalysisContext, TAnalysisResult, TAbstractAnalysisValue>
        : DataFlowOperationVisitor<TAnalysisData, TAnalysisContext, TAnalysisResult, TAbstractAnalysisValue>
        where TAnalysisData : AbstractAnalysisData
        where TAnalysisContext : AbstractDataFlowAnalysisContext<TAnalysisData, TAnalysisContext, TAnalysisResult, TAbstractAnalysisValue>
        where TAnalysisResult : class, IDataFlowAnalysisResult<TAbstractAnalysisValue>
        where TAbstractAnalysisValue : IEquatable<TAbstractAnalysisValue>
    {
        private ImmutableHashSet<ISymbol>? _referencedStaticMembers;
        private bool _hasPotentialCallerAliases;
        private Dictionary<ControlFlowGraph, (ImmutableHashSet<ISymbol> Members, bool HasArrayAccess)>? _referencedMembersByCfg;
        private HashSet<(ControlFlowGraph Cfg, bool CheckReturnedAliases, bool HasStaticState)>? _safeIndirectCapturePaths;

        protected AnalysisEntityDataFlowOperationVisitor(TAnalysisContext analysisContext)
            : base(analysisContext)
        {
            Debug.Assert(!analysisContext.PredicateAnalysis || SupportsPredicateAnalysis);
        }

        protected virtual bool SupportsPredicateAnalysis => false;
        protected void AddTrackedEntities(HashSet<AnalysisEntity> builder, bool forInterproceduralAnalysis = false)
            => AddTrackedEntities(CurrentAnalysisData, builder, forInterproceduralAnalysis);
        protected abstract void AddTrackedEntities(TAnalysisData analysisData, HashSet<AnalysisEntity> builder, bool forInterproceduralAnalysis = false);
        protected abstract void SetAbstractValue(AnalysisEntity analysisEntity, TAbstractAnalysisValue value);
        protected abstract void ResetAbstractValue(AnalysisEntity analysisEntity);
        protected abstract TAbstractAnalysisValue GetAbstractValue(AnalysisEntity analysisEntity);
        protected abstract bool HasAbstractValue(AnalysisEntity analysisEntity);
        protected abstract void StopTrackingEntity(AnalysisEntity analysisEntity, TAnalysisData analysisData);

        protected override TAbstractAnalysisValue ComputeAnalysisValueForReferenceOperation(IOperation operation, TAbstractAnalysisValue defaultValue)
        {
            if (AnalysisEntityFactory.TryCreate(operation, out var analysisEntity))
            {
                if (!HasAbstractValue(analysisEntity))
                {
                    SetAbstractValue(analysisEntity, defaultValue);
                }

                return GetAbstractValue(analysisEntity);
            }
            else
            {
                return defaultValue;
            }
        }

        protected sealed override TAbstractAnalysisValue ComputeAnalysisValueForEscapedRefOrOutArgument(IArgumentOperation operation, TAbstractAnalysisValue defaultValue)
        {
            Debug.Assert(operation.Parameter!.RefKind is RefKind.Ref or RefKind.Out);

            if (AnalysisEntityFactory.TryCreate(operation, out var analysisEntity))
            {
                var value = ComputeAnalysisValueForEscapedRefOrOutArgument(analysisEntity, operation, defaultValue);
                SetAbstractValueForAssignment(analysisEntity, operation, value);
                return GetAbstractValue(analysisEntity);
            }
            else
            {
                return defaultValue;
            }
        }

        protected virtual TAbstractAnalysisValue ComputeAnalysisValueForEscapedRefOrOutArgument(AnalysisEntity analysisEntity, IArgumentOperation operation, TAbstractAnalysisValue defaultValue)
        {
            Debug.Assert(operation.Parameter!.RefKind is RefKind.Ref or RefKind.Out);

            return defaultValue;
        }

        /// <summary>
        /// Helper method to reset analysis data for analysis entities.
        /// </summary>
        protected void ResetAnalysisData(DictionaryAnalysisData<AnalysisEntity, TAbstractAnalysisValue> currentAnalysisData)
        {
            // Reset the current analysis data, while ensuring that we don't violate the monotonicity, i.e. we cannot remove any existing key from currentAnalysisData.
            // Just set the values for existing keys to ValueDomain.UnknownOrMayBeValue.
            var keys = currentAnalysisData.Keys.ToImmutableArray();
            foreach (var key in keys)
            {
                ResetAbstractValue(key);
            }
        }

        protected override void ProcessOutOfScopeLocalsAndFlowCaptures(IEnumerable<ILocalSymbol> locals, IEnumerable<CaptureId> flowCaptures)
        {
            Debug.Assert(locals.Any() || flowCaptures.Any());

            base.ProcessOutOfScopeLocalsAndFlowCaptures(locals, flowCaptures);

            using var allEntities = PooledHashSet<AnalysisEntity>.GetInstance();
            AddTrackedEntities(allEntities);

            // Stop tracking entities for locals and capture Ids that are now out of scope.
            foreach (var local in locals)
            {
                if (AnalysisEntityFactory.TryCreateForSymbolDeclaration(local, out var analysisEntity))
                {
                    StopTrackingDataForEntity(analysisEntity, allEntities);
                }
                else
                {
                    Debug.Fail("TryCreateForSymbolDeclaration failed");
                }
            }

            foreach (var captureId in flowCaptures)
            {
                if (AnalysisEntityFactory.TryGetForFlowCapture(captureId, out var analysisEntity))
                {
                    StopTrackingDataForEntity(analysisEntity, allEntities);
                }
            }
        }

        private void StopTrackingDataForEntity(AnalysisEntity analysisEntity, PooledHashSet<AnalysisEntity> allEntities)
            => StopTrackingDataForEntity(analysisEntity, CurrentAnalysisData, allEntities);

        private void StopTrackingDataForEntity(AnalysisEntity analysisEntity, TAnalysisData analysisData, PooledHashSet<AnalysisEntity> allEntities)
        {
            if (!allEntities.Contains(analysisEntity))
            {
                return;
            }

            // Stop tracking entity that is now out of scope.
            StopTrackingEntity(analysisEntity, analysisData);

            // Additionally, stop tracking all the child entities if the entity type has value copy semantics.
            if (analysisEntity.Type.HasValueCopySemantics())
            {
                foreach (var childEntity in GetChildAnalysisEntities(analysisEntity, allEntities))
                {
                    StopTrackingEntity(childEntity, analysisData);
                }
            }
        }

        private void StopTrackingDataForParamArrayParameterIndices(AnalysisEntity analysisEntity, TAnalysisData analysisData, PooledHashSet<AnalysisEntity> allEntities)
        {
            Debug.Assert(analysisEntity.Symbol is IParameterSymbol parameter && parameter.IsParams);

            foreach (var entity in allEntities)
            {
                if (!entity.Indices.IsEmpty &&
                    entity.InstanceLocation.Equals(analysisEntity.InstanceLocation))
                {
                    StopTrackingEntity(entity, analysisData);
                }
            }
        }

        protected sealed override void StopTrackingDataForParameter(IParameterSymbol parameter, AnalysisEntity analysisEntity)
            => throw new InvalidOperationException("Unreachable");

        protected sealed override void StopTrackingDataForParameters(ImmutableDictionary<IParameterSymbol, AnalysisEntity> parameterEntities)
        {
            if (!parameterEntities.IsEmpty)
            {
                using var allEntities = PooledHashSet<AnalysisEntity>.GetInstance();
                AddTrackedEntities(allEntities);

                foreach (var (parameter, parameterEntity) in parameterEntities)
                {
                    StopTrackingDataForEntity(parameterEntity, CurrentAnalysisData, allEntities);

                    if (parameter.IsParams)
                    {
                        StopTrackingDataForParamArrayParameterIndices(parameterEntity, CurrentAnalysisData, allEntities);
                    }
                }
            }
        }

        protected override TAnalysisData GetMergedAnalysisDataForPossibleThrowingOperation(TAnalysisData? existingData, IOperation operation)
        {
            // Get tracked entities.
            using var entitiesBuilder = PooledHashSet<AnalysisEntity>.GetInstance();
            AddTrackedEntities(entitiesBuilder);

            // Only non-child entities are tracked for now.
            var resultAnalysisData = GetTrimmedCurrentAnalysisData(entitiesBuilder.Where(e => !e.IsChildOrInstanceMember && HasAbstractValue(e)));
            if (existingData != null)
            {
                var mergedAnalysisData = MergeAnalysisData(resultAnalysisData, existingData);
                resultAnalysisData.Dispose();
                resultAnalysisData = mergedAnalysisData;
            }

            return resultAnalysisData;
        }

        #region Helper methods to handle initialization/assignment operations
        protected override void SetAbstractValueForArrayElementInitializer(IArrayCreationOperation arrayCreation, ImmutableArray<AbstractIndex> indices, ITypeSymbol elementType, IOperation initializer, TAbstractAnalysisValue value)
        {
            if (AnalysisEntityFactory.TryCreateForArrayElementInitializer(arrayCreation, indices, elementType, out var analysisEntity))
            {
                SetAbstractValueForAssignment(analysisEntity, initializer, value);
            }
        }

        protected override void SetAbstractValueForAssignment(IOperation target, IOperation? assignedValueOperation, TAbstractAnalysisValue assignedValue, bool mayBeAssignment = false)
        {
            if (AnalysisEntityFactory.TryCreate(target, out var targetAnalysisEntity))
            {
                if (!targetAnalysisEntity.ShouldBeTrackedForAnalysis(HasCompletePointsToAnalysisResult))
                {
                    // We are not tracking points to values for fields and properties.
                    // So, it is not possible to accurately track value changes to target entity which is a member.
                    // Conservatively assume that the entity is assigned an unknown value.
                    assignedValue = ValueDomain.UnknownOrMayBeValue;
                }
                else if (mayBeAssignment)
                {
                    assignedValue = ValueDomain.Merge(GetAbstractValue(targetAnalysisEntity), assignedValue);
                }

                SetAbstractValueForAssignment(targetAnalysisEntity, assignedValueOperation, assignedValue);
            }
        }

        protected override void SetAbstractValueForTupleElementAssignment(AnalysisEntity tupleElementEntity, IOperation assignedValueOperation, TAbstractAnalysisValue assignedValue)
        {
            if (tupleElementEntity.Type.IsTupleType)
            {
                // For nested tuple entity, we only want to flow the value for the tuple, not it's children.
                // Children of nested tuples should have already been assigned when visiting the nested tuple operation in the base dataflow visitor.
                SetAbstractValue(tupleElementEntity, assignedValue);
            }
            else
            {
                SetAbstractValueForAssignment(tupleElementEntity, assignedValueOperation, assignedValue);
            }
        }

        protected virtual void SetAbstractValueForAssignment(AnalysisEntity targetAnalysisEntity, IOperation? assignedValueOperation, TAbstractAnalysisValue assignedValue)
        {
            AnalysisEntity? assignedValueEntity = null;
            if (assignedValueOperation != null)
            {
                var success = AnalysisEntityFactory.TryCreate(assignedValueOperation, out assignedValueEntity);
                Debug.Assert(success || assignedValueEntity == null);
            }

            SetAbstractValueForAssignment(targetAnalysisEntity, assignedValueEntity, assignedValueOperation, assignedValue);
        }

        private void SetAbstractValueForAssignment(AnalysisEntity targetAnalysisEntity, AnalysisEntity? assignedValueEntity, IOperation? assignedValueOperation, TAbstractAnalysisValue assignedValue)
        {
            // Value type and string type assignment has copy semantics.
            if (HasPointsToAnalysisResult &&
                targetAnalysisEntity.Type.HasValueCopySemantics())
            {
                // Reset the analysis values for analysis entities within the target instance.
                ResetValueTypeInstanceAnalysisData(targetAnalysisEntity);

                // Transfer the values of symbols from the assigned instance to the analysis entities in the target instance.
                TransferValueTypeInstanceAnalysisDataForAssignment(targetAnalysisEntity, assignedValueEntity, assignedValueOperation);
            }

            var addressSharedCopyValue = TryGetAddressSharedCopyValue(targetAnalysisEntity);
            if (addressSharedCopyValue != null)
            {
                Debug.Assert(addressSharedCopyValue.AnalysisEntities.Contains(targetAnalysisEntity));
                foreach (var entity in addressSharedCopyValue.AnalysisEntities)
                {
                    SetAbstractValue(entity, assignedValue);
                }
            }
            else
            {
                SetAbstractValue(targetAnalysisEntity, assignedValue);
            }
        }

        protected override void SetValueForParameterOnEntry(IParameterSymbol parameter, AnalysisEntity analysisEntity, ArgumentInfo<TAbstractAnalysisValue>? assignedValue)
        {
            Debug.Assert(SymbolEqualityComparer.Default.Equals(analysisEntity.Symbol, parameter));
            if (assignedValue != null)
            {
                SetAbstractValueForAssignment(analysisEntity, assignedValue.Operation, assignedValue.Value);
            }
            else
            {
                SetAbstractValue(analysisEntity, GetDefaultValueForParameterOnEntry(parameter, analysisEntity));
            }
        }

        protected override void EscapeValueForParameterOnExit(IParameterSymbol parameter, AnalysisEntity analysisEntity)
        {
            Debug.Assert(Equals(analysisEntity.Symbol, parameter));
            if (parameter.RefKind != RefKind.None)
            {
                SetAbstractValue(analysisEntity, GetDefaultValueForParameterOnExit(analysisEntity.Type));
            }
        }

        protected virtual TAbstractAnalysisValue GetDefaultValueForParameterOnEntry(IParameterSymbol parameter, AnalysisEntity analysisEntity) => ValueDomain.UnknownOrMayBeValue;
        protected virtual TAbstractAnalysisValue GetDefaultValueForParameterOnExit(ITypeSymbol parameterType) => ValueDomain.UnknownOrMayBeValue;

        #endregion

        #region Helper methods for reseting/transfer instance analysis data when PointsTo analysis results are available

        /// <summary>
        /// Resets all the analysis data for all <see cref="AnalysisEntity"/> instances that share the same <see cref="AnalysisEntity.InstanceLocation"/>
        /// as the given <paramref name="analysisEntity"/>.
        /// </summary>
        /// <param name="analysisEntity"></param>
        protected override void ResetValueTypeInstanceAnalysisData(AnalysisEntity analysisEntity)
        {
            Debug.Assert(HasPointsToAnalysisResult);
            Debug.Assert(analysisEntity.Type.HasValueCopySemantics());

            IEnumerable<AnalysisEntity> dependantAnalysisEntities = GetChildAnalysisEntities(analysisEntity);
            ResetInstanceAnalysisDataCore(dependantAnalysisEntities.Concat(analysisEntity));
        }

        protected override void ResetReferenceTypeInstanceAnalysisData(PointsToAbstractValue pointsToAbstractValue)
        {
            Debug.Assert(HasPointsToAnalysisResult);

            IEnumerable<AnalysisEntity> dependantAnalysisEntities = GetChildAnalysisEntities(pointsToAbstractValue);
            ResetInstanceAnalysisDataCore(dependantAnalysisEntities);
        }

        /// <summary>
        /// Resets the analysis data for the given <paramref name="dependantAnalysisEntities"/>.
        /// </summary>
        /// <param name="dependantAnalysisEntities"></param>
        private void ResetInstanceAnalysisDataCore(IEnumerable<AnalysisEntity> dependantAnalysisEntities)
        {
            foreach (var dependentAnalysisEntity in dependantAnalysisEntities)
            {
                ResetAbstractValue(dependentAnalysisEntity);
            }
        }

        /// <summary>
        /// Transfers the analysis data rooted from <paramref name="valueAnalysisEntity"/> or <paramref name="assignedValueOperation"/> to <paramref name="targetAnalysisEntity"/>, for a value type assignment operation.
        /// This involves transfer of data for of all <see cref="AnalysisEntity"/> instances that share the same <see cref="AnalysisEntity.InstanceLocation"/> as <paramref name="valueAnalysisEntity"/> or allocation for the <paramref name="assignedValueOperation"/>
        /// to all <see cref="AnalysisEntity"/> instances that share the same <see cref="AnalysisEntity.InstanceLocation"/> as <paramref name="targetAnalysisEntity"/>.
        /// </summary>
        private void TransferValueTypeInstanceAnalysisDataForAssignment(AnalysisEntity targetAnalysisEntity, AnalysisEntity? valueAnalysisEntity, IOperation? assignedValueOperation)
        {
            Debug.Assert(HasPointsToAnalysisResult);
            Debug.Assert(targetAnalysisEntity.Type.HasValueCopySemantics());

            IEnumerable<AnalysisEntity> dependentAnalysisEntities;
            if (valueAnalysisEntity != null)
            {
                if (!valueAnalysisEntity.Type.HasValueCopySemantics())
                {
                    // Unboxing conversion from assigned value (reference type) to target (value copy semantics).
                    // We do not need to transfer any data for such a case as there is no entity for unboxed value.
                    return;
                }

                dependentAnalysisEntities = GetChildAnalysisEntities(valueAnalysisEntity);
            }
            else if (assignedValueOperation != null)
            {
                // For allocations.
                PointsToAbstractValue newValueLocation = GetPointsToAbstractValue(assignedValueOperation);
                dependentAnalysisEntities = GetChildAnalysisEntities(newValueLocation);
            }
            else
            {
                return;
            }

            foreach (AnalysisEntity dependentInstance in dependentAnalysisEntities)
            {
                // Clone the dependent instance but with target as the root.
                AnalysisEntity newAnalysisEntity = AnalysisEntityFactory.CreateWithNewInstanceRoot(dependentInstance, targetAnalysisEntity);
                var dependentValue = GetAbstractValue(dependentInstance);
                SetAbstractValue(newAnalysisEntity, dependentValue);
            }
        }

        private ImmutableHashSet<AnalysisEntity> GetChildAnalysisEntities(AnalysisEntity analysisEntity)
        {
            // PERF: If we do not have complete points to analysis data, then there cannot be any
            // child entities for reference type entities.
            if (!HasCompletePointsToAnalysisResult && analysisEntity.Type.IsReferenceType)
            {
                return ImmutableHashSet<AnalysisEntity>.Empty;
            }

            return GetChildAnalysisEntities(analysisEntity.InstanceLocation, entity => IsChildAnalysisEntity(entity, analysisEntity));
        }

        private static IEnumerable<AnalysisEntity> GetChildAnalysisEntities(AnalysisEntity analysisEntity, HashSet<AnalysisEntity> allEntities)
        {
            Debug.Assert(analysisEntity.Type.HasValueCopySemantics());

            foreach (var entity in allEntities)
            {
                if (IsChildAnalysisEntity(entity, ancestorEntity: analysisEntity))
                {
                    yield return entity;
                }
            }
        }

        protected static bool IsChildAnalysisEntity(AnalysisEntity entity, AnalysisEntity ancestorEntity)
        {
            return (!ancestorEntity.Type.HasValueCopySemantics() || entity.HasAncestor(ancestorEntity)) &&
                IsChildAnalysisEntity(entity, ancestorEntity.InstanceLocation);
        }

        protected ImmutableHashSet<AnalysisEntity> GetChildAnalysisEntities(PointsToAbstractValue? instanceLocation)
        {
            return GetChildAnalysisEntities(instanceLocation, predicate: null);
        }

        private ImmutableHashSet<AnalysisEntity> GetChildAnalysisEntities(PointsToAbstractValue? instanceLocation, Func<AnalysisEntity, bool>? predicate)
        {
            // We are interested only in dependent child/member infos, not the root info.
            if (instanceLocation == null || instanceLocation.Kind == PointsToAbstractValueKind.Unknown)
            {
                return ImmutableHashSet<AnalysisEntity>.Empty;
            }

            predicate ??= entity => IsChildAnalysisEntity(entity, instanceLocation);

            return GetChildAnalysisEntities(predicate);
        }

        protected static bool IsChildAnalysisEntity(AnalysisEntity entity, PointsToAbstractValue instanceLocation)
        {
            return instanceLocation != PointsToAbstractValue.NoLocation &&
                entity.InstanceLocation.Equals(instanceLocation) &&
                entity.IsChildOrInstanceMember;
        }

        private ImmutableHashSet<AnalysisEntity> GetChildAnalysisEntities(Func<AnalysisEntity, bool> predicate)
        {
            var trackedEntitiesBuilder = PooledHashSet<AnalysisEntity>.GetInstance();
            AddTrackedEntities(trackedEntitiesBuilder);
            trackedEntitiesBuilder.RemoveWhere(entity => !predicate(entity));
            return trackedEntitiesBuilder.ToImmutableAndFree();
        }

        #endregion

        #region Interprocedural analysis
        protected override (TAnalysisData Data, bool IsTrimmed) GetInitialInterproceduralAnalysisData(
            IMethodSymbol invokedMethod,
            ControlFlowGraph invokedCfg,
            (AnalysisEntity? Instance, PointsToAbstractValue PointsToValue)? invocationInstance,
            (AnalysisEntity Instance, PointsToAbstractValue PointsToValue)? thisOrMeInstanceForCaller,
            ImmutableDictionary<IParameterSymbol, ArgumentInfo<TAbstractAnalysisValue>> argumentValuesMap,
            ImmutableDictionary<ISymbol, PointsToAbstractValue> capturedVariablesMap,
            IDictionary<AnalysisEntity, PointsToAbstractValue>? pointsToValues,
            IDictionary<AnalysisEntity, CopyAbstractValue>? copyValues,
            IDictionary<AnalysisEntity, ValueContentAbstractValue>? valueContentValues,
            bool isLambdaOrLocalFunction,
            bool hasParameterWithDelegateType)
        {
            // Retain analysis entities reachable from the invocation instance, arguments, this or me instance,
            // and any variables captured by a local function. A lambda or local function invoked
            // from a different method can have captured caller state not reachable from that
            // method's immediate invocation context.
            // Additionally, we also retain the transitive closure of analysis entities reachable from these
            // entities via the PointsTo values chain (i.e., recursively compute child analysis entities).
            // Static state and members reachable through untracked references are also retained.
            // Unrelated local state is excluded.

            if (hasParameterWithDelegateType ||
                pointsToValues is null ||
                (isLambdaOrLocalFunction &&
                 (invokedMethod.MethodKind != MethodKind.LocalFunction ||
                  !SymbolEqualityComparer.Default.Equals(invokedMethod.ContainingSymbol, DataFlowAnalysisContext.OwningSymbol) ||
                  CurrentAnalysisData is AnalysisEntityBasedPredicateAnalysisData<TAbstractAnalysisValue> { HasPredicatedData: true } ||
                  HasIndirectCapturePaths(invokedCfg, pointsToValues))))
            {
                return base.GetInitialInterproceduralAnalysisData(invokedMethod, invokedCfg, invocationInstance,
                    thisOrMeInstanceForCaller, argumentValuesMap, capturedVariablesMap, pointsToValues, copyValues, valueContentValues,
                    isLambdaOrLocalFunction, hasParameterWithDelegateType);
            }

            using var candidateEntitiesBuilder = PooledHashSet<AnalysisEntity>.GetInstance();
            using var interproceduralEntitiesToRetainBuilder = PooledHashSet<AnalysisEntity>.GetInstance();
            using var worklistEntities = PooledHashSet<AnalysisEntity>.GetInstance();
            using var processedEntities = PooledHashSet<AnalysisEntity>.GetInstance();
            using var worklistPointsToValues = PooledHashSet<PointsToAbstractValue>.GetInstance();
            using var processedPointsToValues = PooledHashSet<PointsToAbstractValue>.GetInstance();
            using var childWorklistEntities = PooledHashSet<AnalysisEntity>.GetInstance();
            using var intermediateEntities = PooledHashSet<AnalysisEntity>.GetInstance();

            // All tracked entities are candidates to be retained for initial interprocedural
            // analysis data.
            AddTrackedEntities(candidateEntitiesBuilder, forInterproceduralAnalysis: true);
            var candidateEntitiesCount = candidateEntitiesBuilder.Count;

            var (referencedMembers, hasArrayAccess) = GetReferencedMembers(invokedCfg);

            // Members without values in this analysis can still lead to tracked members
            // through points-to values when they are referenced by the callee.
            foreach (var (entity, pointsToValue) in pointsToValues)
            {
                if (!candidateEntitiesBuilder.Contains(entity) &&
                    entity.IsChildOrInstanceMember &&
                    ShouldProcessPointsToValue(pointsToValue) &&
                    (entity.Symbol is not null ? referencedMembers.Contains(entity.Symbol) : hasArrayAccess))
                {
                    intermediateEntities.Add(entity);
                }
            }

            // Add entities and PointsTo values for invocation instance, this or me instance
            // and argument values to the initial worklist

            if (invocationInstance.HasValue)
            {
                AddWorklistEntityAndPointsToValue(invocationInstance.Value.Instance, isRoot: true);
                AddWorklistPointsToValue(invocationInstance.Value.PointsToValue);
            }

            if (thisOrMeInstanceForCaller.HasValue)
            {
                AddWorklistEntityAndPointsToValue(thisOrMeInstanceForCaller.Value.Instance, isRoot: true);
                AddWorklistPointsToValue(thisOrMeInstanceForCaller.Value.PointsToValue);
            }

            foreach (var argument in argumentValuesMap.Values)
            {
                if (!AddWorklistEntityAndPointsToValue(argument.AnalysisEntity, isRoot: true))
                {
                    // For allocations passed as arguments.
                    AddWorklistPointsToValue(argument.InstanceLocation);
                }
            }

            if (invokedMethod.MethodKind == MethodKind.LocalFunction)
            {
                foreach (var capturedVariable in capturedVariablesMap.Keys)
                {
                    if (AnalysisEntityFactory.TryCreateForSymbolDeclaration(capturedVariable, out var capturedEntity))
                    {
                        AddWorklistEntityAndPointsToValue(capturedEntity, isRoot: true);
                    }
                }
            }

            foreach (var entity in candidateEntitiesBuilder)
            {
                if (entity.Symbol?.IsStatic == true)
                {
                    AddWorklistEntityAndPointsToValue(entity);
                }
            }

            foreach (var entity in pointsToValues.Keys)
            {
                if (entity.Symbol?.IsStatic == true)
                {
                    AddWorklistEntityAndPointsToValue(entity, isRoot: true);
                }
            }

            // Worklist based algorithm to compute the transitive closure of analysis entities
            // that are accessible in the callee via the PointsTo value chain.
            while (worklistEntities.Count > 0 || worklistPointsToValues.Count > 0)
            {
                if (worklistEntities.Count > 0)
                {
                    foreach (var entity in worklistEntities)
                    {
                        if (candidateEntitiesBuilder.Contains(entity))
                        {
                            interproceduralEntitiesToRetainBuilder.Add(entity);
                        }
                    }

                    // Remove the worklistEntities from tracked candidate entities.
                    candidateEntitiesBuilder.ExceptWith(worklistEntities);

                    // Add child entities of worklistEntities to childWorklistEntities.
                    // PERF: We cannot have any child entities for PointsToAnalysis if we
                    // not computing complete PointsToAnalysis data.
                    if (HasCompletePointsToAnalysisResult || !IsPointsToAnalysis)
                    {
                        foreach (var candidateEntity in candidateEntitiesBuilder)
                        {
                            foreach (var ancestorEntity in worklistEntities)
                            {
                                if (IsChildAnalysisEntity(candidateEntity, ancestorEntity))
                                {
                                    childWorklistEntities.Add(candidateEntity);
                                    break;
                                }
                            }
                        }

                        foreach (var intermediateEntity in intermediateEntities)
                        {
                            if (processedEntities.Contains(intermediateEntity))
                            {
                                continue;
                            }

                            foreach (var ancestorEntity in worklistEntities)
                            {
                                if (IsChildAnalysisEntity(intermediateEntity, ancestorEntity))
                                {
                                    childWorklistEntities.Add(intermediateEntity);
                                    break;
                                }
                            }
                        }
                    }

                    worklistEntities.Clear();
                }

                if (worklistPointsToValues.Count > 0)
                {
                    // Add child entities which are accessible from PointsTo chain to childWorklistEntities.
                    // PERF: We cannot have any child entities for PointsToAnalysis if we
                    // not computing complete PointsToAnalysis data.
                    if (HasCompletePointsToAnalysisResult || !IsPointsToAnalysis)
                    {
                        foreach (var candidateEntity in candidateEntitiesBuilder)
                        {
                            foreach (var pointsToValue in worklistPointsToValues)
                            {
                                Debug.Assert(ShouldProcessPointsToValue(pointsToValue));
                                if (IsChildAnalysisEntity(candidateEntity, pointsToValue))
                                {
                                    childWorklistEntities.Add(candidateEntity);
                                    break;
                                }
                            }
                        }

                        foreach (var intermediateEntity in intermediateEntities)
                        {
                            if (processedEntities.Contains(intermediateEntity))
                            {
                                continue;
                            }

                            foreach (var pointsToValue in worklistPointsToValues)
                            {
                                if (IsChildAnalysisEntity(intermediateEntity, pointsToValue))
                                {
                                    childWorklistEntities.Add(intermediateEntity);
                                    break;
                                }
                            }
                        }
                    }

                    worklistPointsToValues.Clear();
                }

                // Move all the child work list entities and their PointsTo values to the worklist.
                foreach (var childEntity in childWorklistEntities)
                {
                    AddWorklistEntityAndPointsToValue(childEntity, isRoot: true);
                }

                childWorklistEntities.Clear();
            }

            // If all candidates being retained, just retain the cloned current analysis data.
            if (interproceduralEntitiesToRetainBuilder.Count == candidateEntitiesCount)
            {
                return (GetClonedCurrentAnalysisData(), false);
            }

            // Otherwise, return cloned current analysis data with trimmed keys.
            return (GetTrimmedCurrentAnalysisData(interproceduralEntitiesToRetainBuilder), true);

            // Local functions.
            bool AddWorklistEntityAndPointsToValue(AnalysisEntity? analysisEntity, bool isRoot = false)
            {
                RoslynDebug.Assert(pointsToValues != null);

                var isCandidate = analysisEntity is not null && candidateEntitiesBuilder.Contains(analysisEntity);
                if (analysisEntity is not null && (isCandidate || isRoot) && processedEntities.Add(analysisEntity))
                {
                    worklistEntities.Add(analysisEntity);

                    if (pointsToValues.TryGetValue(analysisEntity, out var pointsToValue))
                    {
                        AddWorklistPointsToValue(pointsToValue);
                    }
                }

                return isCandidate;
            }

            void AddWorklistPointsToValue(PointsToAbstractValue pointsToValue)
            {
                if (ShouldProcessPointsToValue(pointsToValue) &&
                    processedPointsToValues.Add(pointsToValue))
                {
                    worklistPointsToValues.Add(pointsToValue);
                }
            }

            static bool ShouldProcessPointsToValue(PointsToAbstractValue pointsToValue)
                => pointsToValue.Kind == PointsToAbstractValueKind.KnownLocations &&
                   pointsToValue != PointsToAbstractValue.NoLocation;
        }

        private bool HasIndirectCapturePaths(
            ControlFlowGraph invokedCfg,
            IDictionary<AnalysisEntity, PointsToAbstractValue> pointsToValues)
        {
            var hasStaticState = pointsToValues.Keys.Any(entity => entity.Symbol?.IsStatic == true);
            var referencedStaticMembers = GetReferencedStaticMembers();
            var key = (Cfg: invokedCfg, CheckReturnedAliases: true, HasStaticState: hasStaticState);
            if (_safeIndirectCapturePaths?.Contains(key) == true)
            {
                return false;
            }

            using var activeMethods = PooledHashSet<IMethodSymbol>.GetInstance();
            if (HasIndirectCapturePaths(invokedCfg, activeMethods, referencedStaticMembers,
                checkReturnedAliases: true, hasStaticState))
            {
                return true;
            }

            (_safeIndirectCapturePaths ??= new()).Add(key);
            return false;
        }

        private (ImmutableHashSet<ISymbol> Members, bool HasArrayAccess) GetReferencedMembers(ControlFlowGraph invokedCfg)
        {
            if (_referencedMembersByCfg?.TryGetValue(invokedCfg, out var cached) == true)
            {
                return cached;
            }

            using var members = PooledHashSet<ISymbol>.GetInstance();
            using var visitedMethods = PooledHashSet<IMethodSymbol>.GetInstance();
            var hasArrayAccess = false;
            CollectReferencedMembers(invokedCfg, members, visitedMethods, ref hasArrayAccess);
            var result = (ImmutableHashSet.CreateRange(SymbolEqualityComparer.Default, members), hasArrayAccess);
            (_referencedMembersByCfg ??= new()).Add(invokedCfg, result);
            return result;
        }

        private ImmutableHashSet<ISymbol> GetReferencedStaticMembers()
        {
            if (_referencedStaticMembers is not null)
            {
                return _referencedStaticMembers;
            }

            var builder = ImmutableHashSet.CreateBuilder<ISymbol>(SymbolEqualityComparer.Default);
            var root = DataFlowAnalysisContext.ControlFlowGraph.OriginalOperation;
            if (DataFlowAnalysisContext.OwningSymbol is IMethodSymbol owningMethod)
            {
                // The caller can reach a static root without referring to its symbol through a
                // receiver, parameter, or alias returned by an ordinary method.
                _hasPotentialCallerAliases = !owningMethod.IsStatic ||
                    owningMethod.Parameters.Any(parameter => parameter.Type.IsReferenceType && !parameter.Type.IsPrimitiveType());
            }

            var worklist = new Stack<IOperation>();
            worklist.Push(root);
            while (worklist.Count > 0)
            {
                var operation = worklist.Pop();
                if (operation != root && operation is ILocalFunctionOperation or IAnonymousFunctionOperation)
                {
                    continue;
                }

                switch (operation)
                {
                    case IInvocationOperation invocation:
                        if (!invocation.TargetMethod.ReturnsVoid &&
                                !invocation.TargetMethod.ReturnType.IsPrimitiveType() &&
                                (invocation.TargetMethod.MethodKind != MethodKind.LocalFunction ||
                                 invocation.TargetMethod.ReturnType.IsReferenceType) ||
                            invocation.Arguments.Any(argument =>
                                argument.Parameter is { RefKind: RefKind.Ref or RefKind.Out } parameter &&
                                parameter.Type.IsReferenceType && !parameter.Type.IsPrimitiveType()))
                        {
                            _hasPotentialCallerAliases = true;
                        }

                        break;

                    case IObjectCreationOperation { Type: { IsReferenceType: true } }:
                        // A constructor can expose existing static state through a member of the new object.
                        _hasPotentialCallerAliases = true;
                        break;

                    case IFieldReferenceOperation { Field: { IsStatic: true } field }:
                        builder.Add(field);
                        break;

                    case IPropertyReferenceOperation { Property: { IsStatic: true } property }:
                        builder.Add(property);
                        break;
                }

                foreach (var child in operation.ChildOperations)
                {
                    if (child is not null)
                    {
                        worklist.Push(child);
                    }
                }
            }

            return _referencedStaticMembers = builder.ToImmutable();
        }

        private bool HasIndirectCapturePaths(
            ControlFlowGraph invokedCfg,
            PooledHashSet<IMethodSymbol> activeMethods,
            ImmutableHashSet<ISymbol> referencedStaticMembers,
            bool checkReturnedAliases,
            bool hasStaticState)
        {
            // GetCaptures includes lexical captures, but not captures of other local functions
            // or delegates invoked from this function. Passing a delegate can invoke one too.
            foreach (var operation in invokedCfg.OriginalOperation.Descendants())
            {
                if (operation is IInvocationOperation invocation)
                {
                    var target = invocation.TargetMethod;
                    if (target.MethodKind == MethodKind.DelegateInvoke ||
                        target.Parameters.Any(parameter => !parameter.Type.IsPrimitiveType() && parameter.Type.TypeKind != TypeKind.Enum) ||
                        checkReturnedAliases && target.MethodKind != MethodKind.LocalFunction &&
                            !target.ReturnsVoid && !target.ReturnType.IsPrimitiveType() && target.ReturnType.TypeKind != TypeKind.Enum ||
                        target.MethodKind != MethodKind.LocalFunction &&
                            invocation.Instance is { } receiver &&
                            (checkReturnedAliases && receiver is not IPropertyReferenceOperation { Property: { IsStatic: true } } ||
                             receiver is IFieldReferenceOperation { Field: { IsStatic: true } }))
                    {
                        return true;
                    }

                    if (target.MethodKind == MethodKind.LocalFunction)
                    {
                        // Static locals can still invoke captured delegates passed through a container.
                        if (!target.IsStatic ||
                            !activeMethods.Add(target))
                        {
                            return true;
                        }

                        var targetCfg = DataFlowAnalysisContext.GetLocalFunctionControlFlowGraph(target);
                        if (targetCfg is null)
                        {
                            return true;
                        }

                        var targetCheckReturnedAliases = hasStaticState || _hasPotentialCallerAliases ||
                            target.ReturnType.IsReferenceType && !target.ReturnType.IsPrimitiveType();
                        var key = (Cfg: targetCfg, CheckReturnedAliases: targetCheckReturnedAliases, HasStaticState: hasStaticState);
                        if (_safeIndirectCapturePaths?.Contains(key) != true)
                        {
                            if (HasIndirectCapturePaths(targetCfg, activeMethods, referencedStaticMembers,
                                targetCheckReturnedAliases, hasStaticState))
                            {
                                return true;
                            }

                            // Safe scans depend on the caller's static-state condition and the
                            // returned-alias check, not just the local function's symbol.
                            (_safeIndirectCapturePaths ??= new()).Add(key);
                        }

                        activeMethods.Remove(target);
                    }
                }
                else if (operation is IObjectCreationOperation { Constructor: IMethodSymbol constructor } &&
                         constructor.Parameters.Any(parameter => !parameter.Type.IsPrimitiveType() && parameter.Type.TypeKind != TypeKind.Enum))
                {
                    return true;
                }

                // Unknown static roots can be absent from points-to data even when the caller
                // tracks their members, so a shared root must prevent trimming as well.
                if (operation is IFieldReferenceOperation { Field: { IsStatic: true } field } &&
                    referencedStaticMembers.Contains(field) ||
                    operation is IPropertyReferenceOperation { Property: { IsStatic: true } property } &&
                    referencedStaticMembers.Contains(property))
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectReferencedMembers(
            ControlFlowGraph invokedCfg,
            PooledHashSet<ISymbol> referencedMembers,
            PooledHashSet<IMethodSymbol> visitedMethods,
            ref bool hasArrayAccess)
        {
            foreach (var operation in invokedCfg.OriginalOperation.Descendants())
            {
                switch (operation)
                {
                    case IFieldReferenceOperation fieldReference:
                        referencedMembers.Add(fieldReference.Field);
                        break;

                    case IPropertyReferenceOperation propertyReference:
                        referencedMembers.Add(propertyReference.Property);
                        break;

                    case IEventReferenceOperation eventReference:
                        referencedMembers.Add(eventReference.Event);
                        break;

                    case IArrayElementReferenceOperation:
                        hasArrayAccess = true;
                        break;

                    case IInvocationOperation { TargetMethod: { MethodKind: MethodKind.LocalFunction, IsStatic: true } target }
                        when visitedMethods.Add(target):
                        var targetCfg = DataFlowAnalysisContext.GetLocalFunctionControlFlowGraph(target);
                        if (targetCfg is not null)
                        {
                            CollectReferencedMembers(targetCfg, referencedMembers, visitedMethods, ref hasArrayAccess);
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Returns a cloned CurrentAnalysisData, trimmed down to only have key-value pairs for the given <paramref name="withEntities"/>.
        /// </summary>
        protected abstract TAnalysisData GetTrimmedCurrentAnalysisData(IEnumerable<AnalysisEntity> withEntities);

        protected TAnalysisData GetTrimmedCurrentAnalysisDataHelper(
            IEnumerable<AnalysisEntity> withEntities,
            IDictionary<AnalysisEntity, TAbstractAnalysisValue> existingValues,
            Action<TAnalysisData, AnalysisEntity, TAbstractAnalysisValue> setAbstractValue)
        {
            var initialAnalysisData = GetEmptyAnalysisData();
            foreach (var entity in withEntities)
            {
                setAbstractValue(initialAnalysisData, entity, existingValues[entity]);
            }

            return initialAnalysisData;
        }

        protected abstract void ApplyInterproceduralAnalysisResultCore(TAnalysisData resultData);

        protected sealed override void ApplyInterproceduralAnalysisResult(
            TAnalysisData resultData,
            bool isLambdaOrLocalFunction,
            bool hasDelegateTypeArgument,
            TAnalysisResult analysisResult,
            bool initialDataIsTrimmed)
        {
            if (isLambdaOrLocalFunction && !initialDataIsTrimmed || hasDelegateTypeArgument)
            {
                base.ApplyInterproceduralAnalysisResult(resultData, isLambdaOrLocalFunction, hasDelegateTypeArgument, analysisResult, initialDataIsTrimmed);
                return;
            }

            ApplyInterproceduralAnalysisResultCore(resultData);
        }

        protected void ApplyInterproceduralAnalysisResultHelper(IDictionary<AnalysisEntity, TAbstractAnalysisValue> resultToApply)
        {
            foreach (var kvp in resultToApply)
            {
                var entity = kvp.Key;
                var newValue = kvp.Value;
                var currentValue = GetAbstractValue(entity);
                if (!currentValue.Equals(newValue))
                {
                    SetAbstractValue(entity, newValue);
                }
            }
        }

        internal bool ShouldStopTrackingEntityAtExit(AnalysisEntity entity)
        {
            Debug.Assert(DataFlowAnalysisContext.InterproceduralAnalysisData != null);

            // Filter out all the parameter, local symbol and flow capture entities from the analysis data.
            return IsParameterEntityForCurrentMethod(entity) ||
                entity.Symbol?.Kind == SymbolKind.Local &&
                entity.Symbol.ContainingSymbol.Equals(DataFlowAnalysisContext.OwningSymbol) ||
                entity.CaptureId.HasValue &&
                entity.CaptureId.Value.ControlFlowGraph == DataFlowAnalysisContext.ControlFlowGraph;
        }

        public override TAnalysisData? GetMergedDataForUnhandledThrowOperations()
        {
            // For interprocedural analysis, prune analysis data for unhandled exceptions
            // to remove analysis entities that are only valid in the callee.
            if (DataFlowAnalysisContext.InterproceduralAnalysisData != null &&
                AnalysisDataForUnhandledThrowOperations != null &&
                AnalysisDataForUnhandledThrowOperations.Values.Any(HasAnyAbstractValue))
            {
                using var allAnalysisEntities = PooledHashSet<AnalysisEntity>.GetInstance();

                foreach (var dataAtException in AnalysisDataForUnhandledThrowOperations.Values)
                {
                    AddTrackedEntities(dataAtException, allAnalysisEntities, forInterproceduralAnalysis: true);
                }

                foreach (var entity in allAnalysisEntities)
                {
                    if (ShouldStopTrackingEntityAtExit(entity))
                    {
                        foreach (var dataAtException in AnalysisDataForUnhandledThrowOperations.Values)
                        {
                            StopTrackingDataForEntity(entity, dataAtException, allAnalysisEntities);
                        }
                    }
                }
            }

            return base.GetMergedDataForUnhandledThrowOperations();
        }

        #endregion

        protected DictionaryAnalysisData<AnalysisEntity, TAbstractAnalysisValue> GetClonedAnalysisDataHelper(IDictionary<AnalysisEntity, TAbstractAnalysisValue> analysisData)
            => new(analysisData);

        protected void ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(
            DictionaryAnalysisData<AnalysisEntity, TAbstractAnalysisValue> coreDataAtException,
            DictionaryAnalysisData<AnalysisEntity, TAbstractAnalysisValue> coreCurrentAnalysisData,
            ThrownExceptionInfo throwBranchWithExceptionType)
        {
            Func<AnalysisEntity, bool>? predicate = null;
            if (throwBranchWithExceptionType.IsDefaultExceptionForExceptionsPathAnalysis)
            {
                // Only tracking non-child analysis entities for exceptions path analysis for now.
                Debug.Assert(throwBranchWithExceptionType.ExceptionType.Equals(ExceptionNamedType));
                predicate = e => !e.IsChildOrInstanceMember;
            }

            base.ApplyMissingCurrentAnalysisDataForUnhandledExceptionData(coreDataAtException, coreCurrentAnalysisData, predicate);
        }

        #region Visitor methods

        public override TAbstractAnalysisValue VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation, object? argument)
        {
            var value = base.VisitDeconstructionAssignment(operation, argument);
            var assignedInstance = GetPointsToAbstractValue(operation.Value);
            HandleDeconstructionAssignment(operation.Target, GetChildAnalysisEntities(assignedInstance));
            return value;
        }

        private void HandleDeconstructionAssignment(IOperation target, ImmutableHashSet<AnalysisEntity> childEntities)
        {
            if (target is IDeclarationExpressionOperation declarationExpressionOperation)
            {
                target = declarationExpressionOperation.Expression;
            }

            if (target is ITupleOperation tupleOperation &&
                AnalysisEntityFactory.TryCreateForTupleElements(tupleOperation, out var tupleElementEntities))
            {
                Debug.Assert(tupleOperation.Elements.Length == tupleElementEntities.Length);
                for (int i = 0; i < tupleOperation.Elements.Length; i++)
                {
                    var element = tupleOperation.Elements[i];
                    var tupleElementEntity = tupleElementEntities[i];
                    if (element is ITupleOperation tupleElement)
                    {
                        Debug.Assert(tupleElementEntity.Symbol is IFieldSymbol field);
                        HandleDeconstructionAssignment(tupleElement, childEntities);
                    }
                    else if (AnalysisEntityFactory.TryCreate(element, out var elementEntity))
                    {
                        AnalysisEntity? assignedValueEntity = childEntities.FirstOrDefault(c => IsMatchingAssignedEntity(tupleElementEntity, c));
                        var assignedValue = assignedValueEntity != null ? GetAbstractValue(assignedValueEntity) : ValueDomain.UnknownOrMayBeValue;
                        SetAbstractValueForAssignment(elementEntity, assignedValueEntity, assignedValueOperation: null, assignedValue);
                    }
                }
            }

            return;

            // Local function
            static bool IsMatchingAssignedEntity(AnalysisEntity tupleElementEntity, AnalysisEntity? childEntity)
            {
                if (childEntity == null)
                {
                    return false;
                }

                if (tupleElementEntity.Parent == null)
                {
                    // Root tuple entity, compare the underlying tuple types.
                    return childEntity.Parent == null &&
                        SymbolEqualityComparer.Default.Equals(tupleElementEntity.Type.OriginalDefinition, childEntity.Type.OriginalDefinition);
                }

                // Must be a tuple element field entity.
                return tupleElementEntity.Symbol is IFieldSymbol tupleElementField &&
                    childEntity.Symbol is IFieldSymbol childEntityField &&
                    SymbolEqualityComparer.Default.Equals(tupleElementField.OriginalDefinition, childEntityField.OriginalDefinition) &&
                    IsMatchingAssignedEntity(tupleElementEntity.Parent, childEntity.Parent);
            }
        }

        #endregion
    }
}