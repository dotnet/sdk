// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    internal class GlobalFlowStateDictionaryAnalysisValue : CacheBasedEquatable<GlobalFlowStateDictionaryAnalysisValue>
    {
        public ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet> TrackedEntities { get; }

        // The union of these contributions equals the operations in reportable TrackedEntities values.
        // Additive updates share their stack tails; removals and lost reportability rebuild them.
        public ImmutableStack<TrackingEnumerationSet> ReportableEnumerationSets { get; }

        public GlobalFlowStateDictionaryAnalysisValueKind Kind { get; }

        public static readonly GlobalFlowStateDictionaryAnalysisValue Empty = new(ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet>.Empty, GlobalFlowStateDictionaryAnalysisValueKind.Empty);

        public static readonly GlobalFlowStateDictionaryAnalysisValue Unknown = new(ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet>.Empty, GlobalFlowStateDictionaryAnalysisValueKind.Unknown);

        public GlobalFlowStateDictionaryAnalysisValue(
            ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet> trackedEntities,
            GlobalFlowStateDictionaryAnalysisValueKind kind)
        {
            TrackedEntities = trackedEntities.WithComparers(trackedEntities.KeyComparer, TrackingEnumerationSet.ValueComparer);
            Kind = kind;
            ReportableEnumerationSets = CreateReportableEnumerationSets(trackedEntities);
        }

        private GlobalFlowStateDictionaryAnalysisValue(
            ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet> trackedEntities,
            ImmutableStack<TrackingEnumerationSet> reportableEnumerationSets,
            GlobalFlowStateDictionaryAnalysisValueKind kind)
        {
            TrackedEntities = trackedEntities;
            ReportableEnumerationSets = reportableEnumerationSets;
            Kind = kind;
        }

        private static ImmutableStack<TrackingEnumerationSet> CreateReportableEnumerationSets(
            ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet> trackedEntities)
        {
            var result = ImmutableStack<TrackingEnumerationSet>.Empty;
            foreach (var (_, invocationSet) in trackedEntities)
            {
                if (invocationSet.IsMultipleEnumeration)
                {
                    result = result.Push(invocationSet);
                }
            }

            return result;
        }

        /// <summary>
        /// Merge <param name="value1"/> and <param name="value2"/>, if <param name="intersectValueInCommonKeys"/> is true,
        /// perform <see cref="InvocationSetHelpers.Intersect(TrackingEnumerationSet, TrackingEnumerationSet)"/> operation between the values
        /// of the common keys, otherwise, perform <see cref="InvocationSetHelpers.Merge(TrackingEnumerationSet, TrackingEnumerationSet)"/>
        /// </summary>
        public static GlobalFlowStateDictionaryAnalysisValue Merge(
            GlobalFlowStateDictionaryAnalysisValue value1, GlobalFlowStateDictionaryAnalysisValue value2, bool intersectValueInCommonKeys)
        {
            if (value1.TrackedEntities.Count == 0)
            {
                return value2;
            }

            if (value2.TrackedEntities.Count == 0)
            {
                return value1;
            }

            var (larger, smaller) = value1.TrackedEntities.Count >= value2.TrackedEntities.Count
                ? (value1, value2)
                : (value2, value1);
            var builder = larger.TrackedEntities.ToBuilder();
            var reportableSets = larger.ReportableEnumerationSets;
            var rebuildReportableSets = false;
            foreach (var (key, invocationSet) in smaller.TrackedEntities)
            {
                TrackingEnumerationSet mergedSet;
                if (builder.TryGetValue(key, out var existing))
                {
                    mergedSet = intersectValueInCommonKeys
                        ? InvocationSetHelpers.Intersect(existing, invocationSet)
                        : InvocationSetHelpers.Merge(existing, invocationSet);
                }
                else
                {
                    mergedSet = invocationSet;
                }

                builder[key] = mergedSet;
                if (mergedSet.IsMultipleEnumeration)
                {
                    if (existing == null || !existing.IsMultipleEnumeration ||
                        !ReferenceEquals(existing.Operations, mergedSet.Operations))
                    {
                        reportableSets = reportableSets.Push(mergedSet);
                    }
                }
                else
                {
                    rebuildReportableSets |= existing?.IsMultipleEnumeration == true;
                }
            }

            var trackedEntities = builder.ToImmutable();
            return new GlobalFlowStateDictionaryAnalysisValue(
                trackedEntities,
                rebuildReportableSets ? CreateReportableEnumerationSets(trackedEntities) : reportableSets,
                GlobalFlowStateDictionaryAnalysisValueKind.Known);
        }

        /// <summary>
        /// Intersect the state of the analysisEntity in <param name="value1"/> and <param name="value2"/>.
        /// </summary>
        public static GlobalFlowStateDictionaryAnalysisValue Intersect(GlobalFlowStateDictionaryAnalysisValue value1, GlobalFlowStateDictionaryAnalysisValue value2)
        {
            var builder = ImmutableDictionary.CreateBuilder<IDeferredTypeEntity, TrackingEnumerationSet>();
            var intersectedKeys = new HashSet<IDeferredTypeEntity>();

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
                    builder[key] = InvocationSetHelpers.Intersect(invocationSet, TrackingEnumerationSet.Empty);
                }
            }

            foreach (var kvp in value2.TrackedEntities)
            {
                var key = kvp.Key;
                var invocationSet = kvp.Value;

                if (!intersectedKeys.Contains(kvp.Key))
                {
                    builder[key] = InvocationSetHelpers.Intersect(invocationSet, TrackingEnumerationSet.Empty);
                }
            }

            return new GlobalFlowStateDictionaryAnalysisValue(builder.ToImmutable(), GlobalFlowStateDictionaryAnalysisValueKind.Known);
        }

        public GlobalFlowStateDictionaryAnalysisValue RemoveTrackedDeferredTypeEntity(IDeferredTypeEntity entity)
        {
            if (this.TrackedEntities.TryGetValue(entity, out var removedSet))
            {
                if (this.TrackedEntities.Count == 1)
                {
                    return Empty;
                }

                var trackedEntities = TrackedEntities.Remove(entity);
                var reportableSets = removedSet.IsMultipleEnumeration
                    ? CreateReportableEnumerationSets(trackedEntities)
                    : ReportableEnumerationSets;
                return new GlobalFlowStateDictionaryAnalysisValue(
                    trackedEntities, reportableSets, GlobalFlowStateDictionaryAnalysisValueKind.Known);
            }

            return this;
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
