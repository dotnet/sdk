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
            .And.HaveStdOutContaining($"RuntimeIdentifier: {RuntimeInformation.RuntimeIdentifier}");

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

    [Fact]
    public void ItCallsDeployToDeviceTargetWhenDeviceIsSpecified()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string deviceId = "test-device-1";
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId, "-bl");

        // Should run successfully
        result.Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}");

        // Verify the binlog file was created and the DeployToDevice target ran with the correct Device property
        File.Exists(binlogPath).Should().BeTrue("the binlog file should be created");
        AssertTargetInBinlog(binlogPath, "DeployToDevice",
            targets =>
            {
                targets.Should().NotBeEmpty("DeployToDevice target should have been executed");
                var messages = targets.First().FindChildrenRecursive<Message>();
                var deployMessage = messages.FirstOrDefault(m => m.Text.Contains("DeployToDevice: Deployed to device"));
                deployMessage.Should().NotBeNull("the DeployToDevice target should have logged the device");
                deployMessage.Text.Should().Contain(deviceId, "the Device property should be passed to DeployToDevice");
            });
    }

    [Fact]
    public void ItCallsDeployToDeviceTargetEvenWithNoBuild()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string deviceId = "test-device-1";
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        // First build the project with the device so DeviceInfo gets generated
        // Note: dotnet build doesn't support --device flag, use -p:Device= instead
        new DotnetCommand(Log, "build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, $"-p:Device={deviceId}")
            .Should().Pass();

        // Now run with --no-build
        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId, "--no-build", "-bl");

        // Should run successfully
        result.Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}");

        // Verify the binlog file was created and the DeployToDevice target ran
        File.Exists(binlogPath).Should().BeTrue("the binlog file should be created");
        AssertTargetInBinlog(binlogPath, "DeployToDevice",
            targets => targets.Should().NotBeEmpty("DeployToDevice target should have been executed even with --no-build"));
    }

    [Fact]
    public void ItCallsDeployToDeviceTargetWhenDeviceIsAutoSelected()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        // Run with auto-selection of single device
        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "-bl");

        // Should run successfully
        result.Should().Pass()
            .And.HaveStdOutContaining("Device: single-device");

        // Verify the binlog file was created
        File.Exists(binlogPath).Should().BeTrue("the binlog file should be created");

        // DeployToDevice target should have been called with the correct Device and RuntimeIdentifier
        AssertTargetInBinlog(binlogPath, "DeployToDevice",
            targets =>
            {
                targets.Should().NotBeEmpty("DeployToDevice target should have been executed when a device is selected");
                var messages = targets.First().FindChildrenRecursive<Message>();
                var deployMessage = messages.FirstOrDefault(m => m.Text.Contains("DeployToDevice: Deployed to device"));
                deployMessage.Should().NotBeNull("the DeployToDevice target should have logged the device");
                deployMessage.Text.Should().Contain("single-device", "the auto-selected Device should be passed to DeployToDevice");
                // The single-device has RuntimeIdentifier="$(NETCoreSdkRuntimeIdentifier)" which resolves to the current SDK RID
                deployMessage.Text.Should().Contain(RuntimeInformation.RuntimeIdentifier, "the RuntimeIdentifier from the device should be passed to DeployToDevice");
            });
    }

    [Fact]
    public void ItPassesRuntimeIdentifierToDeployToDeviceTarget()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices")
            .WithSource();

        string deviceId = "test-device-1";
        string rid = RuntimeInformation.RuntimeIdentifier;

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId, "--runtime", rid);

        // Should run successfully and show the RuntimeIdentifier in the app output
        result.Should().Pass()
            .And.HaveStdOutContaining($"Device: {deviceId}")
            .And.HaveStdOutContaining($"RuntimeIdentifier: {rid}");
    }

    [Fact]
    public void ItPassesEnvironmentVariablesToTargets()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices", identifier: "EnvVarTargets")
            .WithSource();

        string deviceId = "test-device-1";
        string buildBinlogPath = Path.Combine(testInstance.Path, "msbuild.binlog");
        string runBinlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId, 
                     "-e", "FOO=BAR", "-e", "ANOTHER=VALUE",
                     "-bl");

        result.Should().Pass();

        // Verify the binlog files were created
        File.Exists(buildBinlogPath).Should().BeTrue("the build binlog file should be created");
        File.Exists(runBinlogPath).Should().BeTrue("the run binlog file should be created");

        // Verify environment variables were passed to Build target (out-of-process build)
        AssertTargetInBinlog(buildBinlogPath, "_LogRuntimeEnvironmentVariableDuringBuild",
            targets => 
            {
                targets.Should().NotBeEmpty("_LogRuntimeEnvironmentVariableDuringBuild target should have executed");
                var messages = targets.First().FindChildrenRecursive<Message>();
                var envVarMessage = messages.FirstOrDefault(m => m.Text?.Contains("Build: RuntimeEnvironmentVariable=") == true);
                envVarMessage.Should().NotBeNull("the Build target should have logged the environment variables");
                envVarMessage.Text.Should().Contain("FOO=BAR").And.Contain("ANOTHER=VALUE");
            });

        // Verify environment variables were passed to ComputeRunArguments target (in-process)
        AssertTargetInBinlog(runBinlogPath, "_LogRuntimeEnvironmentVariableDuringComputeRunArguments",
            targets => 
            {
                targets.Should().NotBeEmpty("_LogRuntimeEnvironmentVariableDuringComputeRunArguments target should have executed");
                var messages = targets.First().FindChildrenRecursive<Message>();
                var envVarMessage = messages.FirstOrDefault(m => m.Text?.Contains("ComputeRunArguments: RuntimeEnvironmentVariable=") == true);
                envVarMessage.Should().NotBeNull("the ComputeRunArguments target should have logged the environment variables");
                envVarMessage.Text.Should().Contain("FOO=BAR").And.Contain("ANOTHER=VALUE");
            });

        // Verify environment variables were passed to DeployToDevice target (in-process)
        AssertTargetInBinlog(runBinlogPath, "DeployToDevice",
            targets => 
            {
                targets.Should().NotBeEmpty("DeployToDevice target should have executed");
                var messages = targets.First().FindChildrenRecursive<Message>();
                var envVarMessage = messages.FirstOrDefault(m => m.Text?.Contains("DeployToDevice: RuntimeEnvironmentVariable=") == true);
                envVarMessage.Should().NotBeNull("the DeployToDevice target should have logged the environment variables");
                envVarMessage.Text.Should().Contain("FOO=BAR").And.Contain("ANOTHER=VALUE");
            });

        // Verify the props file was created in the correct IntermediateOutputPath location
        string tempPropsFile = Path.Combine(testInstance.Path, "obj", "Debug", ToolsetInfo.CurrentTargetFramework, "dotnet-run-env.props");
        var build = BinaryLog.ReadBuild(buildBinlogPath);
        var propsFile = build.SourceFiles?.FirstOrDefault(f => f.FullPath.EndsWith("dotnet-run-env.props", StringComparison.OrdinalIgnoreCase));
        propsFile.Should().NotBeNull("dotnet-run-env.props should be embedded in the binlog");
        propsFile.FullPath.Should().Be(tempPropsFile, "the props file should be in the IntermediateOutputPath");
        File.Exists(tempPropsFile).Should().BeFalse("the temporary props file should be deleted after build");
    }

    [Fact]
    public void ItDoesNotPassEnvironmentVariablesToTargetsWithoutOptIn()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("DotnetRunDevices", identifier: "EnvVarNoOptIn")
            .WithSource();

        string deviceId = "test-device-1";
        string buildBinlogPath = Path.Combine(testInstance.Path, "msbuild.binlog");
        string runBinlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-run.binlog");

        // Run with EnableRuntimeEnvironmentVariableSupport=false to opt out of the capability
        var result = new DotnetCommand(Log, "run")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId,
                     "-e", "FOO=BAR", "-e", "ANOTHER=VALUE",
                     "-p:EnableRuntimeEnvironmentVariableSupport=false",
                     "-bl");

        result.Should().Pass();

        // Verify the binlog files were created
        File.Exists(buildBinlogPath).Should().BeTrue("the build binlog file should be created");
        File.Exists(runBinlogPath).Should().BeTrue("the run binlog file should be created");

        // Verify _LogRuntimeEnvironmentVariableDuringBuild target did NOT execute (condition failed due to no items)
        AssertTargetInBinlog(buildBinlogPath, "_LogRuntimeEnvironmentVariableDuringBuild",
            targets =>
            {
                // The target should either not execute, or execute with no environment variable message
                if (targets.Any())
                {
                    var messages = targets.First().FindChildrenRecursive<Message>();
                    var envVarMessage = messages.FirstOrDefault(m => m.Text?.Contains("Build: RuntimeEnvironmentVariable=") == true);
                    envVarMessage.Should().BeNull("the Build target should NOT have logged the environment variables when not opted in");
                }
            });

        // Verify _LogRuntimeEnvironmentVariableDuringComputeRunArguments target did NOT log env vars
        AssertTargetInBinlog(runBinlogPath, "_LogRuntimeEnvironmentVariableDuringComputeRunArguments",
            targets =>
            {
                if (targets.Any())
                {
                    var messages = targets.First().FindChildrenRecursive<Message>();
                    var envVarMessage = messages.FirstOrDefault(m => m.Text?.Contains("ComputeRunArguments: RuntimeEnvironmentVariable=") == true);
                    envVarMessage.Should().BeNull("the ComputeRunArguments target should NOT have logged the environment variables when not opted in");
                }
            });

        // Verify DeployToDevice target did NOT log actual env var values
        AssertTargetInBinlog(runBinlogPath, "DeployToDevice",
            targets =>
            {
                targets.Should().NotBeEmpty("DeployToDevice target should have executed");
                var messages = targets.First().FindChildrenRecursive<Message>();
                // The message may appear (target has no condition) but should NOT contain actual env var values
                var envVarMessage = messages.FirstOrDefault(m => m.Text?.Contains("FOO=BAR") == true || m.Text?.Contains("ANOTHER=VALUE") == true);
                envVarMessage.Should().BeNull("the DeployToDevice target should NOT have logged the actual environment variable values when not opted in");
            });

        // Verify no props file was created (since opt-in is false)
        string tempPropsFile = Path.Combine(testInstance.Path, "obj", "Debug", ToolsetInfo.CurrentTargetFramework, "dotnet-run-env.props");
        var build = BinaryLog.ReadBuild(buildBinlogPath);
        var propsFile = build.SourceFiles?.FirstOrDefault(f => f.FullPath.EndsWith("dotnet-run-env.props", StringComparison.OrdinalIgnoreCase));
        propsFile.Should().BeNull("dotnet-run-env.props should NOT be created when not opted in");
    }
}
