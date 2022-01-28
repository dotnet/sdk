// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
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
        public ImmutableArray<IMethodSymbol> LinqChainMethods { get; }

        /// <summary>
        /// Methods don't enumerated the IEnumerable types.
        /// e.g. TryGetNonEnumeratedCount
        /// </summary>
        public ImmutableArray<IMethodSymbol> NoEnumerationMethods { get; }

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

        /// <summary>
        /// User specified methods that would not enumerated its parameters. The value comes from editorConfig.
        /// </summary>
        public SymbolNamesWithValueOption<Unit>? CustomizedNoEnumerationMethods { get; }

        /// <summary>
        /// User specified methods that would not enumerated its parameters and the return type is deferred type. The value comes from editorConfig.
        /// </summary>
        public SymbolNamesWithValueOption<Unit>? CustomizedLinqChainMethods { get; }

        public WellKnownSymbolsInfo(
            ImmutableArray<IMethodSymbol> linqChainMethods,
            ImmutableArray<IMethodSymbol> noEnumerationMethods,
            ImmutableArray<IMethodSymbol> enumeratedMethods,
            ImmutableArray<IMethodSymbol> noEffectLinqChainMethods,
            ImmutableArray<ITypeSymbol> additionalDeferredTypes,
            ImmutableArray<IMethodSymbol> getEnumeratorMethods,
            SymbolNamesWithValueOption<Unit>? customizedNoEnumerationMethods,
            SymbolNamesWithValueOption<Unit>? customizedLinqChainMethods)
        {
            LinqChainMethods = linqChainMethods;
            NoEnumerationMethods = noEnumerationMethods;
            EnumeratedMethods = enumeratedMethods;
            NoEffectLinqChainMethods = noEffectLinqChainMethods;
            AdditionalDeferredTypes = additionalDeferredTypes;
            GetEnumeratorMethods = getEnumeratorMethods;
            CustomizedNoEnumerationMethods = customizedNoEnumerationMethods;
            CustomizedLinqChainMethods = customizedLinqChainMethods;
        }
    }
}