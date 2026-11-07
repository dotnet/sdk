// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet;

internal static class RuntimeIdentifierUtilities
{
    /// <summary>
    /// Converts the amd64 architecture alias to x64, leaving other runtime identifiers unchanged.
    /// </summary>
    public static string Normalize(string rid)
    {
        if (GetArchFromRid(rid) == "amd64")
        {
            return $"{GetOsFromRid(rid)}-x64";
        }

        return rid;
    }

    private static string GetOsFromRid(string rid)
        => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));

    private static string GetArchFromRid(string rid)
        => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);
}
