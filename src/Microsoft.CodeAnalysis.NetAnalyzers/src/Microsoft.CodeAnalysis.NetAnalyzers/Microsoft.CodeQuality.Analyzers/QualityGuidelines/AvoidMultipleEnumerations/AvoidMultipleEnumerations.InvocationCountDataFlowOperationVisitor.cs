// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        internal class InvocationCountDataFlowOperationVisitor : GlobalFlowStateDataFlowOperationVisitor
        {
            private readonly ImmutableArray<IMethodSymbol> _wellKnownDelayExecutionMethods;
            private readonly ImmutableArray<IMethodSymbol> _wellKnownEnumerationMethods;

            public InvocationCountDataFlowOperationVisitor(GlobalFlowStateAnalysisContext analysisContext,
                ImmutableArray<IMethodSymbol> wellKnownDelayExecutionMethods,
                ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
                : base(analysisContext,
                    hasPredicatedGlobalState: true)
            {
                _wellKnownDelayExecutionMethods = wellKnownDelayExecutionMethods;
                _wellKnownEnumerationMethods = wellKnownEnumerationMethods;
            }

            public override GlobalFlowStateAnalysisValueSet VisitParameterReference(IParameterReferenceOperation operation, object? argument)
            {
                var value = base.VisitParameterReference(operation, argument);
                if (operation.Parameter.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && IsParameterOrLocalOrArrayElementEnumerated(operation, _wellKnownDelayExecutionMethods, _wellKnownEnumerationMethods))
                {


                }

                return value;
            }

            public override GlobalFlowStateAnalysisValueSet VisitLocalReference(ILocalReferenceOperation operation, object? argument)
            {
                var value = base.VisitLocalReference(operation, argument);
                if (operation.Local.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && IsParameterOrLocalOrArrayElementEnumerated(operation, _wellKnownDelayExecutionMethods, _wellKnownEnumerationMethods))
                {

                }

                return value;
            }

            public override GlobalFlowStateAnalysisValueSet VisitArrayElementReference(IArrayElementReferenceOperation operation, object? argument)
            {
                var value = base.VisitArrayElementReference(operation, argument);
                if (operation.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && IsParameterOrLocalOrArrayElementEnumerated(operation, _wellKnownDelayExecutionMethods, _wellKnownEnumerationMethods))
                {

                }

                return value;
            }

            private GlobalFlowStateAnalysisValueSet CreateAndMergeInvocationCountAnalysisValue(AnalysisEntity analysisEntity)
            {
                if (GlobalState.Kind == GlobalFlowStateAnalysisValueSetKind.Known)
                {
                    var trackingAnalysisValues = GlobalState.AnalysisValues;
                    var oneTimeInvocation = new InvocationCountAnalysisValue(analysisEntity, InvocationTimes.One);
                    if (trackingAnalysisValues.Contains(oneTimeInvocation))
                    {

                    }
                    else
                    {

                    }
                }
            }
        }
    }
}