// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dnup.Tests.Utilities;

/// <summary>
/// Extension methods for working with UpdateChannel in tests
/// </summary>
internal static class UpdateChannelExtensions
{
    /// <summary>
    /// Determines if a channel represents a fully specified version (e.g. 9.0.103)
    /// as opposed to a feature band (e.g. 9.0.1xx) or a special channel (e.g. lts)
    /// </summary>
    public static bool IsFullySpecifiedVersion(this UpdateChannel channel)
    {
        var parts = channel.Name.Split('.');

        // Special channels are not fully specified versions
        if (string.Equals(channel.Name, "lts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(channel.Name, "sts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(channel.Name, "preview", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(channel.Name, "latest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // For a version to be fully specified, it needs at least 3 parts (major.minor.patch)
        if (parts.Length < 3)
        {
            return false;
        }

        // If the third part contains 'xx' (like '1xx'), it's a feature band, not a fully specified version
        if (parts[2].Contains("xx"))
        {
            return false;
        }

        // If we can parse the third part as an integer, it's likely a fully specified version
        return int.TryParse(parts[2], out _);
    }
}
