// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations;

namespace Microsoft.CodeAnalysis.CSharp.NetAnalyzers.Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    internal partial class CSharpAvoidMultipleEnumerationsAnalyzer
    {
        private class CSharpAvoidMultipleEnumerationsHelper : AvoidMultipleEnumerationsHelper
        {
            public static readonly CSharpAvoidMultipleEnumerationsHelper Instance = new();

            public override bool IsInvocationDeferredExecutingInvocationInstance(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
                => false;

            protected override bool IsInvocationCausingEnumerationOverInvocationInstance(IInvocationOperation invocationOperation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
                => false;

            protected override bool IsOperationTheInstanceOfDeferredInvocation(IOperation operation, WellKnownSymbolsInfo wellKnownSymbolsInfo)
                => false;
        }
    }
}