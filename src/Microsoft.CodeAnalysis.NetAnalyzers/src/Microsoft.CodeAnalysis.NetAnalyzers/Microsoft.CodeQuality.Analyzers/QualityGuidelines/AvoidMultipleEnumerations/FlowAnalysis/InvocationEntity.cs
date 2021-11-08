// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Reflection.Metadata;
using Analyzer.Utilities;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Runtime;
using static Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.AvoidMultipleEnumerationsHelpers;

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
        private ImmutableHashSet<DeferredEntity> Entities { get; }

        private InvocationEntity(ImmutableHashSet<DeferredEntity> entities) => Entities = entities;

        public static ImmutableHashSet<InvocationEntity> Create(
            ImmutableHashSet<AbstractLocation> locations,
            PointsToAnalysisResult pointsToAnalysisResult,
            AvoidMultipleEnumerations.WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            using var builder = PooledHashSet<InvocationEntity>.GetInstance();
            foreach (var location in locations)
            {
                if (location.Creation is IInvocationOperation { Arguments: { Length: > 0} } invocationOperation)
                {


                }
            }

            return builder.ToImmutable();
        }

        private static ImmutableArray<DeferredEntity> Collect(
            IInvocationOperation invocationOperation,
            PointsToAnalysisResult pointsToAnalysisResult,
            AvoidMultipleEnumerations.WellKnownSymbolsInfo wellKnownSymbolsInfo)
        {
            var builder = ArrayBuilder<DeferredEntity>.GetInstance();
            var queue = new Queue<IInvocationOperation>();
            queue.Enqueue(invocationOperation);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var argument in current.Arguments)
                {
                    if (IsDeferredExecutingInvocation(invocationOperation, argument, wellKnownSymbolsInfo))
                    {
                        var result = pointsToAnalysisResult[argument.Value]

                    }
                }
            }
        }

        protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
        {
            hashCode.Add(HashUtilities.Combine(Entities));
        }

        protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<InvocationEntity> obj)
        {
            return HashUtilities.Combine(((InvocationEntity)obj).Entities) == HashUtilities.Combine(Entities);
        }

        private class DeferredEntity : CacheBasedEquatable<DeferredEntity>
        {
            public ISymbol? Symbol { get; }

            public IOperation? CreationOperation { get; }

            public DeferredEntity(ISymbol? symbol, IOperation? creationOperation)
            {
                Symbol = symbol;
                CreationOperation = creationOperation;
            }

            protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
            {
                hashCode.Add(Symbol.GetHashCodeOrDefault());
                hashCode.Add(CreationOperation.GetHashCodeOrDefault());
            }

            protected override bool ComputeEqualsByHashCodeParts(CacheBasedEquatable<DeferredEntity> obj)
            {
                var other = (DeferredEntity)obj;
                return other.Symbol.GetHashCodeOrDefault() == Symbol.GetHashCodeOrDefault()
                       && other.CreationOperation.GetHashCodeOrDefault() == CreationOperation.GetHashCodeOrDefault();
            }
        }
    }
}