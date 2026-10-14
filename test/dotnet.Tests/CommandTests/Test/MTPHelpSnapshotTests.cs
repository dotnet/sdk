// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;
using CommandResult = Microsoft.DotNet.Cli.Utils.CommandResult;
using ExitCodes = Microsoft.NET.TestFramework.ExitCode;

namespace Microsoft.DotNet.Cli.Test.Tests;

public class MTPHelpSnapshotTests : SdkTest
{
    public MTPHelpSnapshotTests(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public async Task VerifyMTPHelpOutput()
    {
        TestAsset testInstance = _testAssetsManager
            .CopyTestAsset("TestProjectSolutionWithTestsAndArtifacts", Guid.NewGuid().ToString())
            .WithSource();

        CommandResult result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute(CliConstants.HelpOptionKey);

        result.ExitCode.Should().Be(ExitCodes.Success);

        var helpOutput = result.StdOut;

        // Verify we have MTP mode output (contains Extension Options section)
        helpOutput.Should().Contain("Extension Options:");
        
        var settings = new VerifySettings();
        if (Environment.GetEnvironmentVariable("USER") is string user && user.Contains("helix", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USER")))
        {
            Log.WriteLine($"CI environment detected, using snapshots directory in the current working directory {Environment.CurrentDirectory}");
            settings.UseDirectory(Path.Combine(Environment.CurrentDirectory, "snapshots"));
        }
        else
        {
            Log.WriteLine($"Using snapshots from local repository because $USER {Environment.GetEnvironmentVariable("USER")} is not helix-related");
            settings.UseDirectory("snapshots");
        }
        
        await Verify(helpOutput, settings);
    }
}
