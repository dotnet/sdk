// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.DotNet.Cli.Commands;
using System.Runtime.InteropServices;
using StructuredLoggerTarget = Microsoft.Build.Logging.StructuredLogger.Target;

namespace Microsoft.DotNet.Cli.Test.Tests;

/// <summary>
/// Integration tests for device selection in dotnet test
/// </summary>
[TestClass]
public class GivenDotnetTestSelectsDevice : SdkTest
{
    public GivenDotnetTestSelectsDevice()
    {
    }

    private static void AssertTargetInBinlog(
        string binlogPath,
        string targetName,
        Action<IEnumerable<StructuredLoggerTarget>> assertion)
    {
        var build = BinaryLog.ReadBuild(binlogPath);
        var targets = build.FindChildrenRecursive<StructuredLoggerTarget>(
            target => target.Name == targetName);

        assertion(targets);
    }

    [TestMethod]
    public void ItFailsInNonInteractiveMode_WhenMultipleDevicesAvailableAndNoneSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework);

        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"))
            // The example hint in the error output should reference 'dotnet test' (not 'dotnet run').
            .And.HaveStdErrContaining("dotnet test --device")
            .And.NotHaveStdErrContaining("dotnet run --device");
    }

    [TestMethod]
    [DataRow("test-device-1")]
    [DataRow("test-device-2")]
    public void ItRunsTestsWithSpecifiedDevice(string deviceId)
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: deviceId)
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId);

        result.Should().Pass();
    }

    [TestMethod]
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

    [TestMethod]
    public void ItPromptsForTargetFrameworkWhenDeviceIsSpecifiedWithoutFramework_InNonInteractiveMode()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--device", "test-device-1", "-bl");

        // Should fail because non-interactive mode can't prompt for TF
        result.Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));
        File.Exists(Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog"))
            .Should().BeTrue("target framework selection should be captured in the test binlog");
    }

    [TestMethod]
    public void ItRunsWithDeviceAndFramework()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", "test-device-1");

        result.Should().Pass();
    }

    [TestMethod]
    public void ItPassesEnvironmentVariablesToBuildDeployAndRunArgumentsTargets()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", "EnvironmentVariables")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute(
                "--framework", ToolsetInfo.CurrentTargetFramework,
                "--device", "test-device-1",
                "-e", "FOO=BAR",
                "-p:ModifyRuntimeEnvironmentVariable=true",
                "-bl");

        result.Should().Pass()
            .And.HaveStdOutContaining("Runtime environment variables: FOO=modified-by-target, INJECTED=injected-by-target");

        string buildBinlogPath = Path.Combine(testInstance.Path, "msbuild.binlog");
        string testBinlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");

        AssertTargetInBinlog(
            buildBinlogPath,
            "_LogRuntimeEnvironmentVariableDuringBuild",
            targets => targets.SelectMany(target => target.FindChildrenRecursive<Message>())
                .Should().Contain(message =>
                    message.Text != null &&
                    message.Text.Contains("FOO=BAR", StringComparison.Ordinal)));
        AssertTargetInBinlog(
            testBinlogPath,
            "DeployToDevice",
            targets => targets.SelectMany(target => target.FindChildrenRecursive<Message>())
                .Should().Contain(message =>
                    message.Text != null &&
                    message.Text.Contains("FOO=BAR", StringComparison.Ordinal)));
        AssertTargetInBinlog(
            testBinlogPath,
            "_LogRuntimeEnvironmentVariableDuringComputeRunArguments",
            targets => targets.SelectMany(target => target.FindChildrenRecursive<Message>())
                .Should().Contain(message =>
                    message.Text != null &&
                    message.Text.Contains("FOO=modified-by-target", StringComparison.Ordinal) &&
                    message.Text.Contains("INJECTED=injected-by-target", StringComparison.Ordinal)));

        var build = BinaryLog.ReadBuild(buildBinlogPath);
        build.SourceFiles.Should().Contain(file =>
            file.FullPath.EndsWith("dotnet-test-env.props", StringComparison.OrdinalIgnoreCase));
        File.Exists(Path.Combine(
            testInstance.Path,
            "obj",
            "Debug",
            ToolsetInfo.CurrentTargetFramework,
            "dotnet-test-env.props")).Should().BeFalse();
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void ItRunsDeviceProjectsInSolution()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", "SolutionDeviceTest")
            .WithSource();

        // Create two project subdirectories from the copied asset
        var project1Dir = Path.Combine(testInstance.Path, "Project1");
        var project2Dir = Path.Combine(testInstance.Path, "Project2");
        Directory.CreateDirectory(project1Dir);
        Directory.CreateDirectory(project2Dir);

        foreach (var dir in new[] { project1Dir, project2Dir })
        {
            File.Copy(Path.Combine(testInstance.Path, "Program.cs"), Path.Combine(dir, "Program.cs"));
            File.Copy(
                Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"),
                Path.Combine(dir, Path.GetFileName(dir) + ".csproj"));
        }

        File.Delete(Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"));
        File.Delete(Path.Combine(testInstance.Path, "Program.cs"));

        File.WriteAllText(Path.Combine(testInstance.Path, "TestSolution.slnx"),
            """
            <Solution>
              <Project Path="Project1\Project1.csproj" />
              <Project Path="Project2\Project2.csproj" />
            </Solution>
            """);

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--solution", "TestSolution.slnx", "-p:SingleDevice=true", "-bl");

        result.Should().Pass();

        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");
        File.Exists(binlogPath).Should().BeTrue("the test binlog should be created");
        AssertTargetInBinlog(
            binlogPath,
            "DeployToDevice",
            targets => targets.Should().HaveCount(4, "both target frameworks in both projects should be deployed"));
    }

    [TestMethod]
    public void ItAcceptsDeviceViaMSBuildProperty()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        // Pass Device via -p:Device= instead of --device
        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:Device=test-device-1");

        result.Should().Pass();
    }

    [TestMethod]
    public void ItShowsBuildErrorWhenBuildFailsAfterDeviceSelection()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        // Inject a compile error after device selection would succeed
        var programPath = Path.Combine(testInstance.Path, "Program.cs");
        File.AppendAllText(programPath, "\nclass Broken { int x = \"not an int\"; }");

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true");

        // Should fail with a build error, NOT a device selection error
        result.Should().Fail();
        result.StdErr.Should().NotContain(
            string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyDevice, "--device"));
    }

    [TestMethod]
    public void ItListsDevicesAndExits()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--list-devices", "-bl");

        result.Should().Pass();
        result.StdOut.Should().Contain("test-device-1");
        result.StdOut.Should().Contain("test-device-2");
        // Friendly example using "dotnet test --device ..." rather than "dotnet run --device ..."
        result.StdOut.Should().Contain("dotnet test --device");

        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");
        File.Exists(binlogPath).Should().BeTrue("device listing should be captured in the test binlog");
        AssertTargetInBinlog(
            binlogPath,
            "DeployToDevice",
            targets => targets.Should().BeEmpty("--list-devices must exit before deployment"));
    }

    [TestMethod]
    public void ItListsDevicesForSingleDeviceProject()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "--list-devices");

        result.Should().Pass();
        result.StdOut.Should().Contain("single-device");
    }

    [TestMethod]
    public void ItExitsCleanlyForListDevicesWhenMultipleTargetFrameworks_InNonInteractiveMode()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--list-devices");

        // Multi-targeted project requires a target framework prompt; non-interactive
        // mode prints guidance to stderr and exits 0 (matching `dotnet run --list-devices`,
        // which treats `--list-devices` itself as not a build failure). The example in the
        // guidance references 'dotnet test' (not 'dotnet run').
        result.Should().Pass()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"))
            .And.HaveStdErrContaining("dotnet test --framework")
            .And.NotHaveStdErrContaining("dotnet run --framework");
    }

    [TestMethod]
    public void ItListsNothingForProjectWithoutComputeAvailableDevicesTarget()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--list-devices");

        // When the project has no ComputeAvailableDevices target, --list-devices
        // exits silently with success (matches `dotnet run --list-devices`).
        result.Should().Pass();
    }

    [TestMethod]
    public void ItErrorsWhenListingDevicesForSolution()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", "ListDevicesSolution")
            .WithSource();

        // Build a solution containing the project (mirrors ItRunsDeviceProjectsInSolution layout).
        var projectDir = Path.Combine(testInstance.Path, "Project1");
        Directory.CreateDirectory(projectDir);
        File.Copy(Path.Combine(testInstance.Path, "Program.cs"), Path.Combine(projectDir, "Program.cs"));
        File.Copy(
            Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"),
            Path.Combine(projectDir, "Project1.csproj"));
        File.Delete(Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"));
        File.Delete(Path.Combine(testInstance.Path, "Program.cs"));

        File.WriteAllText(Path.Combine(testInstance.Path, "TestSolution.slnx"),
            """
            <Solution>
              <Project Path="Project1\Project1.csproj" />
            </Solution>
            """);

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--solution", "TestSolution.slnx", "--list-devices");

        // Listing devices across a solution is ambiguous: instruct the user to use --project.
        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.TestCommandUseProject);
    }

    [TestMethod]
    [DataRow(false)] // --device <id> --solution <sln>
    [DataRow(true)]  // --device <id> --framework <tfm> --solution <sln>
    public void ItErrorsWhenUsingDeviceForSolution(bool withFramework)
    {
        var identifier = withFramework ? "DeviceSolutionWithTfm" : "DeviceSolutionNoTfm";
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier)
            .WithSource();

        // Build a solution containing the project (mirrors ItErrorsWhenListingDevicesForSolution layout).
        var projectDir = Path.Combine(testInstance.Path, "Project1");
        Directory.CreateDirectory(projectDir);
        File.Copy(Path.Combine(testInstance.Path, "Program.cs"), Path.Combine(projectDir, "Program.cs"));
        File.Copy(
            Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"),
            Path.Combine(projectDir, "Project1.csproj"));
        File.Delete(Path.Combine(testInstance.Path, "DotnetTestDevices.csproj"));
        File.Delete(Path.Combine(testInstance.Path, "Program.cs"));

        File.WriteAllText(Path.Combine(testInstance.Path, "TestSolution.slnx"),
            """
            <Solution>
              <Project Path="Project1\Project1.csproj" />
            </Solution>
            """);

        var args = new List<string> { "--solution", "TestSolution.slnx", "--device", "test-device-1" };
        if (withFramework)
        {
            args.Add("--framework");
            args.Add(ToolsetInfo.CurrentTargetFramework);
        }

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute(args.ToArray());

        // A device is platform-specific and each project in a solution may have its own
        // device list, so `--device` + `--solution` is rejected regardless of whether
        // -f/--framework was provided (matches `--list-devices` + `--solution`).
        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.TestCommandUseProject);
    }

    [TestMethod]
    public void ItErrorsWhenListDevicesAndListTestsAreCombined()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", "ListDevicesWithListTests")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--list-devices", "--list-tests", "-f", "net11.0-android");

        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.CmdListDevicesAndListTestsMutuallyExclusive);
    }

    [TestMethod]
    public void ItListsDevicesForExplicitFrameworkOnMultiTargetedProject()
    {
        // DotnetTestDevices targets both net9.0 and $(CurrentTargetFramework) with different
        // devices per TFM. Passing an explicit -f should bypass the non-interactive multi-TFM
        // prompt and list devices for just the chosen TFM.
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", "ListDevicesMultiTfmExplicit")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--list-devices", "--framework", "net9.0");

        result.Should().Pass()
            .And.HaveStdOutContaining("test-device-downlevel-1")
            .And.HaveStdOutContaining("test-device-downlevel-2")
            .And.NotHaveStdOutContaining("test-device-1")
            .And.NotHaveStdOutContaining("test-device-2");
    }

    [TestMethod]
    public void ItErrorsWhenListDevicesIsCombinedWithTestModules()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", "ListDevicesWithTestModules")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--list-devices", "--test-modules", "**/*.dll");

        // --test-modules bypasses project evaluation, so listing devices doesn't make sense.
        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.CmdDeviceOptionsRequireProject);
    }

    [TestMethod]
    public void ItCallsDeployToDeviceTargetWhenDeviceIsSpecified()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: "ExplicitDeploy")
            .WithSource();
        string deviceId = "test-device-1";
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId, "-bl");

        result.Should().Pass();
        File.Exists(binlogPath).Should().BeTrue("the test binlog should be created");
        AssertTargetInBinlog(
            binlogPath,
            "DeployToDevice",
            targets =>
            {
                targets.Should().ContainSingle("the selected test target framework should be deployed once");
                var deployMessage = targets.Single().FindChildrenRecursive<Message>()
                    .Single(message => message.Text.Contains("DeployToDevice: Deployed"));
                deployMessage.Text.Should().Contain(deviceId, "the Device property should be passed to DeployToDevice");
            });
    }

    [TestMethod]
    public void ItCallsDeployToDeviceTargetEvenWithNoBuild()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: "NoBuildDeploy")
            .WithSource();
        string deviceId = "test-device-1";
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");

        new DotnetCommand(Log, "build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, $"-p:Device={deviceId}")
            .Should().Pass();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "--device", deviceId, "--no-build", "-bl");

        result.Should().Pass();
        File.Exists(binlogPath).Should().BeTrue("the test binlog should be created");
        AssertTargetInBinlog(
            binlogPath,
            "DeployToDevice",
            targets => targets.Should().ContainSingle("deployment must run even when the build is skipped"));
    }

    [TestMethod]
    public void ItCallsDeployToDeviceTargetWhenDeviceIsAutoSelected()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: "AutoDeploy")
            .WithSource();
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--framework", ToolsetInfo.CurrentTargetFramework, "-p:SingleDevice=true", "-bl");

        result.Should().Pass();
        File.Exists(binlogPath).Should().BeTrue("the test binlog should be created");
        AssertTargetInBinlog(
            binlogPath,
            "DeployToDevice",
            targets =>
            {
                targets.Should().ContainSingle("the selected test target framework should be deployed once");
                var deployMessage = targets.Single().FindChildrenRecursive<Message>()
                    .Single(message => message.Text.Contains("DeployToDevice: Deployed"));
                deployMessage.Text.Should().Contain("single-device", "the auto-selected Device should be deployed");
                deployMessage.Text.Should().Contain(
                    RuntimeInformation.RuntimeIdentifier,
                    "the RuntimeIdentifier supplied by the selected device should be deployed");
            });
    }

    [TestMethod]
    public void ItDeploysBeforeComputingRunArguments()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: "DeployOrder")
            .WithSource();
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute(
                "--framework",
                ToolsetInfo.CurrentTargetFramework,
                "--device",
                "test-device-1",
                "-bl");

        result.Should().Pass();
        File.Exists(Path.Combine(
            testInstance.Path,
            "obj",
            TestingConstants.Debug,
            ToolsetInfo.CurrentTargetFramework,
            "dotnet-test-deploy.marker")).Should().BeTrue();

        var build = BinaryLog.ReadBuild(binlogPath);
        var deployTarget = build.FindChildrenRecursive<StructuredLoggerTarget>(
            target => target.Name == "DeployToDevice").Should().ContainSingle().Which;
        var computeRunArgumentsTarget = build.FindChildrenRecursive<StructuredLoggerTarget>(
            target => target.Name == "ComputeRunArguments").Should().ContainSingle().Which;
        deployTarget.EndTime.Should().BeOnOrBefore(
            computeRunArgumentsTarget.StartTime,
            "deployment must complete before run arguments are computed");
    }

    [TestMethod]
    public void ItFailsWhenDeployToDeviceTargetFails()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: "DeployFailure")
            .WithSource();

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute(
                "--framework",
                ToolsetInfo.CurrentTargetFramework,
                "--device",
                "test-device-1",
                "-p:FailDeployToDevice=true");

        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandDeployFailed);
    }

    [TestMethod]
    public void ItReportsDeployToDeviceFailureGracefullyForSolutionProject()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("TestProjectWithTests", identifier: "SolutionDeployFailure")
            .WithSource();
        string projectPath = Path.Combine(testInstance.Path, "TestProject.csproj");
        string projectContents = File.ReadAllText(projectPath);
        File.WriteAllText(
            projectPath,
            projectContents.Replace(
                "</Project>",
                """
                  <Target Name="DeployToDevice">
                    <Error Text="DeployToDevice failed as requested." />
                  </Target>
                </Project>
                """));
        File.WriteAllText(
            Path.Combine(testInstance.Path, "TestSolution.slnx"),
            """
            <Solution>
              <Project Path="TestProject.csproj" />
            </Solution>
            """);

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
            .Execute("--solution", "TestSolution.slnx");

        result.Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandDeployFailed)
            .And.NotHaveStdErrContaining(nameof(AggregateException));
    }

    [TestMethod]
    public void ItDeploysEveryTargetFramework()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("DotnetTestDevices", identifier: "MultiTargetDeploy")
            .WithSource();
        string binlogPath = Path.Combine(testInstance.Path, "msbuild-dotnet-test.binlog");

        var result = new DotnetTestCommand(Log, disableNewOutput: false)
            .WithWorkingDirectory(testInstance.Path)
            .Execute("-p:SingleDevice=true", "-bl");

        result.Should().Pass();
        File.Exists(binlogPath).Should().BeTrue("the test binlog should be created");
        AssertTargetInBinlog(
            binlogPath,
            "DeployToDevice",
            targets =>
            {
                targets.Should().HaveCount(2, "each target framework should be deployed");
                var messages = targets.SelectMany(target => target.FindChildrenRecursive<Message>());
                messages.Should().Contain(message => message.Text.Contains("net9.0"));
                messages.Should().Contain(message => message.Text.Contains(ToolsetInfo.CurrentTargetFramework));
            });
    }
}
