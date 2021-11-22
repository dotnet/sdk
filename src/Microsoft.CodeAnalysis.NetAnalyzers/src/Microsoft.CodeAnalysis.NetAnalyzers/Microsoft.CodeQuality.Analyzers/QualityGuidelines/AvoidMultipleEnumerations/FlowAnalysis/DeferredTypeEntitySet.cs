// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// A set represents a defer entity that the value might come from many sources.
    /// </summary>
    internal class DeferredTypeEntitySet : CacheBasedEquatable<DeferredTypeEntitySet>, IDeferredTypeEntity
    {
        public ImmutableHashSet<DeferredTypeEntity> DeferredTypeEntities { get; }

        private DeferredTypeEntitySet(ImmutableHashSet<DeferredTypeEntity> deferredTypeEntities)
        {
            DeferredTypeEntities = deferredTypeEntities;
        }

        public static DeferredTypeEntitySet Create(ImmutableHashSet<AbstractLocation> locations)
        {
            using var builder = PooledHashSet<DeferredTypeEntity>.GetInstance();
            foreach (var location in locations)
            {
                builder.Add(new DeferredTypeEntity(location.Symbol, location.Creation));
            }

            return new DeferredTypeEntitySet(builder.ToImmutable());
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(HashUtilities.Combine(DeferredTypeEntities));
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredTypeEntitySet> obj)
        {
            var other = (DeferredTypeEntitySet)obj;
            return HashUtilities.Combine(other.DeferredTypeEntities) == HashUtilities.Combine(DeferredTypeEntities);
        }
    }
}
