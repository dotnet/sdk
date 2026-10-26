// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations
{
    /// <summary>
    /// A helper that contains all methods and types symbols could be involved in the enumeration of IEnumerable.
    /// </summary>
#pragma warning disable CA1815 // Override equals and operator equals on value types
    internal readonly struct WellKnownSymbolsInfo
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        /// <summary>
        /// Methods defer enumerating IEnumerable types.
        /// e.g. Select, Where
        /// </summary>
        public ImmutableArray<IMethodSymbol> LinqChainMethods { get; }

        /// <summary>
        /// Methods that do not enumerate the IEnumerable types.
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
        /// User specified methods that would enumerate its parameters. This value comes from options.
        /// </summary>
        public SymbolNamesWithValueOption<Unit> CustomizedEumerationMethods { get; }

        /// <summary>
        /// User specified methods that accept a deferred type parameter and return a new deferred type value. This value comes from options.
        /// </summary>
        public SymbolNamesWithValueOption<Unit> CustomizedLinqChainMethods { get; }

        /// <summary>
        /// User specified value that should assume method enumerates take IEnumerable type parameters or not.
        /// </summary>
        public bool AssumeMethodEnumeratesParameters { get; }

        public WellKnownSymbolsInfo(
            ImmutableArray<IMethodSymbol> linqChainMethods,
            ImmutableArray<IMethodSymbol> noEnumerationMethods,
            ImmutableArray<IMethodSymbol> enumeratedMethods,
            ImmutableArray<IMethodSymbol> noEffectLinqChainMethods,
            ImmutableArray<ITypeSymbol> additionalDeferredTypes,
            ImmutableArray<IMethodSymbol> getEnumeratorMethods,
            SymbolNamesWithValueOption<Unit> customizedEumerationMethods,
            SymbolNamesWithValueOption<Unit> customizedLinqChainMethods,
            bool assumeMethodEnumeratesParameters)
        {
            LinqChainMethods = linqChainMethods;
            NoEnumerationMethods = noEnumerationMethods;
            EnumeratedMethods = enumeratedMethods;
            NoEffectLinqChainMethods = noEffectLinqChainMethods;
            AdditionalDeferredTypes = additionalDeferredTypes;
            GetEnumeratorMethods = getEnumeratorMethods;
            CustomizedEumerationMethods = customizedEumerationMethods;
            CustomizedLinqChainMethods = customizedLinqChainMethods;
            AssumeMethodEnumeratesParameters = assumeMethodEnumeratesParameters;
        }

        public bool IsCustomizedEnumerationMethods(IMethodSymbol methodSymbol)
            => CustomizedEumerationMethods.Contains(methodSymbol);

        public bool IsCustomizedLinqChainMethods(IMethodSymbol methodSymbol)
            => CustomizedLinqChainMethods.Contains(methodSymbol);
    }
}