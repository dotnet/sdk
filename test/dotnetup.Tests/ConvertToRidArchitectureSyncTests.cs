// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

// scripts/get-dotnetup.ps1 is published and downloaded standalone (via aka.ms),
// so it cannot dot-source eng/sdk-tools.ps1 at runtime and must carry its own copy
// of ConvertTo-RidArchitecture. These tests guard against the two copies silently diverging.
public class ConvertToRidArchitectureSyncTests
{
    private const string BeginMarker = "# BEGIN-SYNC ConvertTo-RidArchitecture";
    private const string EndMarker = "# END-SYNC ConvertTo-RidArchitecture";

    private static string RepoRoot { get; } = Path.GetFullPath(
        typeof(ConvertToRidArchitectureSyncTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "RepoRoot").Value!);

    [Fact]
    public void ConvertToRidArchitecture_IsIdentical_BetweenSdkToolsAndGetDotnetup()
    {
        var sdkToolsPath = Path.Combine(RepoRoot, "eng", "sdk-tools.ps1");
        var getDotnetupPath = Path.Combine(RepoRoot, "scripts", "get-dotnetup.ps1");

        File.Exists(sdkToolsPath).Should().BeTrue($"expected to find '{sdkToolsPath}'");
        File.Exists(getDotnetupPath).Should().BeTrue($"expected to find '{getDotnetupPath}'");

        var sdkToolsBlock = ExtractSyncBlock(sdkToolsPath);
        var getDotnetupBlock = ExtractSyncBlock(getDotnetupPath);

        // Guard against a vacuous pass: if both blocks were empty (e.g. the
        // function body was deleted but the markers left in place), they would
        // still compare equal. Require the real function to be present first.
        sdkToolsBlock.Should().Contain(
            "function ConvertTo-RidArchitecture",
            $"the SYNC block in '{sdkToolsPath}' must contain the function definition");

        getDotnetupBlock.Should().Be(
            sdkToolsBlock,
            "ConvertTo-RidArchitecture must stay identical in eng/sdk-tools.ps1 and " +
            "scripts/get-dotnetup.ps1 (the latter is published standalone and cannot " +
            "dot-source the former). Update both copies together.");
    }

    // Returns the lines between the BEGIN/END sync markers, normalized so trailing
    // whitespace and line-ending differences do not cause false failures.
    private static string ExtractSyncBlock(string scriptPath)
    {
        var lines = File.ReadAllLines(scriptPath);

        var begin = Array.FindIndex(lines, l => l.TrimStart().StartsWith(BeginMarker, StringComparison.Ordinal));
        begin.Should().BeGreaterThanOrEqualTo(0, $"'{BeginMarker}' must exist in '{scriptPath}'");

        var end = Array.FindIndex(lines, begin + 1, l => l.TrimStart().StartsWith(EndMarker, StringComparison.Ordinal));
        end.Should().BeGreaterThan(begin, $"'{EndMarker}' must follow the begin marker in '{scriptPath}'");

        var inner = lines[(begin + 1)..end].Select(l => l.TrimEnd());
        return string.Join("\n", inner);
    }
}
