// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Test.Tests;

/// <summary>
/// Integration tests for device selection in dotnet test
/// </summary>
public class GivenDotnetTestSelectsDevice : SdkTest
{
    public GivenDotnetTestSelectsDevice(ITestOutputHelper log) : base(log)
    {
    }

    [Fact]
    public void ItFailsInNonInteractiveMode_WhenMultipleDevicesAvailableAndNoneSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework);

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"));
    }

    [Theory]
    [InlineData("test-device-1")]
    [InlineData("test-device-2")]
    public void ItRunsTestsWithSpecifiedDevice(string deviceId)
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: deviceId)
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId);

        result.Should().Pass();
    }

    [Fact]
    public void ItAutoSelectsSingleDevice()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        // In test/CI environments, IsInteractive defaults to false (stdout is redirected),
        // so single device should be auto-selected without prompting.
        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true");

        result.Should().Pass();
    }

    [Fact]
    public void ItPromptsForTargetFrameworkWhenDeviceIsSpecifiedWithoutFramework_InNonInteractiveMode()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--device", "test-device-1");

        // Should fail because non-interactive mode can't prompt for TF
        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
    }

    [Fact]
    public void ItRunsWithDeviceAndFramework()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", "test-device-1");

        result.Should().Pass();
    }

    [Fact]
    public void ItDoesNotPromptForDeviceWhenComputeAvailableDevicesTargetDoesNotExist()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        // Should pass without any device prompting
        result.Should().Pass();
    }

    [Fact]
    public void ItAutoSelectsSingleDevicePerTfm()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        // Run without -f to test all TFMs. SingleDevice=true means one device per TFM
        // is auto-selected. Device selection happens BEFORE the build so that any
        // device-provided RuntimeIdentifier is included in the build output.
        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("-p:SingleDevice=true");

        result.Should().Pass();
    }
}
