// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if MICROSOFT_ENABLE_TELEMETRY_AZURE_MONITOR

using System.Text;
using System.Text.Json.Nodes;

namespace Microsoft.DotNet.Tests.TelemetryTests;

[TestClass]
public class PersistentStorageTelemetryE2ETests : SdkTest
{
    // Every environment variable inspected by CIEnvironmentDetectorForTelemetry. Cleared for the
    // spawned CLI so it takes the local persist-then-drain path rather than the CI direct-export
    // path, regardless of the CI system this test happens to run on.
    private static readonly string[] CIEnvironmentVariables =
    [
        "TF_BUILD", "GITHUB_ACTIONS", "APPVEYOR", "CI", "TRAVIS", "CIRCLECI",
        "CODEBUILD_BUILD_ID", "AWS_REGION", "BUILD_ID", "BUILD_URL", "PROJECT_ID",
        "TEAMCITY_VERSION", "JB_SPACE_API_URL"
    ];

    // Only runs on Windows because the Azure Monitor / persistent-storage telemetry path is only
    // compiled and exercised on Microsoft (non-source-build) Windows builds.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ItPersistsTraceTelemetryToTheStorageDirectory()
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;
        var storageDir = Path.Combine(testDir, "telemetry-storage");
        Directory.CreateDirectory(storageDir);

        var command = new DotnetCommand(Log, "--help")
            .WithWorkingDirectory(testDir)
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false")
            // Leave DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT unset so the persist path runs.
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_STORAGE_PATH", storageDir);

        // Force the non-CI (persist-then-drain) path: in a CI environment the CLI instead uses the
        // standard Azure Monitor exporter and does not persist blobs. Clear every variable the CI
        // detector inspects so this test exercises the persist path even when it runs on CI hosts.
        foreach (var ciVariable in CIEnvironmentVariables)
        {
            command = command.WithEnvironmentVariable(ciVariable, "");
        }

        command
            .Execute()
            .Should()
            .Pass();

        // The command should have persisted at least one telemetry blob to the storage directory.
        var blobs = Directory.GetFiles(storageDir, "*", SearchOption.AllDirectories);
        blobs.Should().NotBeEmpty("the CLI should persist trace telemetry to the configured storage directory");

        // Every persisted blob should be valid newline-delimited JSON telemetry envelopes.
        foreach (var blob in blobs)
        {
            var content = Encoding.UTF8.GetString(File.ReadAllBytes(blob));
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().NotBeEmpty();

            foreach (var line in lines)
            {
                var envelope = JsonNode.Parse(line);
                envelope.Should().NotBeNull();
                envelope!["name"].Should().NotBeNull();
                envelope["iKey"].Should().NotBeNull();
                envelope["data"]!["baseType"].Should().NotBeNull();
            }
        }
    }
}

#endif
