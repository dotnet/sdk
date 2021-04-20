// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Utils
{
    public static class DictionaryExtensions
    {
        public static IReadOnlyDictionary<string, T> CloneIfDifferentComparer<T>(this IReadOnlyDictionary<string, T> source, StringComparer comparer)
        {
            if (((Dictionary<string, T>)(source)).Comparer == comparer)
            {
                return source;
            }
            else
            {
                Dictionary<string, T> cloneDict = new Dictionary<string, T>(comparer);
                foreach (KeyValuePair<string, T> entry in source)
                {
                    cloneDict.Add(entry.Key, entry.Value);
                }

                return cloneDict;
            }
        }
    }
}
