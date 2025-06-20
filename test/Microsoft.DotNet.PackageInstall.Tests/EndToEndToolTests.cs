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
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings, collectBinlogs: true);

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
                RidSpecific = true
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
                package.Should()
                    .NotBeNull($"Package {packageName} should be present in the tool packages directory")
                    .And.Satisfy<string>(EnsurePackageIsAnExecutable);
            }

            // top-level package should declare all of the rids
            var topLevelPackage = packages.First(p => p.EndsWith($"{packageIdentifier}.{toolSettings.ToolPackageVersion}.nupkg"));
            var foundRids = GetRidsInSettingsFile(topLevelPackage);
            foundRids.Should().BeEquivalentTo(expectedRids, "The top-level package should declare all of the RIDs for the tools it contains");
        }

        [Fact]
        public void PackagesMultipleTrimmedToolsWithASingleInvocation()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                Trimmed = true
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
                package.Should()
                    .NotBeNull($"Package {packageName} should be present in the tool packages directory")
                    .And.Satisfy<string>(EnsurePackageIsAnExecutable)
                    .And.Satisfy((string package) => EnsurePackageLacksTrimmedDependency(package, "System.Xml.dll"));
            }

            // top-level package should declare all of the rids
            var topLevelPackage = packages.First(p => p.EndsWith($"{packageIdentifier}.{toolSettings.ToolPackageVersion}.nupkg"));
            var foundRids = GetRidsInSettingsFile(topLevelPackage);
            foundRids.Should().BeEquivalentTo(expectedRids, "The top-level package should declare all of the RIDs for the tools it contains");
        }

        [Fact]
        public void PackagesFrameworkDependentRidSpecificPackagesCorrectly()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                RidSpecific = true,
            };
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings, collectBinlogs: true);

            var packages = Directory.GetFiles(toolPackagesPath, "*.nupkg");
            var packageIdentifier = toolSettings.ToolPackageId;
            var expectedRids = ToolsetInfo.LatestRuntimeIdentifiers.Split(';');

            packages.Length.Should().Be(expectedRids.Length + 1, "There should be one package for the tool-wrapper and one for each RID");
            foreach (string rid in expectedRids)
            {
                var packageName = $"{toolSettings.ToolPackageId}.{rid}.{toolSettings.ToolPackageVersion}";
                var package = packages.FirstOrDefault(p => p.EndsWith(packageName + ".nupkg"));
                package.Should()
                    .NotBeNull($"Package {packageName} should be present in the tool packages directory")
                    .And.Satisfy<string>(EnsurePackageIsAnExecutable);
            }

            // top-level package should declare all of the rids
            var topLevelPackage = packages.First(p => p.EndsWith($"{packageIdentifier}.{toolSettings.ToolPackageVersion}.nupkg"));
            var foundRids = GetRidsInSettingsFile(topLevelPackage);
            foundRids.Should().BeEquivalentTo(expectedRids, "The top-level package should declare all of the RIDs for the tools it contains");
        }

        [Fact]
        public void PackageToolWithAnyRid()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                RidSpecific = true,
                IncludeAnyRid = true
            };

            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);

            var packages = Directory.GetFiles(toolPackagesPath, "*.nupkg");
            var packageIdentifier = toolSettings.ToolPackageId;
            var expectedRids = ToolsetInfo.LatestRuntimeIdentifiers.Split(';');

            packages.Length.Should().Be(expectedRids.Length + 1 + 1, "There should be one package for the tool-wrapper, one for the top-level manifest, and one for each RID");
            foreach (string rid in expectedRids)
            {
                var packageName = $"{toolSettings.ToolPackageId}.{rid}.{toolSettings.ToolPackageVersion}";
                var package = packages.FirstOrDefault(p => p.EndsWith(packageName + ".nupkg"));
                package.Should().NotBeNull($"Package {packageName} should be present in the tool packages directory")
                        .And.Satisfy<string>(EnsurePackageIsAnExecutable);
            }

            // Ensure that the package with the "any" RID is present
            var anyRidPackage = packages.FirstOrDefault(p => p.EndsWith($"{packageIdentifier}.any.{toolSettings.ToolPackageVersion}.nupkg"));
            anyRidPackage.Should().NotBeNull($"Package {packageIdentifier}.any.{toolSettings.ToolPackageVersion}.nupkg should be present in the tool packages directory")
                .And.Satisfy<string>(EnsurePackageIsFdd);

            // top-level package should declare all of the rids
            var topLevelPackage = packages.First(p => p.EndsWith($"{packageIdentifier}.{toolSettings.ToolPackageVersion}.nupkg"));
            var settingsXml = GetToolSettingsFile(topLevelPackage);
            var packageNodes = GetRidsInSettingsFile(settingsXml);

            packageNodes.Should().BeEquivalentTo([.. expectedRids, "any"], "The top-level package should declare all of the RIDs for the tools it contains");
        }

        [Fact]
        public void InstallAndRunToolFromAnyRid()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                IncludeAnyRid = true // will make one package with the "any" RID
            };
            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);
            var packages = Directory.GetFiles(toolPackagesPath, "*.nupkg").Select(p => Path.GetFileName(p)).ToArray();
            packages.Should().BeEquivalentTo([
                $"{toolSettings.ToolPackageId}.{toolSettings.ToolPackageVersion}.nupkg",
                $"{toolSettings.ToolPackageId}.any.{toolSettings.ToolPackageVersion}.nupkg"
                ], "There should be two packages: one for the tool-wrapper and one for the 'any' RID");
            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var homeFolder = Path.Combine(testDirectory.Path, "home");

            new DotnetToolCommand(Log, "exec", toolSettings.ToolPackageId, "--yes", "--add-source", toolPackagesPath)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello Tool!");
        }

        [Fact]
        public void InstallAndRunToolFromAnyRidWhenOtherRidsArePresentButIncompatible()
        {
            var toolSettings = new TestToolBuilder.TestToolSettings()
            {
                IncludeCurrentRid = false,
                RidSpecific = true, // will make one package for each RID except the current RID
                IncludeAnyRid = true // will make one package with the "any" RID
            };
            List<string> expectedRids = [ .. ToolsetInfo.LatestRuntimeIdentifiers.Split(';').Where(rid => rid != RuntimeInformation.RuntimeIdentifier), "any"];

            string toolPackagesPath = ToolBuilder.CreateTestTool(Log, toolSettings);
            var packages = Directory.GetFiles(toolPackagesPath, "*.nupkg").Select(p => Path.GetFileName(p)).ToArray();
            packages.Should().BeEquivalentTo([
                $"{toolSettings.ToolPackageId}.{toolSettings.ToolPackageVersion}.nupkg",
                .. expectedRids.Select(rid => $"{toolSettings.ToolPackageId}.{rid}.{toolSettings.ToolPackageVersion}.nupkg"),
                ], $"There should be { 1 + expectedRids.Count } packages: one for the tool-wrapper and one for each RID except the current RID");
            var testDirectory = _testAssetsManager.CreateTestDirectory();
            var homeFolder = Path.Combine(testDirectory.Path, "home");

            new DotnetToolCommand(Log, "exec", toolSettings.ToolPackageId, "--yes", "--add-source", toolPackagesPath)
                .WithEnvironmentVariables(homeFolder)
                .WithWorkingDirectory(testDirectory.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello Tool!");
        }

        private void EnsurePackageIsFdd(string packagePath)
        {
            var settingsXml = GetToolSettingsFile(packagePath);
            var runner = GetRunnerFromSettingsFile(settingsXml);
            runner.Should().Be("dotnet", "The tool should be packaged as a framework-dependent executable (FDD) with a 'dotnet' runner.");
        }

        private void EnsurePackageIsAnExecutable(string packagePath)
        {
            var settingsXml = GetToolSettingsFile(packagePath);
            var runner = GetRunnerFromSettingsFile(settingsXml);
            runner.Should().Be("executable", "The tool should be packaged as a executable with an 'executable' runner.");
        }

        private object GetRunnerFromSettingsFile(XElement settingsXml)
        {
            return settingsXml.Elements("Commands").First().Elements("Command").First().Attribute("Runner")?.Value
                   ?? throw new InvalidOperationException("The tool settings file does not contain a 'Runner' attribute.");
        }

        private string[] GetRidsInSettingsFile(string packagePath)
        {
            var settingsXml = GetToolSettingsFile(packagePath);
            var rids = GetRidsInSettingsFile(settingsXml);
            rids.Should().NotBeEmpty("The tool settings file should contain at least one RuntimeIdentifierPackage element.");
            return rids;
        }

        private string[] GetRidsInSettingsFile(XElement settingsXml)
        {
            var nodes = (settingsXml.Nodes()
                    .First(n => n is XElement e && e.Name == "RuntimeIdentifierPackages") as XElement)!.Nodes()
                    .Where(n => (n as XElement)!.Name == "RuntimeIdentifierPackage")
                    .Select(e => (e as XElement)!.Attributes().First(a => a.Name == "RuntimeIdentifier").Value)
                    .ToArray();
            return nodes;
        }

        private XElement GetToolSettingsFile(string packagePath)
        {
            using var zipArchive = ZipFile.OpenRead(packagePath);
            var nuspecEntry = zipArchive.Entries.First(e => e.Name == "DotnetToolSettings.xml")!;
            var stream = nuspecEntry.Open();
            var xml = XDocument.Load(stream, LoadOptions.None);
            return xml.Root!;

        }

        /// <summary>
        /// Opens the nupkg and verifies that it does not contain a dependency on the given dll.
        /// </summary>
        private void EnsurePackageLacksTrimmedDependency(string packagePath, string dll)
        {
            using var zipArchive = ZipFile.OpenRead(packagePath);
            zipArchive.Entries.Should().NotContain(
                e => e.FullName.EndsWith(dll, StringComparison.OrdinalIgnoreCase),
                $"The package {Path.GetFileName(packagePath)} should not contain a dependency on {dll}.");
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
