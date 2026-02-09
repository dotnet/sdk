// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

internal class UpdateChannel
{
    public string Name { get; set; }

    public UpdateChannel(string name)
    {
        Name = name;
    }

    public bool IsFullySpecifiedVersion()
    {
        return ReleaseVersion.TryParse(Name, out _);
    }

    /// <summary>
    /// Checks if the channel string looks like an SDK version or feature band pattern rather than a runtime version.
    /// SDK versions have a third component >= 100 (e.g., "9.0.103", "9.0.304") or use "xx" patterns (e.g., "9.0.1xx").
    /// Runtime versions have a third component < 100 (e.g., "9.0.12", "9.0.0").
    /// </summary>
    /// <remarks>
    /// We cannot use ReleaseVersion.SdkFeatureBand here because ReleaseVersion parses any valid semantic version
    /// without knowing if it's an SDK or runtime version. For example, both "9.0.103" (SDK) and "9.0.12" (runtime)
    /// would parse successfully, but SdkFeatureBand would return 100 for the SDK version and 0 for the runtime version.
    /// Since we're validating user input where we don't know the intent, we use a heuristic: any third component >= 100
    /// or containing 'x' is likely an SDK version/feature band and should be rejected for runtime installations.
    /// </remarks>
    public bool IsSdkVersionOrFeatureBand()
    {
        var parts = Name.Split('.');
        if (parts.Length < 3)
        {
            return false;
        }

        string thirdPart = parts[2];

        // Check for feature band patterns like "1xx", "2xx", "12x"
        if (thirdPart.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if it's a numeric SDK version (patch >= 100 indicates SDK, e.g., "9.0.103")
        // Runtime patches are typically < 100 (e.g., "9.0.12")
        if (int.TryParse(thirdPart, out int patch) && patch >= 100)
        {
            return true;
        }

        return false;
    }
}
