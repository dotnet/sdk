// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        private readonly struct EnumerationInvocationAnalysisValue : IAbstractAnalysisValue
        {
            public IOperation InvocationOperation { get; }

            public EnumerationInvocationAnalysisValue(IOperation operation)
            {
                InvocationOperation = operation;
            }

            public bool Equals(IAbstractAnalysisValue other)
            {
                if (other is EnumerationInvocationAnalysisValue otherValue)
                {
                    return InvocationOperation.Equals(otherValue.InvocationOperation);
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
            public bool Equals(IAbstractAnalysisValue other)
            {
                throw new NotImplementedException();
            }

            public IAbstractAnalysisValue GetNegatedValue()
            {
                throw new NotImplementedException();
            }
        }
    }
}