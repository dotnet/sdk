// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>
/// Derives the release channel that <c>self update</c> uses when <c>--channel</c> is omitted
/// from the running build's SemVer prerelease label.
/// </summary>
/// <remarks>
/// A build without a prerelease label is stable, a <c>preview</c> label is the preview channel, and
/// any other label (including local development builds) or an unparseable version is daily.
/// Official daily builds currently carry the same <c>preview</c> label as preview builds, because a
/// preview is a promoted daily build rather than a rebuild, so they default to the preview channel.
/// </remarks>
internal static class SelfUpdateDefaultChannel
{
    public const string Daily = "daily";
    public const string Preview = "preview";
    public const string Stable = "stable";

    public static string FromLoadedVersion(string loadedVersion)
    {
        if (!ReleaseVersion.TryParse(loadedVersion, out var version))
        {
            return Daily;
        }

        if (string.IsNullOrEmpty(version.Prerelease))
        {
            return Stable;
        }

        var separator = version.Prerelease.IndexOf('.');
        var label = separator < 0 ? version.Prerelease : version.Prerelease[..separator];
        return string.Equals(label, Preview, StringComparison.OrdinalIgnoreCase) ? Preview : Daily;
    }
}
