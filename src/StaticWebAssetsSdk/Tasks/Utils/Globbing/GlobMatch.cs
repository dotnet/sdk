// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public struct GlobMatch(bool isMatch, string stem)
{
    public bool IsMatch { get; set; } = isMatch;
    public string Pattern { get; set; } = stem;

    public string GetStem(string path)
    {
        return null;
    }
}
