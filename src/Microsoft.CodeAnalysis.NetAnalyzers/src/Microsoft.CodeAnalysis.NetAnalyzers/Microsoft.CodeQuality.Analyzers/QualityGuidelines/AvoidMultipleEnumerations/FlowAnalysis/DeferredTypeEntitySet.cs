// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// Represents a symbol with the deferred type that might come from many sources.
    /// </summary>
    internal class DeferredTypeEntitySet : CacheBasedEquatable<DeferredTypeEntitySet>, IDeferredTypeEntity
    {
        public ISymbol Symbol { get; }

        public ImmutableHashSet<AbstractLocation> DeferredTypeLocations { get; }

        public DeferredTypeEntitySet(ISymbol symbol, ImmutableHashSet<AbstractLocation> deferredTypeEntities)
        {
            Symbol = symbol;
            DeferredTypeLocations = deferredTypeEntities;
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(Symbol.GetHashCode());
            hashCode.Add(HashUtilities.Combine(DeferredTypeLocations));
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeEntitySet> obj)
        {
            var other = (DeferredTypeEntitySet)obj;
            return other.Symbol.GetHashCode() == Symbol.GetHashCode()
                && HashUtilities.Combine(other.DeferredTypeLocations) == HashUtilities.Combine(DeferredTypeLocations);
        }
    }
}
