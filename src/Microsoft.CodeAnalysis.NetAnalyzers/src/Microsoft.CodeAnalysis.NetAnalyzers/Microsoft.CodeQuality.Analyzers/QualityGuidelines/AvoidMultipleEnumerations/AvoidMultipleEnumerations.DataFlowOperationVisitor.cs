// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        internal abstract class InvocationCountValueSetFlowOperationVisitor : GlobalFlowStateValueSetFlowOperationVisitor
        {
            private readonly ImmutableArray<IMethodSymbol> _wellKnownDelayExecutionMethods;
            private readonly ImmutableArray<IMethodSymbol> _wellKnownEnumerationMethods;
            private readonly IMethodSymbol? _getEnumeratorMethod;

            protected InvocationCountValueSetFlowOperationVisitor(GlobalFlowStateAnalysisContext analysisContext,
                ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
                ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods,
                IMethodSymbol? getEnumeratorMethod)
                : base(analysisContext, hasPredicatedGlobalState: true)
            {
                _wellKnownDelayExecutionMethods = wellKnownDelayExecutionMethods;
                _wellKnownEnumerationMethods = wellKnownEnumerationMethods;
                _getEnumeratorMethod = getEnumeratorMethod;
            }

            protected abstract bool IsExpressionOfForEachStatement(SyntaxNode node);

            public override GlobalFlowStateAnalysisValueSet VisitParameterReference(IParameterReferenceOperation operation, object? argument)
            {
                var value = base.VisitParameterReference(operation, argument);
                return operation.Parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    ? VisitLocalOrParameter(operation, operation.Parameter, value)
                    : value;
            }

            public override GlobalFlowStateAnalysisValueSet VisitLocalReference(ILocalReferenceOperation operation, object? argument)
            {
                var value = base.VisitLocalReference(operation, argument);
                return operation.Local.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    ? VisitLocalOrParameter(operation, operation.Local, value)
                    : value;
            }

            private GlobalFlowStateAnalysisValueSet VisitLocalOrParameter(
                IOperation operation, ISymbol symbol, GlobalFlowStateAnalysisValueSet defaultValue)
            {
                if (IsOperationEnumeratedByMethodInvocation(operation, _wellKnownDelayExecutionMethods, _wellKnownEnumerationMethods)
                    || IsGetEnumeratorOfForEachLoopInvoked(operation))
                {
                    var newValue = CreateAnalysisValueSet(symbol);
                    MergeAndSetGlobalState(newValue);
                    return newValue;
                }

                return defaultValue;
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

            private GlobalFlowStateAnalysisValueSet CreateAnalysisValueSet(ISymbol symbol)
            {
                var oneTimeAnalysisValue = new InvocationCountAnalysisValue(symbol, InvocationTimes.OneTime);
                var twoOrMoreTimesAnalysisValue = new InvocationCountAnalysisValue(symbol, InvocationTimes.TwoOrMore);

                if (ContainsAnalysisValue(twoOrMoreTimesAnalysisValue, GlobalState) || ContainsAnalysisValue(oneTimeAnalysisValue, GlobalState))
                {
                    return GlobalFlowStateAnalysisValueSet.Create(
                        ImmutableHashSet.Create<IAbstractAnalysisValue>(oneTimeAnalysisValue, twoOrMoreTimesAnalysisValue),
                        parents: ImmutableHashSet<GlobalFlowStateAnalysisValueSet>.Empty,
                        height: 0);
                }

                return GlobalFlowStateAnalysisValueSet.Create(oneTimeAnalysisValue);
            }

            private static bool ContainsAnalysisValue(InvocationCountAnalysisValue value, GlobalFlowStateAnalysisValueSet analysisValueSet)
            {
                if (analysisValueSet.Kind != GlobalFlowStateAnalysisValueSetKind.Known)
                {
                    return false;
                }

                if (analysisValueSet.AnalysisValues.Contains(value))
                {
                    return true;
                }

                if (!analysisValueSet.Parents.IsEmpty)
                {
                    var containsAnalysisValue = true;
                    foreach (var parentValueSet in analysisValueSet.Parents)
                    {
                        containsAnalysisValue &= ContainsAnalysisValue(value, parentValueSet);
                    }

                    return containsAnalysisValue;
                }

                return false;
            }

            private static bool IsImplicitConventionToIEnumerable(IConversionOperation conversionOperation)
                => conversionOperation.Conversion.IsImplicit
                   && conversionOperation.Type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }
    }
}