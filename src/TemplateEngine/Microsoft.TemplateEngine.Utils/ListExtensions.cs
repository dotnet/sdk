// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Utils
{
    public static class ListExtensions
    {
        public static IEnumerable<IGrouping<TKey, TElement>> GroupBy<TElement, TKey>(this IEnumerable<TElement> elements, Func<TElement, TKey> grouper, Func<TElement, bool> hasGroupKey, IEqualityComparer<TKey> comparer = null)
            where TKey : IEquatable<TKey>
        {
            if (comparer == null)
            {
                comparer = EqualityComparer<TKey>.Default;
            }
            Dictionary<ValueWrapper<TKey>, List<TElement>> groups = new Dictionary<ValueWrapper<TKey>, List<TElement>>(new ValueWrapperComparer<TKey>(comparer));
            List<TElement> ungrouped = new List<TElement>();

            foreach (TElement element in elements)
            {
                if (hasGroupKey(element))
                {
                    ValueWrapper<TKey> x = new ValueWrapper<TKey>(grouper(element));
                    if (!groups.TryGetValue(x, out List<TElement> group))
                    {
                        groups[x] = group = new List<TElement>();
                    }

                    group.Add(element);
                }
                else
                {
                    ungrouped.Add(element);
                }
            }

            List<IGrouping<TKey, TElement>> allGrouped = new List<IGrouping<TKey, TElement>>();

            foreach (KeyValuePair<ValueWrapper<TKey>, List<TElement>> entry in groups)
            {
                allGrouped.Add(new Grouping<TKey, TElement>(entry.Key.Val, entry.Value));
            }

            foreach (TElement entry in ungrouped)
            {
                allGrouped.Add(new Grouping<TKey, TElement>(default(TKey), new[] { entry }));
            }

            return allGrouped;
        }

        private struct ValueWrapper<T>
        {
            public ValueWrapper(T val)
            {
                Val = val;
            }

            public T Val { get; private set; }

            public override bool Equals(object obj)
            {
                return obj is ValueWrapper<T> v && Equals(Val, v.Val);
            }

            public override int GetHashCode()
            {
                return Val?.GetHashCode() ?? 0;
            }
        }

        private class ValueWrapperComparer<T> : IEqualityComparer<ValueWrapper<T>>
        {
            private IEqualityComparer<T> _comparer;

            public ValueWrapperComparer(IEqualityComparer<T> comparer)
            {
                _comparer = comparer;
            }

            public bool Equals(ValueWrapper<T> x, ValueWrapper<T> y)
            {
                if (x.Val == null && y.Val == null)
                {
                    return true;
                }
                if (x.Val == null || y.Val == null)
                {
                    return false;
                }
                return _comparer.Equals(x.Val, y.Val);
            }

            public int GetHashCode(ValueWrapper<T> obj)
            {
                if (obj.Val == null)
                {
                    return 0;
                }
                return _comparer.GetHashCode(obj.Val);
            }
        }

        private class Grouping<TKey, TElement> : IGrouping<TKey, TElement>
        {
            private IEnumerable<TElement> _element;

            public Grouping(TKey key, IEnumerable<TElement> element)
            {
                Key = key;
                _element = element;
            }

            public TKey Key { get; }

            public IEnumerator<TElement> GetEnumerator()
            {
                return _element.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
