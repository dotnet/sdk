// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public partial class AvoidMultipleEnumerations
    {
        private class InvocationCountAbstractValue : IAbstractAnalysisValue
        {
            public IOperation InvocationOperation { get; }

            public InvocationCountAbstractValue(IOperation operation)
            {
                InvocationOperation = operation;
            }

            public bool Equals(IAbstractAnalysisValue other)
            {
                if (other is InvocationCountAbstractValue otherValue)
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
    }
}