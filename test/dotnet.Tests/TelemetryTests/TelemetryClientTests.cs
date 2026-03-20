// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests.TelemetryTests;

public class TelemetryClientTests(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void ItWritesTelemetryLogs()
    {
        var testDir = TestAssetsManager.CreateTestDirectory().Path;
        var logFile = Path.Combine(testDir, "TelemLog.json");

        new DotnetCommand(Log, "--info")
            .WithWorkingDirectory(testDir)
            // TODO: Make mechanism to run test without actually sending telemetry to App Insights.
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "false")
            .WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_LOG_PATH", logFile)
            .Execute().Should().Pass();

        new FileInfo(logFile)
            .Should()
            .Exist();
    }
}
