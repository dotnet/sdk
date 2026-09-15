// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.DotNet.Cli;

namespace dotnet.Tests;

[TestClass]
public class OpenHarmonyEnvironmentDefaultsTests
{
    [TestMethod]
    public void ApplyIsANoOpOutsideOpenHarmony()
    {
        if (OperatingSystem.IsOSPlatform("openharmony"))
        {
            // The OpenHarmony branch sets the child-process defaults; it is exercised on
            // device rather than here.
            return;
        }

        var before = SnapshotEnvironment();

        OpenHarmonyEnvironmentDefaults.Apply();

        SnapshotEnvironment().Should().Equal(before);
    }

    [TestMethod]
    public void DefaultsPinTheChildProcessContract()
    {
        var defaults = OpenHarmonyEnvironmentDefaults.Defaults.ToDictionary(d => d.Name, d => d.Value);

        defaults.Should().HaveCount(4);
        defaults.Should().Contain("DOTNET_EnableWriteXorExecute", "0");
        defaults.Should().Contain("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT", "1");
        defaults.Should().Contain(EnvironmentVariableNames.TELEMETRY_OPTOUT, "1");
        defaults.Should().Contain(EnvironmentVariableNames.DOTNET_NOLOGO, "1");
    }

    private static string[] SnapshotEnvironment() =>
        Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(e => $"{e.Key}={e.Value}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
}
