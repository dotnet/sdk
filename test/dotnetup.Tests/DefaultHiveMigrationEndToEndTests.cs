// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DefaultHiveMigrationEndToEndTests
{
    private const string LegacyRuntimeVersion = "8.0.0";
    private const string NewRuntimeVersion = "8.0.1";

    [TestMethod]
    public void RuntimeInstall_WithLegacyDefaultHive_UsesNewDefaultAndPreservesLegacyHive()
    {
        using var testEnvironment = new TestEnvironment();
        string dataDirectory = Path.Combine(testEnvironment.TempRoot, "dotnetup");
        string manifestPath = Path.Combine(dataDirectory, "dotnetup_manifest.json");
        string legacyRoot = testEnvironment.InstallPath;
        string newDefaultRoot = Path.Combine(dataDirectory, "dotnet");
        string legacyMarker = Path.Combine(legacyRoot, "legacy-hive.marker");

        SeedLegacyDefaultHive(manifestPath, legacyRoot, legacyMarker);

        var environmentVariables = new Dictionary<string, string>
        {
            ["DOTNET_DOTNETUP_DATA_DIR"] = dataDirectory,
            ["DOTNET_TESTHOOK_DEFAULT_DOTNET_PATH"] = string.Empty,
            ["DOTNET_TESTHOOK_DEFAULT_INSTALL_PATH"] = string.Empty,
            ["DOTNET_TESTHOOK_MANIFEST_PATH"] = string.Empty,
        };
        string[] installArguments =
        [
            "runtime", "install", NewRuntimeVersion,
            "--interactive", "false",
            "--set-default-install", "false",
            "--no-progress",
        ];

        (int exitCode, string output) = DotnetupTestUtilities.RunDotnetupProcess(
            installArguments,
            captureOutput: true,
            workingDirectory: testEnvironment.TempRoot,
            environmentVariables: environmentVariables);

        exitCode.Should().Be(0, $"dotnetup exited with code {exitCode}. Output:\n{output}");
        File.Exists(legacyMarker).Should().BeTrue("the legacy hive must not be removed or modified");
        Directory.Exists(GetRuntimeDirectory(legacyRoot, LegacyRuntimeVersion)).Should().BeTrue();
        Directory.Exists(GetRuntimeDirectory(legacyRoot, NewRuntimeVersion)).Should().BeFalse(
            "an install with no explicit path must not use the legacy default hive");
        File.Exists(Path.Combine(newDefaultRoot, DotnetupUtilities.GetDotnetExeName())).Should().BeTrue();
        Directory.Exists(GetRuntimeDirectory(newDefaultRoot, NewRuntimeVersion)).Should().BeTrue();

        DotnetupManifestData manifestData;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            manifestData = new DotnetupSharedManifest(manifestPath).ReadManifest();
        }

        manifestData.DotnetRoots.Should().HaveCount(2);
        DotnetRootEntry legacyEntry = manifestData.DotnetRoots.Should().ContainSingle(
            root => DotnetupUtilities.PathsEqual(root.Path, legacyRoot)).Subject;
        legacyEntry.InstallSpecs.Should().ContainSingle(spec =>
            spec.Component == InstallComponent.Runtime && spec.VersionOrChannel == LegacyRuntimeVersion);
        legacyEntry.Installations.Should().ContainSingle(installation =>
            installation.Component == InstallComponent.Runtime && installation.Version == LegacyRuntimeVersion);

        DotnetRootEntry newDefaultEntry = manifestData.DotnetRoots.Should().ContainSingle(
            root => DotnetupUtilities.PathsEqual(root.Path, newDefaultRoot)).Subject;
        newDefaultEntry.Installations.Should().ContainSingle(installation =>
            installation.Component == InstallComponent.Runtime && installation.Version == NewRuntimeVersion);

        (exitCode, output) = DotnetupTestUtilities.RunDotnetupProcess(
            ["env", "script", "--shell", "pwsh", "--dotnet"],
            captureOutput: true,
            workingDirectory: testEnvironment.TempRoot,
            environmentVariables: environmentVariables);

        exitCode.Should().Be(0, $"dotnetup env script exited with code {exitCode}. Output:\n{output}");
        output.Should().Contain(newDefaultRoot);
        output.Should().NotContain($"$env:DOTNET_ROOT = '{legacyRoot}'");
    }

    private static void SeedLegacyDefaultHive(string manifestPath, string legacyRoot, string markerPath)
    {
        Directory.CreateDirectory(GetRuntimeDirectory(legacyRoot, LegacyRuntimeVersion));
        File.WriteAllText(markerPath, "legacy hive");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

        string manifestJson = $$"""
            {
              "schemaVersion": "1",
              "dotnetRoots": [
                {
                  "path": {{JsonSerializer.Serialize(legacyRoot)}},
                  "architecture": "{{InstallerUtilities.GetDefaultInstallArchitecture()}}",
                  "installSpecs": [
                    {
                      "component": "Runtime",
                      "versionOrChannel": "{{LegacyRuntimeVersion}}",
                      "installSource": "Explicit",
                      "globalJsonPath": null
                    }
                  ],
                  "installations": [
                    {
                      "component": "Runtime",
                      "version": "{{LegacyRuntimeVersion}}",
                      "subcomponents": [
                        "host/fxr/{{LegacyRuntimeVersion}}",
                        "shared/Microsoft.NETCore.App/{{LegacyRuntimeVersion}}"
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        File.WriteAllText(manifestPath, manifestJson);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson));
        File.WriteAllText(manifestPath + ".sha256", Convert.ToHexString(hash));
    }

    private static string GetRuntimeDirectory(string root, string version)
        => Path.Combine(root, "shared", InstallComponentExtensions.RuntimeFrameworkName, version);
}