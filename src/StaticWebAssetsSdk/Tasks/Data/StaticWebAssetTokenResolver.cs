// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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

        // Check AssetGroups for group-based tokens.
        // AssetGroups is a semicolon-delimited list of "name=value" pairs.
        var assetGroups = asset.AssetGroups;
        if (!string.IsNullOrEmpty(assetGroups))
        {
            foreach (var entry in assetGroups.Split(';'))
            {
                var eqIndex = entry.IndexOf('=');
                if (eqIndex > 0)
                {
                    var name = entry.Substring(0, eqIndex);
                    if (string.Equals(name, key, StringComparison.Ordinal))
                    {
                        value = entry.Substring(eqIndex + 1);
                        return true;
                    }
                }
            }
        }

        if (tokens == null)
        {
            value = null;
            return false;
        }

        return tokens.TryGetValue(key, out value);
    }
}
