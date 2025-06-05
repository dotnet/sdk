// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    [Collection(nameof(TestToolBuilderCollection))]
    public class EndToEndToolTests : SdkTest
    {
        private readonly TestToolBuilder ToolBuilder;

        public EndToEndToolTests(ITestOutputHelper log, TestToolBuilder toolBuilder) : base(log)
        {
            ToolBuilder = toolBuilder;
        }

        [Fact]
        public void InstallAndRunToolGlobal()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings();
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);

            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var homeFolder = Path.Combine(testDirectory.Path, "home");

            new DotnetToolCommand(Log, "install", "-g", toolSettings.ToolPackageId, "--add-source", toolPackagesPath)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass();

            var toolsFolder = Path.Combine(homeFolder, ".dotnet", "tools");

            var shimPath = Path.Combine(toolsFolder, toolSettings.ToolCommandName + EnvironmentInfo.ExecutableExtension);
            new FileInfo(shimPath).Should().Exist();

            new RunExeCommand(Log, shimPath)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello Tool!");
        }

        [Fact]
        public void InstallAndRunNativeAotGlobalTool()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                NativeAOT = true
            };
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);

            var testDirectory = _testAssetsManager.CreateTestDirectory();

            var homeFolder = Path.Combine(testDirectory.Path, "home");

            new DotnetToolCommand(Log, "install", "-g", toolSettings.ToolPackageId, "--add-source", toolPackagesPath)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass();

            var toolsFolder = Path.Combine(homeFolder, ".dotnet", "tools");

            var shimPath = Path.Combine(toolsFolder, toolSettings.ToolCommandName + (OperatingSystem.IsWindows() ? ".cmd" : ""));
            new FileInfo(shimPath).Should().Exist();

            new RunExeCommand(Log, shimPath)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello Tool!");
        }

        [Fact]
        public void InstallAndRunToolLocal()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings();
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);

            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var homeFolder = Path.Combine(testDirectory.Path, "home");

            new DotnetCommand(Log, "new", "tool-manifest")
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass();

            new DotnetToolCommand(Log, "install", toolSettings.ToolPackageId, "--add-source", toolPackagesPath)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, toolSettings.ToolCommandName)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello Tool!");
        }

        [Fact]
        public void InstallAndRunNativeAotLocalTool()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                NativeAOT = true
            };
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);

            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var homeFolder = Path.Combine(testDirectory.Path, "home");

            new DotnetCommand(Log, "new", "tool-manifest")
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass();

            new DotnetToolCommand(Log, "install", toolSettings.ToolPackageId, "--add-source", toolPackagesPath)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, toolSettings.ToolCommandName)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello Tool!");
        }
    }

    static class EndToEndToolTestExtensions
    {
        public static TestCommand WithEnvironmentVariables(this TestCommand command, string homeFolder)
        {
            return command.WithEnvironmentVariable("DOTNET_CLI_HOME", homeFolder)
                          .WithEnvironmentVariable("DOTNET_NOLOGO", "1")
                          .WithEnvironmentVariable("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", "0");
        }
    }
}
