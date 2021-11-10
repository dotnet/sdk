// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    internal class GlobalFlowStateDictionaryAnalysisValue : CacheBasedEquatable<GlobalFlowStateDictionaryAnalysisValue>
    {
        public ImmutableDictionary<EnumerationEntity, TrackingInvocationSet> TrackedEntities { get; }

        public GlobalFlowStateDictionaryAnalysisValueKind Kind { get; }

        public static readonly GlobalFlowStateDictionaryAnalysisValue Empty = new(ImmutableDictionary<EnumerationEntity, TrackingInvocationSet>.Empty, GlobalFlowStateDictionaryAnalysisValueKind.Empty);

        public static readonly GlobalFlowStateDictionaryAnalysisValue Unknown = new(ImmutableDictionary<EnumerationEntity, TrackingInvocationSet>.Empty, GlobalFlowStateDictionaryAnalysisValueKind.Unknown);

        public GlobalFlowStateDictionaryAnalysisValue(
            ImmutableDictionary<EnumerationEntity, TrackingInvocationSet> trackedEntities,
            GlobalFlowStateDictionaryAnalysisValueKind kind)
        {
            TrackedEntities = trackedEntities;
            Kind = kind;
        }

        /// <summary>
        /// Merge <param name="value1"/> and <param name="value2"/>
        /// </summary>
        public static GlobalFlowStateDictionaryAnalysisValue Merge(GlobalFlowStateDictionaryAnalysisValue value1, GlobalFlowStateDictionaryAnalysisValue value2)
        {
            if (value1.TrackedEntities.Count == 0)
            {
                return value2;
            }

            if (value2.TrackedEntities.Count == 0)
            {
                return value1;
            }

            using var builder = PooledDictionary<EnumerationEntity, TrackingInvocationSet>.GetInstance();
            foreach (var kvp in value2.TrackedEntities)
            {
                builder[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in value1.TrackedEntities)
            {
                var key = kvp.Key;
                var trackedEntities1 = kvp.Value;
                if (value2.TrackedEntities.TryGetValue(key, out var trackedEntities2))
                {
                    builder[key] = InvocationSetHelpers.Merge(trackedEntities1, trackedEntities2);
                }
                else
                {
                    builder[key] = kvp.Value;
                }
            }

            return new GlobalFlowStateDictionaryAnalysisValue(builder.ToImmutableDictionary(), GlobalFlowStateDictionaryAnalysisValueKind.Known);
        }

        /// <summary>
        /// Intersect the state of the analysisEntity in <param name="value1"/> and <param name="value2"/>.
        /// </summary>
        public static GlobalFlowStateDictionaryAnalysisValue Intersect(GlobalFlowStateDictionaryAnalysisValue value1, GlobalFlowStateDictionaryAnalysisValue value2)
        {
            var builder = ImmutableDictionary.CreateBuilder<EnumerationEntity, TrackingInvocationSet>();
            var intersectedKeys = new HashSet<EnumerationEntity>();

            foreach (var kvp in value1.TrackedEntities)
            {
                var key = kvp.Key;
                var invocationSet = kvp.Value;
                if (value2.TrackedEntities.TryGetValue(key, out var trackedEntities2))
                {
                    builder[key] = InvocationSetHelpers.Intersect(invocationSet, trackedEntities2);
                    intersectedKeys.Add(key);
                }
                else
                {
                    // If there is no such entity, intersect it with an empty value
                    // e.g.
                    // if (i == 10)
                    // {
                    //     Bar.ToArray();
                    // }
                    // else
                    // {
                    // }
                    // The AnalysisValue after the if-else branch should be Zero Times.
                    builder[key] = InvocationSetHelpers.Intersect(invocationSet, TrackingInvocationSet.Empty);
                }
            }

            foreach (var kvp in value2.TrackedEntities)
            {
                var key = kvp.Key;
                var invocationSet = kvp.Value;

                if (!intersectedKeys.Contains(kvp.Key))
                {
                    builder[key] = InvocationSetHelpers.Intersect(invocationSet, TrackingInvocationSet.Empty);
                }
            }

            return new GlobalFlowStateDictionaryAnalysisValue(builder.ToImmutable(), GlobalFlowStateDictionaryAnalysisValueKind.Known);
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<GlobalFlowStateDictionaryAnalysisValue> obj)
        {
            var other = (GlobalFlowStateDictionaryAnalysisValue)obj;
            return HashUtilities.Combine(TrackedEntities) == HashUtilities.Combine(other.TrackedEntities)
                && Kind.GetHashCode() == other.Kind.GetHashCode();
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(HashUtilities.Combine(TrackedEntities));
            hashCode.Add(Kind.GetHashCode());
        }
    }
}
