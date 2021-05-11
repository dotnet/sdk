// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Utils
{
    public static class EqualityExtensions
    {
        public static bool AllAreTheSame<T, TValue>(this IEnumerable<T> items, Func<T, TValue> selector)
            where TValue : IEquatable<TValue>
        {
            return items.AllAreTheSame(selector, (x, y) => x?.Equals(y) ?? (y == null));
        }

        public static bool AllAreTheSame<T, TValue>(this IEnumerable<T> items, Func<T, TValue> selector, IEqualityComparer<TValue> comparer)
            where TValue : IEquatable<TValue>
        {
            if (comparer == null)
            {
                comparer = EqualityComparer<TValue>.Default;
            }

            return items.AllAreTheSame(selector, comparer.Equals);
        }

        public static bool AllAreTheSame<T, TValue>(this IEnumerable<T> items, Func<T, TValue> selector, Func<TValue, TValue, bool> comparer)
            where TValue : IEquatable<TValue>
        {
            if (comparer == null)
            {
                comparer = EqualityComparer<TValue>.Default.Equals;
            }

            using (IEnumerator<T> enumerator = items.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return true; //If there are no elements they're all the same
                }

                TValue firstValue = selector(enumerator.Current);

                while (enumerator.MoveNext())
                {
                    TValue currentValue = selector(enumerator.Current);

                    if (!comparer(firstValue, currentValue))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
