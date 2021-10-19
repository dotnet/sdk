// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.FlowAnalysis.Analysis.InvocationCountAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        internal abstract class AvoidMultipleEnumerationsFlowOperationVisitor : InvocationCountDataFlowOperationVisitor
        {
            private readonly ImmutableArray<IMethodSymbol> _wellKnownDelayExecutionMethods;
            private readonly ImmutableArray<IMethodSymbol> _wellKnownEnumerationMethods;
            private readonly IMethodSymbol? _getEnumeratorMethod;

            protected AvoidMultipleEnumerationsFlowOperationVisitor(
                InvocationCountAnalysisContext analysisContext,
                ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
                ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
                IMethodSymbol? getEnumeratorMethod) : base(analysisContext)
            {
                _wellKnownDelayExecutionMethods = wellKnownDelayExecutionMethods;
                _wellKnownEnumerationMethods = wellKnownEnumerationMethods;
                _getEnumeratorMethod = getEnumeratorMethod;
            }

            protected abstract bool IsExpressionOfForEachStatement(SyntaxNode node);

            public override InvocationCountAnalysisValue VisitParameterReference(IParameterReferenceOperation operation, object? argument)
            {
                var value = base.VisitParameterReference(operation, argument);
                return operation.Parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && AnalysisEntityFactory.TryCreate(operation, out var analysisEntity)
                        ? VisitLocalOrParameterOrArrayElement(operation, analysisEntity, value)
                        : value;
            }

            public override InvocationCountAnalysisValue VisitLocalReference(ILocalReferenceOperation operation, object? argument)
            {
                var value = base.VisitLocalReference(operation, argument);
                return operation.Local.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && AnalysisEntityFactory.TryCreate(operation, out var analysisEntity)
                        ? VisitLocalOrParameterOrArrayElement(operation, analysisEntity, value)
                        : value;
            }

            public override InvocationCountAnalysisValue VisitArrayElementReference(IArrayElementReferenceOperation operation, object? argument)
            {
                var value = base.VisitArrayElementReference(operation, argument);
                return operation.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && AnalysisEntityFactory.TryCreate(operation, out var analysisEntity)
                        ? VisitLocalOrParameterOrArrayElement(operation, analysisEntity, value)
                        : value;
            }

            private InvocationCountAnalysisValue VisitLocalOrParameterOrArrayElement(
                IOperation operation, AnalysisEntity analysisEntity, InvocationCountAnalysisValue value)
            {
                if (IsOperationEnumeratedByMethodInvocation(operation, _wellKnownDelayExecutionMethods, _wellKnownEnumerationMethods)
                    || IsGetEnumeratorOfForEachLoopInvoked(operation))
                {
                    var newValue = CreateAnalysisValue(analysisEntity, operation, value);
                    UpdateGlobalValue(newValue);
                    return newValue;
                }

                return value;
            }

            private void UpdateGlobalValue(InvocationCountAnalysisValue value)
            {
                if (value.Kind == InvocationCountAnalysisValueKind.Known)
                {
                    var newState = InvocationCountAnalysisValue.Merge(GlobalState, value);
                    SetAbstractValue(GlobalEntity, newState);
                }
            }

            private bool IsGetEnumeratorOfForEachLoopInvoked(IOperation operation)
            {
                RoslynDebug.Assert(operation is ILocalReferenceOperation or IParameterReferenceOperation or IArrayElementReferenceOperation);
                var operationToCheck = SkipDelayExecutingMethodIfNeeded(operation, _wellKnownDelayExecutionMethods);

                // Make sure it has IEnumerable type, not some other type like list, array, etc...
                if (operationToCheck.Type.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T)
                {
                    return false;
                }

                // Check 1: Expression of ForEachLoop would be converted to IEnumerable.
                // Check 2: The result of the Conversion would be invoked by GetEnumerator method
                // Check 3: Make sure the linked syntax node is the expression of ForEachLoop. It can't be done by finding IForEachLoopOperation,
                // because the Operation in CFG doesn't have that information.
                return operationToCheck.Parent is IConversionOperation conversionOperation
                   && IsImplicitConventionToIEnumerable(conversionOperation)
                   && conversionOperation.Parent is IInvocationOperation invocationOperation
                   && invocationOperation.TargetMethod.OriginalDefinition.Equals(_getEnumeratorMethod)
                   && IsExpressionOfForEachStatement(invocationOperation.Syntax);
            }

            private static InvocationCountAnalysisValue CreateAnalysisValue(
                AnalysisEntity analysisEntity,
                IOperation operation,
                InvocationCountAnalysisValue value)
            {
                var invocationSet = new TrackingInvocationSet(ImmutableHashSet.Create(operation), InvocationCount.One);
                var builder = PooledDictionary<AnalysisEntity, TrackingInvocationSet>.GetInstance();
                builder.Add(analysisEntity, invocationSet);

                var analysisValue = new InvocationCountAnalysisValue(
                    builder.ToImmutableDictionary(),
                    InvocationCountAnalysisValueKind.Known);
                return value.Kind == InvocationCountAnalysisValueKind.Known
                    ? InvocationCountAnalysisValue.Merge(analysisValue, value)
                    : analysisValue;
            }

            private static bool IsImplicitConventionToIEnumerable(IConversionOperation conversionOperation)
                => conversionOperation.Conversion.IsImplicit
                   && conversionOperation.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }
    }
}