// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis
{
    internal static class InvocationSetHelpers
    {
        public static TrackingEnumerationSet Merge(TrackingEnumerationSet set1, TrackingEnumerationSet set2)
        {
            var builder = ImmutableHashSet.CreateBuilder<IOperation>();
            var totalCount = AddInvocationCount(set1.EnumerationCount, set2.EnumerationCount);
            foreach (var operation in set1.Operations)
            {
                builder.Add(operation);
            }

            foreach (var operation in set2.Operations)
            {
                builder.Add(operation);
            }

            return new TrackingEnumerationSet(builder.ToImmutable(), totalCount);
        }

        public static TrackingEnumerationSet Intersect(TrackingEnumerationSet set1, TrackingEnumerationSet set2)
        {
            var builder = ImmutableHashSet.CreateBuilder<IOperation>();

            // Get the min of two count.
            // Example:
            // if (a)
            // {
            //    Bar.First();
            //    Bar.First();
            // }
            // else
            // {
            //    Bar.First();
            // }
            // Then 'Bar' is only guaranteed to be enumerated once after the if-else statement
            var totalCount = Min(set1.EnumerationCount, set2.EnumerationCount);
            foreach (var operation in set1.Operations)
            {
                builder.Add(operation);
            }

            foreach (var operation in set2.Operations)
            {
                builder.Add(operation);
            }

            return new TrackingEnumerationSet(builder.ToImmutable(), totalCount);
        }

        private static EnumerationCount Min(EnumerationCount count1, EnumerationCount count2)
        {
            // Unknown = -1, Zero = 0, One = 1, TwoOrMoreTime = 2
            var min = Math.Min((int)count1, (int)count2);
            return (EnumerationCount)min;
        }

        public static EnumerationCount AddInvocationCount(EnumerationCount count1, EnumerationCount count2)
            => (count1, count2) switch
            {
                (EnumerationCount.None, _) => EnumerationCount.None,
                (_, EnumerationCount.None) => EnumerationCount.None,
                (EnumerationCount.Zero, _) => count2,
                (_, EnumerationCount.Zero) => count1,
                (EnumerationCount.One, EnumerationCount.One) => EnumerationCount.TwoOrMoreTime,
                (EnumerationCount.TwoOrMoreTime, _) => EnumerationCount.TwoOrMoreTime,
                (_, EnumerationCount.TwoOrMoreTime) => EnumerationCount.TwoOrMoreTime,
                (_, _) => EnumerationCount.None,
            };
    }
}
