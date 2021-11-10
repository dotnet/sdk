// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelpers;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    /// <summary>
    /// The entity that could be enumerated by for each loop or method.
    /// </summary>
    internal partial class EnumerationEntity : CacheBasedEquatable<EnumerationEntity>
    {
        /// <summary>
        /// A set of the possible entities that would be enumerated by this entity.
        /// </summary>
        private ImmutableHashSet<DeferredTypeEntity> Entities { get; }

        private EnumerationEntity(ImmutableHashSet<DeferredTypeEntity> entities) => Entities = entities;

        public static ImmutableHashSet<EnumerationEntity> Create(
            IOperation operation,
            PointsToAnalysisResult? pointsToAnalysisResult,
            WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            if (pointsToAnalysisResult is null)
            {
                return ImmutableHashSet<EnumerationEntity>.Empty;
            }

            var result = pointsToAnalysisResult[operation.Kind, operation.Syntax];
            if (result.Kind != PointsToAbstractValueKind.KnownLocations)
            {
                return ImmutableHashSet<EnumerationEntity>.Empty;
            }

            return ImmutableHashSet<EnumerationEntity>.Empty;
        }

        //private static ImmutableArray<DeferredTypeEntity> Collect(
        //    IInvocationOperation invocationOperation,
        //    PointsToAnalysisResult pointsToAnalysisResult,
        //    WellKnownSymbolsInfo wellKnownSymbolsInfo)
        //{
        //    var builder = ArrayBuilder<DeferredTypeEntity>.GetInstance();
        //    var queue = new Queue<IInvocationOperation>();
        //    queue.Enqueue(invocationOperation);
        //    while (queue.Count > 0)
        //    {
        //        var current = queue.Dequeue();
        //        foreach (var argument in current.Arguments)
        //        {
        //            if (IsDeferredExecutingInvocation(invocationOperation, argument, wellKnownSymbolsInfo))
        //            {
        //                var result = pointsToAnalysisResult[argument.Value]

        //            }
        //        }
        //    }
        //}

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(HashUtilities.Combine(Entities));
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<EnumerationEntity> obj)
        {
            return HashUtilities.Combine(((EnumerationEntity)obj).Entities) == HashUtilities.Combine(Entities);
        }
    }
}