// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        internal class InvocationCountDataFlowOperationVisitor : GlobalFlowStateDataFlowOperationVisitor
        {
            private readonly ImmutableArray<IMethodSymbol> _wellKnownEnumerationMethods;

            private readonly ImmutableArray<IMethodSymbol> _wellKnownDelayingMethods;

            public InvocationCountDataFlowOperationVisitor(
                GlobalFlowStateAnalysisContext analysisContext,
                ImmutableArray<IMethodSymbol> wellKnownEnumerationMethods)
                : base(analysisContext, hasPredicatedGlobalState: true)
            {
                _wellKnownEnumerationMethods = wellKnownEnumerationMethods;
            }

            public override GlobalFlowStateAnalysisValueSet VisitParameterReference(IParameterReferenceOperation operation, object? argument)
            {
                if (operation.Parameter.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                    && operation.Parent is IArgumentOperation argumentOperation)
                {

                }

                return base.VisitParameterReference(operation, argument);
            }

            public override GlobalFlowStateAnalysisValueSet VisitLocalReference(ILocalReferenceOperation operation, object? argument)
            {
                if (operation.Local.Type.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                {
                    var parent = operation.Parent;

                }

                return base.VisitLocalReference(operation, argument);
            }

            public override GlobalFlowStateAnalysisValueSet VisitFlowCapture(IFlowCaptureOperation operation, object? argument)
            {
                return base.VisitFlowCapture(operation, argument);
            }

            private bool IsInForEachLoop(IOperation operation)
            {
                if (operation.Parent.Parent is IForEachLoopOperation)
                {

                }

                return false;
            }

            public override GlobalFlowStateAnalysisValueSet VisitInvocation_NonLambdaOrDelegateOrLocalFunction(
                IMethodSymbol method,
                IOperation? visitedInstance,
                ImmutableArray<IArgumentOperation> visitedArguments,
                bool invokedAsDelegate,
                IOperation originalOperation,
                GlobalFlowStateAnalysisValueSet defaultValue)
            {
                var value = base.VisitInvocation_NonLambdaOrDelegateOrLocalFunction(method, visitedInstance, visitedArguments, invokedAsDelegate, originalOperation, defaultValue);

                if (_wellKnownEnumerationMethods.Contains(method)
                    && !visitedArguments.IsEmpty
                    && AnalysisEntityFactory.TryCreate(visitedArguments[0].Value, out var analysisEntity))
                {
                    if (HasAbstractValue(analysisEntity))
                    {
                        var existingAbstractValue = GetAbstractValue(analysisEntity);
                        var newAbstractValue = existingAbstractValue.WithAdditionalAnalysisValues(
                            GlobalFlowStateAnalysisValueSet.Create(new EnumerationInvocationAnalysisValue(originalOperation)), negate: false);
                        SetAbstractValue(analysisEntity, newAbstractValue);
                        return newAbstractValue;
                    }
                    else
                    {
                        var newAbstractValue = GlobalFlowStateAnalysisValueSet.Create(new EnumerationInvocationAnalysisValue(originalOperation));
                        SetAbstractValue(analysisEntity, newAbstractValue);
                        return newAbstractValue;
                    }
                }

                return value;
            }
        }
    }
}