// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// Represents a symbol with the deferred type that might come from many sources.
    /// </summary>
    internal class DeferredTypeEntitySet : CacheBasedEquatable<DeferredTypeEntitySet>, IDeferredTypeEntity
    {
        public ImmutableHashSet<AbstractLocation> DeferredTypeLocations { get; }

        public DeferredTypeEntitySet(ImmutableHashSet<AbstractLocation> deferredTypeEntities)
            => DeferredTypeLocations = deferredTypeEntities;

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
            => hashCode.Add(HashUtilities.Combine(DeferredTypeLocations));

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeEntitySet> obj)
        {
            var other = (DeferredTypeEntitySet)obj;
            return HashUtilities.Combine(other.DeferredTypeLocations) == HashUtilities.Combine(DeferredTypeLocations);
        }
    }
}
