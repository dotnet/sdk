// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.New;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.New.Tests;

// Regression tests for https://github.com/dotnet/sdk/issues/51669
// Specifically validates numeric ordering across one vs two digit majors (9 vs 10) to ensure we do NOT alpha-sort "10" ahead of "9".
public class BuiltInTemplatePackageProviderTests
{
    [Fact]
    public void GetBestVersionsByMajorMinor_UsesHighestPatchAndNumericOrderingAcrossSingleAndDoubleDigitMajors()
    {
        // SDK version (NewCommandParser) must be >= 10.x for both 9.x and 10.x buckets to be included.
        var sdkVersion = typeof(NewCommandParser).Assembly.GetName().Version ?? new();
        Assert.True(sdkVersion.Major >= 10, "This test requires running under an SDK whose major version is at least 10.");

        // Build synthetic version directories:
        // 9.0 bucket: highest patch should be 200 (release preferred over preview)
        // 10.0 bucket: highest patch should be 300 (release preferred over rc)
        var versionDirInfo = new Dictionary<string, SemanticVersion>
        {
            { @"templates\9.0.101", new SemanticVersion(9, 0, 101) },
            { @"templates\9.0.200-preview.1", new SemanticVersion(9, 0, 200, releaseLabel: "preview.1") },
            { @"templates\9.0.200", new SemanticVersion(9, 0, 200) },

            { @"templates\10.0.100", new SemanticVersion(10, 0, 100) },
            { @"templates\10.0.300-rc.1", new SemanticVersion(10, 0, 300, releaseLabel: "rc.1") },
            { @"templates\10.0.300", new SemanticVersion(10, 0, 300) }
        };

        var result = BuiltInTemplatePackageProvider.GetBestVersionsByMajorMinor(versionDirInfo);

        // Expect one entry per (Major.Minor) bucket: 9.0 and 10.0
        Assert.Equal(2, result.Count);

        // Parse back selected versions (strip any prerelease suffix).
        var parsed = result.Select(p =>
        {
            var name = p.Split('\\').Last();
            var baseVersion = name.Split('-', 2)[0];
            return (Path: p, Version: SemanticVersion.Parse(baseVersion));
        }).ToList();

        // Validate that ordering is numeric ascending: 9.x then 10.x
        Assert.Equal(9, parsed[0].Version.Major);
        Assert.Equal(10, parsed[1].Version.Major);

        // Validate highest patch chosen per bucket (release preferred over prerelease)
        Assert.Equal(200, parsed[0].Version.Patch); // 9.0.200
        Assert.Equal(300, parsed[1].Version.Patch); // 10.0.300

        // Ensure we did not accidentally select prerelease directories
        Assert.DoesNotContain(result, r => r.Contains("preview.1"));
        Assert.DoesNotContain(result, r => r.Contains("rc.1"));
    }

    [Fact]
    public void GetBestVersionsByMajorMinor_DoesNotAlphabeticallyPlaceTenBeforeNine()
    {
        // This is a more direct assertion against the original bug: alpha ordering of keys would have produced "10.0.*" before "9.0.*".
        var sdkVersion = typeof(NewCommandParser).Assembly.GetName().Version ?? new();
        Assert.True(sdkVersion.Major >= 10, "This test requires running under an SDK whose major version is at least 10.");

        var versionDirInfo = new Dictionary<string, SemanticVersion>
        {
            { @"templates\9.0.150", new SemanticVersion(9, 0, 150) },
            { @"templates\10.0.250", new SemanticVersion(10, 0, 250) }
        };

        var result = BuiltInTemplatePackageProvider.GetBestVersionsByMajorMinor(versionDirInfo);

        Assert.Equal(2, result.Count);

        // Under the buggy alpha key ordering, index 0 would be 10.0.250.
        // Correct numeric ordering must yield 9.x first.
        var firstName = result[0].Split('\\').Last();
        Assert.StartsWith("9.0.", firstName);
        var secondName = result[1].Split('\\').Last();
        Assert.StartsWith("10.0.", secondName);
    }
}
