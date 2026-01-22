// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class VersionRangeUtilities
{
    public static VersionRange? GetVersionRange(
        string? packageVersionFromIdentityArgument,
        string? packageVersionFromVersionOption,
        bool prerelease)
    {
        // Check that only one of these has a value
        if (!string.IsNullOrEmpty(packageVersionFromIdentityArgument) && !string.IsNullOrEmpty(packageVersionFromVersionOption))
        {
            throw new GracefulException(CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }

        var packageVersion = packageVersionFromIdentityArgument ?? packageVersionFromVersionOption;
        if (string.IsNullOrEmpty(packageVersion))
        {
            if (!prerelease)
            {
                return null;
            }

            packageVersion = "*-*";
        }
        else if (prerelease)
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime,
                    packageVersion));
        }

        // accept 'bare' versions and interpret 'bare' versions as NuGet exact versions
        if (NuGetVersion.TryParse(packageVersion, out var version))
        {
            return new VersionRange(minVersion: version, includeMinVersion: true, maxVersion: version, includeMaxVersion: true, originalString: "[" + packageVersion + "]");
        }

        if (VersionRange.TryParse(packageVersion, out var versionRange))
        {
            return versionRange;
        }

        throw new GracefulException(
            string.Format(
                CliCommandStrings.ToolInstallInvalidNuGetVersionRange,
                packageVersion));
    }
}
