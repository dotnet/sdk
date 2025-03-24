// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Installer.Tests;

public enum Architecture
{
    X64,
    Arm64
}

public static class Config
{
    public static string AssetsDirectory { get; } = GetRuntimeConfig(AssetsDirectorySwitch);
    const string AssetsDirectorySwitch = RuntimeConfigSwitchPrefix + nameof(AssetsDirectory);

    public static string PackagesDirectory { get; } = GetRuntimeConfig(PackagesDirectorySwitch);
    const string PackagesDirectorySwitch = RuntimeConfigSwitchPrefix + nameof(PackagesDirectory);

    public static string ScenarioTestsNuGetConfigPath { get; } = GetRuntimeConfig(ScenarioTestsNuGetConfigSwitch);
    const string ScenarioTestsNuGetConfigSwitch = RuntimeConfigSwitchPrefix + nameof(ScenarioTestsNuGetConfigPath);

    public static Architecture Architecture { get; } = GetArchitecture(GetRuntimeConfig(ArchitectureSwitch));
    const string ArchitectureSwitch = RuntimeConfigSwitchPrefix + nameof(Architecture);

    public static bool TestRpmPackages { get; } = TryGetRuntimeConfig(TestRpmPackagesSwitch, out bool value) ? value : false;
    const string TestRpmPackagesSwitch = RuntimeConfigSwitchPrefix + nameof(TestRpmPackages);

    public static bool TestDebPackages { get; } = TryGetRuntimeConfig(TestDebPackagesSwitch, out bool value) ? value : false;
    const string TestDebPackagesSwitch = RuntimeConfigSwitchPrefix + nameof(TestDebPackages);

    public static bool KeepDockerImages { get; } = TryGetRuntimeConfig(KeepDockerImagesSwitch, out bool value) ? value : false;
    const string KeepDockerImagesSwitch = RuntimeConfigSwitchPrefix + nameof(KeepDockerImages);

    public const string RuntimeConfigSwitchPrefix = "Microsoft.DotNet.Installer.Tests.";

    public static Architecture GetArchitecture(string architecture) => architecture switch
    {
        "x64" => Architecture.X64,
        "arm64" => Architecture.Arm64,
        _ => throw new ArgumentException($"Unknown architecture: {architecture}")
    };

    public static string GetRuntimeConfig(string key)
    {
        return TryGetRuntimeConfig(key, out string? value) ? value : throw new InvalidOperationException($"Runtime config setting '{key}' must be specified");
    }

    public static bool TryGetRuntimeConfig(string key, out bool value)
    {
        string? rawValue = (string?)AppContext.GetData(key);
        if (string.IsNullOrEmpty(rawValue))
        {
            value = default!;
            return false;
        }
        value = bool.Parse(rawValue);
        return true;
    }

    public static bool TryGetRuntimeConfig(string key, [NotNullWhen(true)] out string? value)
    {
        value = (string?)AppContext.GetData(key);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        return true;
    }
}
