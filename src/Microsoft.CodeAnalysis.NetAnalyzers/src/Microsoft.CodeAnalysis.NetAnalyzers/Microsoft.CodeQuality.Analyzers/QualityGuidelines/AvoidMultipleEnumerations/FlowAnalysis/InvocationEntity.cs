// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// The entity that could be enumerated by for each loop or method.
    /// </summary>
    internal class InvocationEntity : CacheBasedEquatable<InvocationEntity>
    {
        /// <summary>
        /// A set of the possible locations of a local or parameter symbol.
        /// TODO: We should support skip the deferred method call
        /// e.g. var d = Enumerable.Range(1, 10)
        ///      d.ToArray();
        ///      var e = d.Select(i => i)
        ///      e.ToArray();
        /// For e, Locations will point to d.Select(i => i), but ideally it should point to the local 'd'
        /// </summary>
        private ImmutableHashSet<AbstractLocation> Locations { get; }

        public InvocationEntity(ImmutableHashSet<AbstractLocation> locations)
        {
            Locations = locations;
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(HashUtilities.Combine(Locations));
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<InvocationEntity> obj)
        {
            return HashUtilities.Combine(((InvocationEntity)obj).Locations) == HashUtilities.Combine(Locations);
        }
    }
}