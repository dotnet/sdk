// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    internal class TrackingEnumerationSet : CacheBasedEquatable<TrackingEnumerationSet>
    {
        public static IEqualityComparer<TrackingEnumerationSet> ValueComparer { get; } = new EnumerationSetComparer();

        private sealed class EnumerationSetComparer : IEqualityComparer<TrackingEnumerationSet>
        {
            public bool Equals(TrackingEnumerationSet? first, TrackingEnumerationSet? second)
            {
                if (ReferenceEquals(first, second))
                {
                    return true;
                }

                // A new site or count already proves inequality; avoid hashing the growing site set.
                return first != null && second != null &&
                    first.EnumerationCount == second.EnumerationCount &&
                    first.Operations.Count == second.Operations.Count &&
                    first.Equals(second);
            }

            public int GetHashCode(TrackingEnumerationSet value) => value.GetHashCode();
        }

        /// <summary>
        /// All the operations that might contribute to the <see cref="EnumerationCount"/>
        /// </summary>
        /// <remarks>
        /// In some cases, EnumerationCount would be Zero, but this property is not empty.
        /// e.g.
        /// if (b)
        /// {
        ///     Bar.First();
        /// }
        /// else if (a)
        /// {
        /// }
        /// Here the block after the two if-branches would have a Zero EnumerationCount (because 'Bar' is not enumerated on all paths),
        /// with Bar.First() recorded in this property
        /// </remarks>
        public ImmutableHashSet<IOperation> Operations { get; }

        /// <summary>
        /// The total number of enumeration.
        /// </summary>
        public EnumerationCount EnumerationCount { get; }

        // Distinct sites are reported even when they occur on mutually exclusive paths.
        public bool IsMultipleEnumeration
            => EnumerationCount == EnumerationCount.TwoOrMoreTime || Operations.Count > 1;

        public TrackingEnumerationSet? MergedSet1 { get; }
        public TrackingEnumerationSet? MergedSet2 { get; }

        public static readonly TrackingEnumerationSet Empty = new(ImmutableHashSet<IOperation>.Empty, EnumerationCount.Zero);

        public TrackingEnumerationSet(ImmutableHashSet<IOperation> operations, EnumerationCount enumerationCount)
        {
            Operations = operations;
            EnumerationCount = enumerationCount;
        }

        public TrackingEnumerationSet(
            ImmutableHashSet<IOperation> operations,
            EnumerationCount enumerationCount,
            TrackingEnumerationSet mergedSet1,
            TrackingEnumerationSet mergedSet2) : this(operations, enumerationCount)
        {
            MergedSet1 = mergedSet1;
            MergedSet2 = mergedSet2;
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(EnumerationCount.GetHashCode());
            hashCode.Add(HashUtilities.Combine(Operations));
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<TrackingEnumerationSet> obj)
        {
            var other = (TrackingEnumerationSet)obj;
            return other.EnumerationCount.GetHashCode() == EnumerationCount.GetHashCode()
                && HashUtilities.Combine(other.Operations) == HashUtilities.Combine(Operations);
        }
    }
}
