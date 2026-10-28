// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public struct GlobMatch(bool isMatch, string pattern = null, string stem = null, string capturedStem = null)
{
    public bool IsMatch { get; set; } = isMatch;

    public string Pattern { get; set; } = pattern;

    public string Stem { get; set; } = stem;

    // The portion of the path matched exclusively by **, without any trailing literal segments from the pattern.
    // For **/index.html matching admin/index.html: "admin". For wwwroot/** matching wwwroot/css/file.css: "css/file.css".
    // Empty string for patterns without ** or when ** captures nothing.
    public string CapturedStem { get; set; } = capturedStem;
}
