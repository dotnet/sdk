// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    /// <summary>
    /// A helper that contains all methods and types symbols could be involved in the enumeration of IEnumerable.
    /// </summary>
    internal readonly struct WellKnownSymbolsInfo
    {
        /// <summary>
        /// Methods defer enumerating IEnumerable types.
        /// e.g. Select, Where
        /// </summary>
        public ImmutableArray<IMethodSymbol> DeferredMethods { get; }

        /// <summary>
        /// Methods enumerate IEnumerable types.
        /// e.g. ToArray, Count
        /// </summary>
        public ImmutableArray<IMethodSymbol> EnumeratedMethods { get; }

        /// <summary>
        /// Methods that don't create new IEnumerable types, but can be used in linq chain.
        /// e.g. AsEnumerable
        /// </summary>
        public ImmutableArray<IMethodSymbol> NoEffectLinqChainMethods { get; }

        /// <summary>
        /// Other deferred types except IEnumerable and IEnumerable`1.
        /// e.g.
        /// IOrderedEnumerable
        /// </summary>
        public ImmutableArray<ITypeSymbol> AdditionalDeferredTypes { get; }

        /// <summary>
        /// IEnumerable.GetEnumerator() and IEnumerable'1.GetEnumerator().
        /// </summary>
        public ImmutableArray<IMethodSymbol> GetEnumeratorMethods { get; }

        public WellKnownSymbolsInfo(
            ImmutableArray<IMethodSymbol> deferredMethods,
            ImmutableArray<IMethodSymbol> enumeratedMethods,
            ImmutableArray<IMethodSymbol> noEffectLinqChainMethods,
            ImmutableArray<ITypeSymbol> additionalDeferredTypes,
            ImmutableArray<IMethodSymbol> getEnumeratorMethods)
        {
            DeferredMethods = deferredMethods;
            EnumeratedMethods = enumeratedMethods;
            NoEffectLinqChainMethods = noEffectLinqChainMethods;
            AdditionalDeferredTypes = additionalDeferredTypes;
            GetEnumeratorMethods = getEnumeratorMethods;
        }
    }
}