// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class StaticWebAssetTokenResolver(IReadOnlyDictionary<string, string> tokens)
{
    public static readonly StaticWebAssetTokenResolver Instance = new();

    private static readonly Dictionary<string, string> _empty = [];

    public StaticWebAssetTokenResolver() : this(_empty) { }

    internal virtual bool TryGetValue(StaticWebAsset asset, string key, out string value)
    {
        if (string.Equals(key, nameof(StaticWebAsset.Fingerprint), StringComparison.OrdinalIgnoreCase))
        {
            value = asset.Fingerprint;
            return true;
        }

        if (tokens == null)
        {
            value = null;
            return false;
        }

        return tokens.TryGetValue(key, out value);
    }
}
