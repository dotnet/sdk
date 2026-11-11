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
    [DataRow(true)]
    [DataRow(false)]
    [OSCondition(OperatingSystems.Windows)]
    public void ItPersistsTraceTelemetryToTheStorageDirectory(bool managed)
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;
        var storageDir = Path.Combine(testDir, "telemetry-storage");
        Directory.CreateDirectory(storageDir);

        string sessionId = Guid.NewGuid().ToString();
        var command = CreateCommand(testDir, storageDir, sessionId, managed);
        command.Execute().Should().Pass();
        AssertPersistedTelemetry(storageDir, sessionId);
    }

    // dotnet.dll and dotnet-aot run with different AppContext.BaseDirectory values, which the exporter hashes into the
    // storage sub directory name unless Azure.Monitor.OpenTelemetry.Exporter.StorageSubDirectory is set. Sharing the
    // directory lets whichever entry point runs next upload what the other one persisted.
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [OSCondition(OperatingSystems.Windows)]
    public void ItDrainsTelemetryPersistedByTheOtherEntryPoint(bool writerIsManaged)
    {
        var testDir = TestAssetsManager.CreateTestDirectory(identifier: writerIsManaged.ToString()).Path;
        var storageDir = Path.Combine(testDir, "telemetry-storage");
        Directory.CreateDirectory(storageDir);

        string writerSessionId = Guid.NewGuid().ToString();
        CreateCommand(testDir, storageDir, writerSessionId, writerIsManaged).Execute().Should().Pass();
        FindPersistedTelemetry(storageDir, writerSessionId).Should().NotBeNull();

        // The writer's shutdown drain can lease its own blob before the process exits. Expire those leases as if the
        // lease period had elapsed so that only the drainer can hold a lease afterwards.
        ExpireLeases(storageDir);
        FindPersistedTelemetry(storageDir, writerSessionId).Should().EndWith(".blob");

        // A failing command gives the shutdown drain a budget, so the drainer reliably starts draining before exit.
        // The upload fails because of the unreachable proxy, which leaves every blob the drain picked up leased.
        string drainerSessionId = Guid.NewGuid().ToString();
        CreateCommand(testDir, storageDir, drainerSessionId, managed: !writerIsManaged, "solution", "list").Execute().Should().ExitWith(1);

        string? writerBlob = FindPersistedTelemetry(storageDir, writerSessionId);
        string? drainerBlob = FindPersistedTelemetry(storageDir, drainerSessionId);
        drainerBlob.Should().NotBeNull();
        Path.GetDirectoryName(writerBlob).Should().Be(Path.GetDirectoryName(drainerBlob), "the managed and AOT entry points should share one storage sub directory");
        writerBlob.Should().EndWith(".lock", "the other entry point should have leased the blob to upload it");
    }

    private TestCommand CreateCommand(string testDir, string storageDir, string sessionId, bool managed, params string[] args)
    {
        if (args.Length == 0)
        {
            args = ["--help"];
        }

        string cliAssembly = Path.Combine(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "dotnet.dll");
        var command = new DotnetCommand(Log, managed ? ["exec", cliAssembly, .. args] : args)
            .WithWorkingDirectory(testDir)
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_DISABLE_TRACE_EXPORT", "false")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_SESSIONID", sessionId)
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_STORAGE_PATH", storageDir)
            .WithEnvironmentVariable("HTTPS_PROXY", "http://127.0.0.1:1")
            .WithEnvironmentVariable("HTTP_PROXY", "http://127.0.0.1:1")
            .WithEnvironmentVariable("ALL_PROXY", "http://127.0.0.1:1")
            .WithEnvironmentVariable("NO_PROXY", "")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_ENABLE_EXPORTER", "false")
            .WithEnvironmentVariable("DOTNET_CLI_ENABLEAOT", managed ? "false" : "true")
            .WithEnvironmentVariable("OTEL_SDK_DISABLED", "false")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_SHUTDOWN_TIMEOUT_MS", "1");

        foreach (string variable in Microsoft.DotNet.Cli.EnvironmentVariableNames.OtlpExporterEnvVars)
        {
            command.EnvironmentToRemove.Add(variable);
        }

        foreach (var ciVariable in CIEnvironmentVariables)
        {
            command.EnvironmentToRemove.Add(ciVariable);
        }

        return command;
    }

    private static string[] PersistedTelemetryFiles(string storageDir) =>
        Directory.GetFiles(storageDir, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".blob", StringComparison.Ordinal) || path.EndsWith(".lock", StringComparison.Ordinal))
            .ToArray();

    private static string? FindPersistedTelemetry(string storageDir, string sessionId) =>
        PersistedTelemetryFiles(storageDir).SingleOrDefault(path => File.ReadAllText(path).Contains(sessionId, StringComparison.Ordinal));

    // A leased blob is renamed from "<name>.blob" to "<name>.blob@<expiry>.lock"; restoring the name releases the lease.
    private static void ExpireLeases(string storageDir)
    {
        foreach (var lease in Directory.GetFiles(storageDir, "*.lock", SearchOption.AllDirectories))
        {
            File.Move(lease, lease[..lease.LastIndexOf('@')]);
        }
    }

    private static void AssertPersistedTelemetry(string storageDir, string sessionId)
    {
        // The command should have persisted at least one telemetry blob to the storage directory.
        var blobs = PersistedTelemetryFiles(storageDir);
        blobs.Should().NotBeEmpty("the CLI should persist trace telemetry to the configured storage directory");
        blobs.Select(File.ReadAllText).Should().Contain(payload => payload.Contains(sessionId, StringComparison.Ordinal));

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
