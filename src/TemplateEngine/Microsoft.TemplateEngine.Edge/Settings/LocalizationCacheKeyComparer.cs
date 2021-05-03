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
    internal class LocalizationCacheKeyComparer : IEqualityComparer<(string? key, string? value)>
    {
        public bool Equals((string? key, string? value) x, (string? key, string? value) y)
        {
            return string.Equals(x.key, y.key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.value, y.value, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string? key, string? value) obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.key) * 17 +
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.value);
        }
    }
}
