// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    public abstract partial class AvoidMultipleEnumerations
    {
        /// <summary>
        /// A helper that contains all methods and types symbols could be involved in the enumeration of IEnumerable.
        /// </summary>
        internal readonly struct WellKnownSymbolsInfo
        {
            /// <summary>
            /// Deferred methods that take one deferred type parameter.
            /// e.g. Select, Where
            /// </summary>
            public ImmutableArray<IMethodSymbol> OneParameterDeferredMethods { get; }

            /// <summary>
            /// Deferred methods that take two deferred type parameters.
            /// e.g. Concat, Except
            /// </summary>
            public ImmutableArray<IMethodSymbol> TwoParametersDeferredMethods { get; }

            /// <summary>
            /// Enumeration methods that take one deferred type parameter.
            /// e.g. ToArray, Count
            /// </summary>
            public ImmutableArray<IMethodSymbol> OneParameterEnumeratedMethods { get; }

            /// <summary>
            /// Enumeration methods that take two deferred type parameter.
            /// e.g. SequentialEqual
            /// </summary>
            public ImmutableArray<IMethodSymbol> TwoParametersEnumeratedMethods { get; }

            /// <summary>
            /// Other deferred types except IEnumerable and IEnumerable`1.
            /// e.g.
            /// IOrderedEnumerable
            /// </summary>
            public ImmutableArray<ITypeSymbol> AdditionalDeferredTypes { get; }

            /// <summary>
            /// IEnumerable.GetEnumerator() and IEnumerable'1.GetEnumerator()
            /// </summary>
            public ImmutableArray<IMethodSymbol> GetEnumeratorMethods { get; }

            public WellKnownSymbolsInfo(
                ImmutableArray<IMethodSymbol> oneParameterDeferredMethods,
                ImmutableArray<IMethodSymbol> twoParametersDeferredMethods,
                ImmutableArray<IMethodSymbol> oneParameterEnumeratedMethods,
                ImmutableArray<IMethodSymbol> twoParametersEnumeratedMethods,
                ImmutableArray<ITypeSymbol> additionalDeferredTypes,
                ImmutableArray<IMethodSymbol> getEnumeratorMethods)
            {
                OneParameterDeferredMethods = oneParameterDeferredMethods;
                TwoParametersDeferredMethods = twoParametersDeferredMethods;
                OneParameterEnumeratedMethods = oneParameterEnumeratedMethods;
                TwoParametersEnumeratedMethods = twoParametersEnumeratedMethods;
                AdditionalDeferredTypes = additionalDeferredTypes;
                GetEnumeratorMethods = getEnumeratorMethods;
            }
        }
    }
}