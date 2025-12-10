// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal static class ParseResultExtension
{
    public static VersionRange? GetVersionRange(this ParseResult parseResult)
    {
        var packageVersionFromIdentityArgument = parseResult.GetValue(ToolInstallCommandParser.PackageIdentityArgument).VersionRange?.OriginalString;
        var packageVersionFromVersionOption = parseResult.GetValue(ToolInstallCommandParser.VersionOption);

        // Check that only one of these has a value
        if (!string.IsNullOrEmpty(packageVersionFromIdentityArgument) && !string.IsNullOrEmpty(packageVersionFromVersionOption))
        {
            throw new GracefulException(CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }

        string? packageVersion = packageVersionFromIdentityArgument ?? packageVersionFromVersionOption;

        bool prerelease = parseResult.GetValue(ToolInstallCommandParser.PrereleaseOption);

        if (!string.IsNullOrEmpty(packageVersion) && prerelease)
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime,
                    packageVersion));
        }

        if (prerelease)
        {
            packageVersion = "*-*";
        }

        VersionRange? versionRange = null;

        // accept 'bare' versions and interpret 'bare' versions as NuGet exact versions
        if (!string.IsNullOrEmpty(packageVersion) && NuGetVersion.TryParse(packageVersion, out NuGetVersion? version2))
        {
            return new VersionRange(minVersion: version2, includeMinVersion: true, maxVersion: version2, includeMaxVersion: true, originalString: "[" + packageVersion + "]");
        }

        if (!string.IsNullOrEmpty(packageVersion) && !VersionRange.TryParse(packageVersion, out versionRange))
        {
            throw new GracefulException(
                string.Format(
                    CliCommandStrings.ToolInstallInvalidNuGetVersionRange,
                    packageVersion));
        }
        return versionRange;
    }
}
