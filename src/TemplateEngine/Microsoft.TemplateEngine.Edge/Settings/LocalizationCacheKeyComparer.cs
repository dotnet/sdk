// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Edge.Settings
{
    /// <summary>
    /// Makes an ordinal, case insensitive comparison with the two strings of the given tuples.
    /// </summary>
    internal class LocalizationCacheKeyComparer : IEqualityComparer<(string? Key, string? Value)>
    {
        public bool Equals((string? Key, string? Value) x, (string? Key, string? Value) y)
        {
            return string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string? Key, string? Value) obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key) * 17 +
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value);
        }
    }
}
