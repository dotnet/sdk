// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.DotNet.Cli.Commands;
using StructuredLoggerTarget = Microsoft.Build.Logging.StructuredLogger.Target;

namespace Microsoft.DotNet.Cli.Run.Tests;

/// <summary>
/// Integration tests for device selection in dotnet run
/// </summary>
public class GivenDotnetRunSelectsDevice : SdkTest
{
    public GivenDotnetRunSelectsDevice(ITestOutputHelper log) : base(log)
    {
    }

    string ExpectedRid => OperatingSystem.IsWindows() ? "win" : (OperatingSystem.IsMacOS() ? "osx" : "linux");

    /// <summary>
    /// Helper method to assert conditions about MSBuild target execution in a binlog file
    /// </summary>
    private static void AssertTargetInBinlog(string binlogPath, string targetName, Action<IEnumerable<StructuredLoggerTarget>> assertion)
    {
        var build = BinaryLog.ReadBuild(binlogPath);
        var targets = build.FindChildrenRecursive<StructuredLoggerTarget>(
            target => target.Name == targetName);
        
        assertion(targets);
    }

    [Fact]
    public void ItFailsInNonInteractiveMode_WhenMultipleDevicesAvailableAndNoneSpecified()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"));
    }

    [Fact]
    public void ItListsDevicesForSpecifiedFramework()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--list-devices");

        result.Should().Pass()
            .And.HaveStdOutContaining("test-device-1")
            .And.HaveStdOutContaining("test-device-2")
            .And.HaveStdOutContaining("Emulator");
    }

    [Theory]
    [InlineData("test-device-1")]
    [InlineData("test-device-2")]
    public void ItRunsDifferentDevicesInMultiTargetedApp(string deviceId)
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId)
            .Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}");
    }

    [Fact]
    public void ItShowsErrorMessageWithAvailableDevices_InNonInteractiveMode()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"))
            .And.HaveStdErrContaining("test-device-1")
            .And.HaveStdErrContaining("test-device-2");
    }

    [Fact]
    public void ItDoesNotPromptForDeviceWhenComputeAvailableDevicesTargetDoesNotExist()
    {
        var testInstance = _testAssetsManager.CopyTestAsset(
                "NETFrameworkReferenceNETStandard20",
                testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
            .WithSource();

        string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

        // This project doesn't have ComputeAvailableDevices target, so it should just run
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(projectDirectory)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework)
            .Should().Pass()
            .And.HaveStdOutContaining("This string came from the test library!");
    }

    [Fact]
    public void ItTreatsEmptyDeviceSpecificationAsNotSpecified()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:Device=", "--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"));
    }

    [Fact]
    public void ItWorksWithDevicePropertySyntax()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string deviceId = "test-device-1";
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, $"-p:Device={deviceId}")
            .Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}");
    }

    [Fact]
    public void ItWorksWithDeviceWithoutRuntimeIdentifier()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string deviceId = "test-device-2";
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId)
            .Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}")
            .And.HaveStdOutContaining("RuntimeIdentifier:");
    }

    [Theory]
    [InlineData(true)]  // interactive
    [InlineData(false)] // non-interactive
    public void ItAutoSelectsSingleDeviceWithoutPrompting(bool interactive)
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        var command = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path);

        var args = new List<string> { "--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "-bl" };
        if (!interactive)
        {
            args.Add("--no-interactive");
        }

        var result = command.Execute(args.ToArray());

        // Should auto-select the single device and run successfully
        result.Should().Pass()
            .And.HaveStdOutContaining("Device: single-device")
            .And.HaveStdOutContaining($"RuntimeIdentifier: {ExpectedRid}");

        // Verify the binlog file was created and the ComputeAvailableDevices target ran
        File.Exists(binlogPath).Should().BeTrue("the binlog file should be created");
        AssertTargetInBinlog(binlogPath, "ComputeAvailableDevices", 
            targets => targets.Should().NotBeEmpty("ComputeAvailableDevices target should run to discover available devices"));
    }

    [Fact]
    public void ItCreatesBinlogWhenRequestedForDeviceSelection()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        // When /bl:device-list.binlog is specified, the verb "dotnet-run" is appended
        string binlogPath = Path.Combine(testInstance.Path, "device-list-dotnet-run.binlog");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--list-devices", "/bl:device-list.binlog");

        result.Should().Pass()
            .And.HaveStdOutContaining("test-device-1");

        // Verify the binlog file was created with the unified logger and the ComputeAvailableDevices target ran
        File.Exists(binlogPath).Should().BeTrue("the binlog file should be created when /bl: argument is provided");
        AssertTargetInBinlog(binlogPath, "ComputeAvailableDevices", 
            targets => targets.Should().NotBeEmpty("ComputeAvailableDevices target should have been executed"));
    }

    [Fact]
    public void ItFailsWhenNoDevicesAreAvailable()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:NoDevices=true", "--no-interactive");

        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandNoDevicesAvailable);
    }

    [Theory]
    [InlineData("--device")]
    [InlineData("-p:Device=")]
    public void ItDoesNotRunComputeAvailableDevicesWhenDeviceIsPreSpecified(string deviceArgPrefix)
    {
        string deviceId = "test-device-2";
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        var args = new List<string> { "--framework", ToolsetInfo.CurrentTargetFramework };
        if (deviceArgPrefix == "--device")
        {
            args.Add("--device");
            args.Add(deviceId);
        }
        else
        {
            args.Add($"{deviceArgPrefix}{deviceId}");
        }
        args.Add("-bl");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute(args.ToArray());

        // Should run successfully
        result.Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}");

        // Verify the binlog file was created and the ComputeAvailableDevices target did not run
        File.Exists(binlogPath).Should().BeTrue("the binlog file should be created");
        AssertTargetInBinlog(binlogPath, "ComputeAvailableDevices", 
            targets => targets.Should().BeEmpty("ComputeAvailableDevices target should not have been executed when device is pre-specified"));
    }

    [Fact]
    public void ItPromptsForTargetFrameworkEvenWhenDeviceIsSpecified()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string deviceId = "test-device-1";

        // Don't specify --framework, only specify --device
        // This should fail in non-interactive mode because framework selection is still needed
        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--device", deviceId, "--no-interactive");

        // Should fail with framework selection error, not device selection error
        result.Should().Fail()
            .And.HaveStdErrContaining("Your project targets multiple frameworks. Specify which framework to run using '--framework'");
    }
}
