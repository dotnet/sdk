// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Utils
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Way of resolving keys conflicts.
        /// </summary>
        public enum ConflictingKeysResolution
        {
            /// <summary>
            /// Force overwrite values in original dictionary in case of conflict.
            /// </summary>
            Overwrite,

            /// <summary>
            /// Preserve original values in case of conflict.
            /// </summary>
            PreserveOriginal,

            /// <summary>
            /// Throw in case of conflict.
            /// </summary>
            Throw
        }

        public static IReadOnlyDictionary<string, T> CloneIfDifferentComparer<T>(this IReadOnlyDictionary<string, T> source, StringComparer comparer)
        {
            if (((Dictionary<string, T>)source).Comparer == comparer)
            {
                return source;
            }
            else
            {
                Dictionary<string, T> cloneDict = new(comparer);
                foreach (KeyValuePair<string, T> entry in source)
                {
                    cloneDict.Add(entry.Key, entry.Value);
                }

                return cloneDict;
            }
        }

        /// <summary>
        /// Adds a content of given dictionary to current dictionary.
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="dict">Dictionary to receive another values.</param>
        /// <param name="another">Dictionary to be merged into current.</param>
        /// <param name="conflictingKeysResolution">Way of resolving keys conflicts.</param>
        /// <exception cref="Exception">Thrown if key is already present in current dictionary and <see cref="ConflictingKeysResolution.Throw"/> strategy was requested.</exception>
        public static void Merge<TKey, TValue>(
            this IDictionary<TKey, TValue> dict,
            IReadOnlyDictionary<TKey, TValue> another,
            ConflictingKeysResolution conflictingKeysResolution = ConflictingKeysResolution.Overwrite)
        {
            foreach (var pair in another)
            {
                if (conflictingKeysResolution == ConflictingKeysResolution.Overwrite || !dict.ContainsKey(pair.Key))
                {
                    dict[pair.Key] = pair.Value;
                }
                else if (conflictingKeysResolution == ConflictingKeysResolution.Throw)
                {
                    throw new ArgumentException(string.Format(LocalizableStrings.DictionaryExtensions_Error_KeyExists, pair.Key));
                }
            }
        }

        /// <summary>
        /// Adds a <paramref name="value"/> to given dictionary <paramref name="dict"/> if <paramref name="value"/> satisfies <paramref name="condition"/>.
        /// </summary>
        /// <typeparam name="TKey"> Type of keys of <paramref name="dict"/>. </typeparam>
        /// <typeparam name="TValue"> Type of values of <paramref name="dict"/>.</typeparam>
        /// <param name="dict">A dictionary to add <paramref name="key"/> and <paramref name="value"/> to.</param>
        /// <param name="key">A key for new <paramref name="value"/>.</param>
        /// <param name="value">A value with <paramref name="key"/>.</param>
        /// <param name="condition">If condition is <see langword="true"/>, then <paramref name="value"/> will be added to <paramref name="dict"/>.</param>
        /// <exception cref="Exception">Thrown if key is already present in current dictionary and <see cref="ConflictingKeysResolution.Throw"/> strategy was requested.</exception>
        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value, Predicate<TValue> condition)
        {
            if (condition(value))
            {
                dict[key] = value!;
                return true;
            }

            return false;
        }
    }
}
