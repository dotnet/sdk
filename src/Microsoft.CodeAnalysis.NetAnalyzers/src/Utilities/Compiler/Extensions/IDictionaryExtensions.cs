// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Analyzer.Utilities.Extensions
{
    internal static class IDictionaryExtensions
    {
        [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Currently used only with ImmutableDictionary<K, V>.Builder nested type.")]
        public static void AddKeyValueIfNotNull<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey? key,
            TValue? value)
            where TKey : class
            where TValue : class
        {
            if (key != null && value != null)
            {
                dictionary.Add(key, value);
            }
        }

        public static void AddRange<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            IEnumerable<KeyValuePair<TKey, TValue>> items)
            where TKey : notnull
        {
            foreach (var item in items)
            {
                dictionary.Add(item);
            }
        }

        public static bool IsEqualTo<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, IReadOnlyDictionary<TKey, TValue> other)
            where TKey : notnull
            => dictionary.Count == other.Count &&
                dictionary.Keys.All(key => other.ContainsKey(key) && dictionary[key]?.Equals(other[key]) == true);
    }
}
