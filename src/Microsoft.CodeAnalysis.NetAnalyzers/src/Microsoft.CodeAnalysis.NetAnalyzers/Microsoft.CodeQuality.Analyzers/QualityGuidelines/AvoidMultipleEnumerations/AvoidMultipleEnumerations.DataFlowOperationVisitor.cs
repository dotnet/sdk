// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        internal abstract class AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor : GlobalFlowStateDictionaryFlowOperationVisitor
        {
            private readonly WellKnownSymbolsInfo _wellKnownSymbolsInfo;
            private readonly IMethodSymbol? _getEnumeratorMethod;

            protected AvoidMultipleEnumerationsFlowStateDictionaryFlowOperationVisitor(
                GlobalFlowStateDictionaryAnalysisContext analysisContext,
                WellKnownSymbolsInfo wellKnownSymbolsInfo,
                IMethodSymbol? getEnumeratorMethod) : base(analysisContext)
            {
                _wellKnownSymbolsInfo = wellKnownSymbolsInfo;
                _getEnumeratorMethod = getEnumeratorMethod;
            }

            protected abstract bool IsExpressionOfForEachStatement(SyntaxNode node);

            public override GlobalFlowStateDictionaryAnalysisValue VisitParameterReference(IParameterReferenceOperation operation, object? argument)
            {
                var value = base.VisitParameterReference(operation, argument);
                if (!IsDeferredType(operation.Parameter.Type.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                {
                    return value;
                }

                return CreateAndUpdateAnalysisValue(operation, value);
            }

            public override GlobalFlowStateDictionaryAnalysisValue VisitLocalReference(ILocalReferenceOperation operation, object? argument)
            {
                var value = base.VisitLocalReference(operation, argument);
                if (!IsDeferredType(operation.Local.Type.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                {
                    return value;
                }

                return CreateAndUpdateAnalysisValue(operation, value);
            }

            private GlobalFlowStateDictionaryAnalysisValue CreateAndUpdateAnalysisValue(IOperation operation, GlobalFlowStateDictionaryAnalysisValue defaultValue)
            {
                if (!IsOperationEnumeratedByMethodInvocation(operation, _wellKnownSymbolsInfo) && !IsGetEnumeratorOfForEachLoopInvoked(operation))
                {
                    return defaultValue;
                }

                if (!TryGetInvocationEntity(operation, out var invocationEntity))
                {
                    return defaultValue;
                }

                var newValue = CreateAnalysisValue(invocationEntity, operation, defaultValue);
                UpdateGlobalValue(newValue);
                return newValue;
            }

            private bool TryGetInvocationEntity(IOperation operation, [NotNullWhen(true)] out InvocationEntity? invocationEntity)
            {
                var pointToAnalysisResult = DataFlowAnalysisContext.PointsToAnalysisResult;
                invocationEntity = null;
                if (pointToAnalysisResult is null)
                {
                    return false;
                }

                var result = pointToAnalysisResult[operation.Kind, operation.Syntax];
                if (result.Kind != PointsToAbstractValueKind.KnownLocations)
                {
                    return false;
                }

                invocationEntity = new InvocationEntity(result.Locations);
                return true;
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
                if (!IsDeferredType(operationToCheck.Type.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes))
                {
                    return false;
                }

                // Check 1: Expression of ForEachLoop would be implicitly converted to IEnumerable.
                // Check 2: The result of the Conversion would be invoked by GetEnumerator method
                // Check 3: Make sure the linked syntax node is the expression of ForEachLoop. It can't be done by finding IForEachLoopOperation,
                // because the Operation in CFG doesn't have that information.
                return operationToCheck is IConversionOperation conversionOperation
                   && IsImplicitConventionToDeferredType(conversionOperation)
                   && conversionOperation.Parent is IInvocationOperation invocationOperation
                   && invocationOperation.TargetMethod.OriginalDefinition.Equals(_getEnumeratorMethod)
                   && IsExpressionOfForEachStatement(invocationOperation.Syntax);
            }

            private static GlobalFlowStateDictionaryAnalysisValue CreateAnalysisValue(
                InvocationEntity analysisEntity,
                IOperation operation,
                GlobalFlowStateDictionaryAnalysisValue value)
            {
                var invocationSet = new TrackingInvocationSet(ImmutableHashSet.Create(operation), InvocationCount.One);

                var analysisValue = new GlobalFlowStateDictionaryAnalysisValue(
                    ImmutableDictionary<InvocationEntity, TrackingInvocationSet>.Empty.Add(analysisEntity, invocationSet),
                    GlobalFlowStateDictionaryAnalysisValueKind.Known);

                return value.Kind == GlobalFlowStateDictionaryAnalysisValueKind.Known
                    ? GlobalFlowStateDictionaryAnalysisValue.Merge(analysisValue, value)
                    : analysisValue;
            }

            private bool IsImplicitConventionToDeferredType(IConversionOperation conversionOperation)
                => conversionOperation.Conversion.IsImplicit && IsDeferredType(conversionOperation.Type.OriginalDefinition, _wellKnownSymbolsInfo.AdditionalDeferredTypes);
        }
    }
}