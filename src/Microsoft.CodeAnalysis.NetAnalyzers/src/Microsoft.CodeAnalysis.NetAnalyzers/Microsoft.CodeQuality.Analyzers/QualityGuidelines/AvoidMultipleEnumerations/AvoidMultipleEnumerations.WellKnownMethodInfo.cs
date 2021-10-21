// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public abstract partial class AvoidMultipleEnumerations
    {
        public readonly struct WellKnownSymbolsInfo
        {
            public ImmutableArray<IMethodSymbol> OneParameterDeferredMethods { get; }
            public ImmutableArray<IMethodSymbol> TwoParametersDeferredMethods { get; }
            public ImmutableArray<IMethodSymbol> OneParameterEnumeratedMethods { get; }
            public ImmutableArray<IMethodSymbol> TwoParametersEnumeratedMethods { get; }
            public ImmutableArray<ITypeSymbol> AdditionalDeferredTypes { get; }

            public WellKnownSymbolsInfo(
                ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
                ImmutableArray<IMethodSymbol> twoParametersDeferredMethods,
                ImmutableArray<IMethodSymbol> oneParameterEnumeratedMethods,
                ImmutableArray<IMethodSymbol> twoParametersEnumeratedMethods,
                ImmutableArray<ITypeSymbol> additionalDeferredTypes)
            {
                OneParameterDeferredMethods = oneParameterDeferredMethods;
                TwoParametersDeferredMethods = twoParametersDeferredMethods;
                OneParameterEnumeratedMethods = oneParameterEnumeratedMethods;
                TwoParametersEnumeratedMethods = twoParametersEnumeratedMethods;
                AdditionalDeferredTypes = additionalDeferredTypes;
            }
        }
    }
}