// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;

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


        [Fact]
        public void PackagesMultipleToolsWithASingleInvocation()
        {

            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                SelfContained = true
            };
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);

            var packages = Directory.GetFiles(toolPackagesPath, "*.nupkg");
            var packageIdentifier = toolSettings.ToolPackageId;
            var expectedRids = ToolsetInfo.LatestRuntimeIdentifiers.Split(';');

            packages.Length.Should().Be(expectedRids.Length + 1, "There should be one package for the tool-wrapper and one for each RID");
            foreach (string rid in expectedRids)
            {
                var packageName = $"{toolSettings.ToolPackageId}.{rid}.{toolSettings.ToolPackageVersion}";
                var package = packages.FirstOrDefault(p => p.EndsWith(packageName + ".nupkg"));
                packages.Should().NotBeNull($"Package {packageName} should be present in the tool packages directory");
            }

            // top-level package should declare all of the rids
            var topLevelPackage = packages.First(p => p.EndsWith($"{packageIdentifier}.{toolSettings.ToolPackageVersion}.nupkg"));
            using var zipArchive = ZipFile.OpenRead(topLevelPackage);
            var nuspecEntry = zipArchive.GetEntry($"tools/{ToolsetInfo.CurrentTargetFramework}/any/DotnetToolSettings.xml")!;
            var stream = nuspecEntry.Open();
            var xml = XDocument.Load(stream, LoadOptions.None);
            var packageNodes =
                (xml.Root!.Nodes()
                    .First(n => n is XElement e && e.Name == "RuntimeIdentifierPackages") as XElement)!.Nodes()
                    .Where(n => (n as XElement)!.Name == "RuntimeIdentifierPackage")
                    .Select(e => (e as XElement)!.Attributes().First(a => a.Name == "RuntimeIdentifier").Value);
            packageNodes.Should().BeEquivalentTo(expectedRids, "The top-level package should declare all of the RIDs for the tools it contains");
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
