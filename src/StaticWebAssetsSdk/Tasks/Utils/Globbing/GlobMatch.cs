// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public struct GlobMatch(bool isMatch, string pattern = null, string stem = null)
{
    public bool IsMatch { get; set; } = isMatch;

    public string Pattern { get; set; } = pattern;

    public string Stem { get; set; } = stem;
}
