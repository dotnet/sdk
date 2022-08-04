// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Utils
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Detects whether given sequence has any duplicate elements.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sequence">Sequence to be tested for duplicates.</param>
        /// <param name="comparer">Comparer to be used to evaluate equality. Default comparer is being used if null is passed.</param>
        /// <returns>true if sequence contains duplicates, false otherwise.</returns>
        public static bool HasDuplicates<T>(this IEnumerable<T>? sequence, IEqualityComparer<T>? comparer = null)
        {
            if (sequence == null)
            {
                return false;
            }

            return sequence.GroupBy(x => x, comparer).Any(g => g.Count() > 1);
        }

        /// <summary>
        /// Detects whether given sequence has any duplicate elements and return those.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sequence">Sequence to be tested for duplicates.</param>
        /// <param name="comparer">Comparer to be used to evaluate equality. Default comparer is being used if null is passed.</param>
        /// <returns>Sequence of elements being duplicated in input. Each item is returned just once, even if located multiple times in original sequence. Empty sequence returned if input has no duplicates.</returns>
        public static IEnumerable<T> GetDuplicates<T>(this IEnumerable<T>? sequence, IEqualityComparer<T>? comparer = null)
        {
            if (sequence == null)
            {
                return Enumerable.Empty<T>();
            }

            return sequence.GroupBy(x => x, comparer)
                .Where(g => g.Count() > 1)
                .Select(y => y.Key);
        }

        /// <summary>
        /// Concatenates items of input sequence into csv string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">Sequence to be turned into csv string.</param>
        /// <param name="useSpace">Indicates whether space should be inserted between comas and following items.</param>
        /// <returns>Csv string.</returns>
        public static string ToCsvString<T>(this IEnumerable<T>? source, bool useSpace = true)
        {
            return source == null ? "<NULL>" : string.Join("," + (useSpace ? " " : string.Empty), source);
        }

        /// <summary>
        /// Enqueue elements of given sequence to a queue.
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="elements"></param>
        /// <typeparam name="T"></typeparam>
        public static void AddRange<T>(this Queue<T> queue, IEnumerable<T> elements)
        {
            foreach (T item in elements)
            {
                queue.Enqueue(item);
            }
        }

        /// <summary>
        /// Performs an action for each element in given sequence.
        /// </summary>
        /// <param name="sequence"></param>
        /// <param name="action"></param>
        /// <typeparam name="T"></typeparam>
        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (T element in sequence)
            {
                action(element);
            }
        }
    }
}
