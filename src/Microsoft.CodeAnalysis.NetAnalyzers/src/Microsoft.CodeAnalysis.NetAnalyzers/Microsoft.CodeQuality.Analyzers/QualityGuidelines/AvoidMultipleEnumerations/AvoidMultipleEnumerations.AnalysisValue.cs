// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        private enum InvocationTimes
        {
            One,
            TwoOrMore,
            Unknown
        }

        private readonly struct InvocationCountAnalysisValue : IAbstractAnalysisValue
        {
            public AnalysisEntity EnumeratedEntity { get; }

            public InvocationTimes InvocationTimes { get; }

            public InvocationCountAnalysisValue(AnalysisEntity enumeratedEntity, InvocationTimes invocationTimes)
            {
                EnumeratedEntity = enumeratedEntity;
                InvocationTimes = invocationTimes;
            }

            public bool Equals(IAbstractAnalysisValue other)
            {
                if (other is InvocationCountAnalysisValue otherValue)
                {
                    return EnumeratedEntity.Equals(otherValue.EnumeratedEntity) && InvocationTimes == otherValue.InvocationTimes;
                }

                return false;
            }

            public IAbstractAnalysisValue GetNegatedValue()
            {
                return this;
            }
        }

        private readonly struct GetEnumeratorInvocationAnalysisValue : IAbstractAnalysisValue
        {
            public AnalysisEntity EnumeratorSource { get; }

            public CaptureId FlowCaptureId { get; }

            public bool Equals(IAbstractAnalysisValue other)
            {
                if (other is GetEnumeratorInvocationAnalysisValue otherValue)
                {
                    return EnumeratorSource.Equals(otherValue.EnumeratorSource) && FlowCaptureId.Equals(otherValue.FlowCaptureId);
                }

                return false;
            }

            public IAbstractAnalysisValue GetNegatedValue() => this;
        }
    }
}