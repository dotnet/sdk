// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public sealed partial class AvoidMultipleEnumerations
    {
        private readonly struct IEnumerableInvocationInfo
        {
            public IOperation OperationReferencingIEnumerableLocalOrParameter { get; }
            public ISymbol IEnumerableSymbol { get; }

            public IEnumerableInvocationInfo(IOperation operationReferencingIEnumerableLocalOrParameter, ISymbol enumerableSymbol)
            {
                OperationReferencingIEnumerableLocalOrParameter = operationReferencingIEnumerableLocalOrParameter;
                IEnumerableSymbol = enumerableSymbol;
            }
        }
    }
}