// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources;

public sealed partial class SatelliteStringResourceManager
{
    /// <summary>
    ///  An immutable snapshot of the localized table chain for one culture and cache generation.
    /// </summary>
    private sealed class CultureCache
    {
        internal CultureCache(
            string cultureName,
            int generation,
            IndexedStringResourceTable[] tables)
        {
            CultureName = cultureName;
            Generation = generation;
            Tables = tables;
        }

        internal string CultureName { get; }

        internal int Generation { get; }

        internal IndexedStringResourceTable[] Tables { get; }
    }
}
