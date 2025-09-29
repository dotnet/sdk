// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Package.Add.Tests
{
    public class GivenDotnetPackageAdd : SdkTest
    {
        public GivenDotnetPackageAdd(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void WhenValidPackageIsPassedBeforeVersionItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName, "--version", packageVersion);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
        }

        public static readonly TheoryData<string[], string?, string> PackageVersionsTheoryData = new()
        {
            { ["0.0.5", "0.9.0", "1.0.0-preview.3"], "0.9.0", "1.0.0-preview.3" },
            { ["0.0.5", "0.9.0", "1.0.0-preview.3", "1.1.1-preview.7"], "0.9.0", "1.1.1-preview.7" },
            { ["0.0.5", "0.9.0", "1.0.0"], "1.0.0", "1.0.0" },
            { ["0.0.5", "0.9.0", "1.0.0-preview.3", "2.0.0"], "2.0.0", "2.0.0" },
            { ["1.0.0-preview.1", "1.0.0-preview.2", "1.0.0-preview.3"], null, "1.0.0-preview.3" },
        };

        [Theory]
        [MemberData(nameof(PackageVersionsTheoryData))]
        public void WhenPrereleaseOptionIsPassed(string[] inputVersions, string? _, string expectedVersion)
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = targetFramework,
            };

            var packages = inputVersions.Select(e => GetPackagePath(targetFramework, "A", e, identifier: expectedVersion + e + inputVersions.GetHashCode().ToString())).ToArray();

            // disable implicit use of the Roslyn Toolset compiler package
            testProject.AdditionalProperties["BuildWithNetFrameworkHostedCompiler"] = false.ToString();
            testProject.AdditionalProperties.Add("RestoreSources",
                                     "$(RestoreSources);" + string.Join(";", packages.Select(package => Path.GetDirectoryName(package))));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: inputVersions.GetHashCode().ToString());

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("add", "package", "--prerelease", "A")
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package 'A' version '{expectedVersion}' ")
                .And.NotHaveStdErr();
        }

        [Theory]
        [MemberData(nameof(PackageVersionsTheoryData))]
        public void WhenNoVersionIsPassed(string[] inputVersions, string? expectedVersion, string prereleaseVersion)
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            TestProject testProject = new()
            {
                Name = "Project",
                IsExe = false,
                TargetFrameworks = targetFramework,
            };

            var packages = inputVersions.Select(e => GetPackagePath(targetFramework, "A", e, identifier: expectedVersion + e + inputVersions.GetHashCode().ToString())).ToArray();

            // disable implicit use of the Roslyn Toolset compiler package
            testProject.AdditionalProperties["BuildWithNetFrameworkHostedCompiler"] = false.ToString();
            testProject.AdditionalProperties.Add("RestoreSources",
                                     "$(RestoreSources);" + string.Join(";", packages.Select(package => Path.GetDirectoryName(package))));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: inputVersions.GetHashCode().ToString());

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("add", "package", "A");

            if (expectedVersion is null)
            {
                cmd.Should().Fail()
                    .And.HaveStdOutContaining($"There are no stable versions available, {prereleaseVersion} is the best available. Consider adding the --prerelease option");
            }
            else
            {
                cmd.Should().Pass()
                    .And.HaveStdOutContaining($"PackageReference for package 'A' version '{expectedVersion}' ")
                    .And.NotHaveStdErr();
            }
        }

        [Fact]
        public void WhenPrereleaseAndVersionOptionIsPassedFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", "--prerelease", "Newtonsoft.Json", "--version", ToolsetInfo.GetNewtonsoftJsonPackageVersion())
                .Should().Fail()
                .And.HaveStdOutContaining("The --prerelease and --version options are not supported in the same command.");
        }

        [Fact]
        public void
            WhenValidProjectAndPackageArePassedItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var csproj = $"{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj";
            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", csproj, "package", packageName, "--version", packageVersion)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package \'{packageName}\' version \'{packageVersion}\' added to file '{csproj}'.")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void
            WhenValidProjectAndPackageWithPackageDirectoryContainingSpaceArePassedItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageDirectory = Path.Combine(projectDirectory, "local packages");

            var csproj = $"{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj";
            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", csproj, "package", packageName, "--version", packageVersion, "--package-directory", packageDirectory)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package \'{packageName}\' version \'{packageVersion}\' added to file '{csproj}'.")
                .And.NotHaveStdErr();

            var restoredPackageDirectory = Path.Combine(packageDirectory, packageName.ToLowerInvariant(), packageVersion);
            var packageDirectoryExists = Directory.Exists(restoredPackageDirectory);
            Assert.True(packageDirectoryExists);
        }

        [Fact]
        public void WhenValidPackageIsPassedAfterVersionItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", "--version", packageVersion, packageName)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenValidPackageIsPassedWithFrameworkItGetsAdded()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var framework = ToolsetInfo.CurrentTargetFramework;
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", packageName, "--version", packageVersion, "--framework", framework)
                .Should()
                .Pass()
                .And.HaveStdOutContaining($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenValidPackageIsPassedMSBuildDoesNotPrintVersionHeader()
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion();
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package", packageName, "--version", packageVersion)
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("Microsoft (R) Build Engine version")
                .And.NotHaveStdErr();
        }

        [Fact]
        public void WhenMultiplePackagesArePassedCommandFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", "package1", "package2", "package3")
                .Should()
                .Fail();
        }

        [Fact]
        public void WhenNoPackageisPassedCommandFails()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"add", "package")
                .Should()
                .Fail();
        }

        [Theory, CombinatorialData]
        public void VersionRange(bool asArgument)
        {
            var testAsset = "TestAppSimple";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Humanizer";
            var packageVersion = "2.*";
            string[] args = asArgument
                ? ["add", "package", $"{packageName}@{packageVersion}"]
                : ["add", "package", packageName, "--version", packageVersion];
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(args);
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain($"PackageReference for package '{packageName}' version '{packageVersion}' " +
                $"added to file '{projectDirectory + Path.DirectorySeparatorChar + testAsset}.csproj'.");
            cmd.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void FileBasedApp()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "add", "Humanizer@2.14.1", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Humanizer@2.14.1

                Console.WriteLine();
                """);
        }

        [Theory]
        [InlineData("Humanizer")]
        [InlineData("humanizer")]
        public void FileBasedApp_ReplaceExisting(
            string sourceFilePackageId)
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, $"""
                #:package {sourceFilePackageId}@2.9.9
                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "add", "Humanizer@2.14.1", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                #:package Humanizer@2.14.1
                Console.WriteLine();
                """);
        }

        [Theory, MemberData(nameof(PackageVersionsTheoryData))]
        public void FileBasedApp_NoVersion(string[] inputVersions, string? expectedVersion, string _)
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();

            var packages = inputVersions.Select(e => GetPackagePath(ToolsetInfo.CurrentTargetFramework, "A", e, identifier: expectedVersion + e + inputVersions.GetHashCode().ToString())).ToArray();

            var restoreSources = string.Join(";", packages.Select(package => Path.GetDirectoryName(package)));

            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = $"""
                #:property RestoreSources=$(RestoreSources);{restoreSources}
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            var cmd = new DotnetCommand(Log, "package", "add", "A", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            if (expectedVersion is null)
            {
                cmd.Should().Fail();

                File.ReadAllText(file).Should().Be(source);
            }
            else
            {
                cmd.Should().Pass();

                File.ReadAllText(file).Should().Be($"""
                    #:package A@{expectedVersion}
                    {source}
                    """);
            }
        }

        [Theory, MemberData(nameof(PackageVersionsTheoryData))]
        public void FileBasedApp_NoVersion_Prerelease(string[] inputVersions, string? _, string expectedVersion)
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();

            var packages = inputVersions.Select(e => GetPackagePath(ToolsetInfo.CurrentTargetFramework, "A", e, identifier: expectedVersion + e + inputVersions.GetHashCode().ToString())).ToArray();

            var restoreSources = string.Join(";", packages.Select(package => Path.GetDirectoryName(package)));

            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = $"""
                #:property RestoreSources=$(RestoreSources);{restoreSources}
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            var cmd = new DotnetCommand(Log, "package", "add", "A", "--prerelease", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                    #:package A@{expectedVersion}
                    {source}
                    """);
        }

        [Fact]
        public void FileBasedApp_NoVersionAndNoRestore()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "add", "Humanizer", "--file", "Program.cs", "--no-restore")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Humanizer@*

                Console.WriteLine();
                """);
        }

        [Fact]
        public void FileBasedApp_VersionAndPrerelease()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            new DotnetCommand(Log, "package", "add", "Humanizer@2.14.1", "--file", "Program.cs", "--prerelease")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(CliCommandStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime);

            File.ReadAllText(file).Should().Be(source);
        }

        [Fact]
        public void FileBasedApp_InvalidPackage()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            new DotnetCommand(Log, "package", "add", "Microsoft.ThisPackageDoesNotExist", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail();

            File.ReadAllText(file).Should().Be(source);
        }

        [Fact]
        public void FileBasedApp_InvalidPackage_NoRestore()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "add", "Microsoft.ThisPackageDoesNotExist", "--file", "Program.cs", "--no-restore")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Microsoft.ThisPackageDoesNotExist@*

                Console.WriteLine();
                """);
        }

        [Fact]
        public void FileBasedApp_CentralPackageManagement()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            var directoryPackagesProps = Path.Join(testInstance.Path, "Directory.Packages.props");
            File.WriteAllText(directoryPackagesProps, """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                </Project>
                """);

            new DotnetCommand(Log, "package", "add", "Humanizer@2.14.1", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                #:package Humanizer

                {source}
                """);

            File.ReadAllText(directoryPackagesProps).Should().Be("""
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Humanizer" Version="2.14.1" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_CentralPackageManagement_ReplaceExisting(bool wasInFile)
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;

            if (wasInFile)
            {
                source = $"""
                    #:package Humanizer@2.9.9

                    {source}
                    """;
            }

            File.WriteAllText(file, source);

            var directoryPackagesProps = Path.Join(testInstance.Path, "Directory.Packages.props");
            File.WriteAllText(directoryPackagesProps, """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Humanizer" Version="2.9.9" />
                  </ItemGroup>
                </Project>
                """);

            new DotnetCommand(Log, "package", "add", "Humanizer@2.14.1", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Humanizer

                Console.WriteLine();
                """);

            File.ReadAllText(directoryPackagesProps).Should().Be("""
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Humanizer" Version="2.14.1" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public void FileBasedApp_CentralPackageManagement_NoVersionSpecified()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();

            string[] versions = ["0.0.5", "0.9.0", "1.0.0-preview.3"];
            var packages = versions.Select(e => GetPackagePath(ToolsetInfo.CurrentTargetFramework, "A", e, identifier: e + versions.GetHashCode().ToString())).ToArray();

            var restoreSources = string.Join(";", packages.Select(package => Path.GetDirectoryName(package)));

            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = $"""
                #:property RestoreSources=$(RestoreSources);{restoreSources}
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            var directoryPackagesProps = Path.Join(testInstance.Path, "Directory.Packages.props");
            File.WriteAllText(directoryPackagesProps, """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                </Project>
                """);

            new DotnetCommand(Log, "package", "add", "A", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                #:package A
                {source}
                """);

            File.ReadAllText(directoryPackagesProps).Should().Be("""
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="A" Version="0.9.0" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public void FileBasedApp_CentralPackageManagement_NoVersionSpecified_KeepExisting()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                #:package Humanizer
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            var directoryPackagesProps = Path.Join(testInstance.Path, "Directory.Packages.props");
            var directoryPackagesPropsSource = """
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Humanizer" Version="2.9.9" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(directoryPackagesProps, directoryPackagesPropsSource);

            new DotnetCommand(Log, "package", "add", "Humanizer", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be(source);

            File.ReadAllText(directoryPackagesProps).Should().Be(directoryPackagesPropsSource);
        }

        private static TestProject GetProject(string targetFramework, string referenceProjectName, string version)
        {
            var project = new TestProject()
            {
                Name = referenceProjectName,
                TargetFrameworks = targetFramework,
            };
            project.AdditionalProperties.Add("Version", version);
            return project;
        }

        private string GetPackagePath(string targetFramework, string packageName, string version, [CallerMemberName] string callingMethod = "", string? identifier = null)
        {
            var project = GetProject(targetFramework, packageName, version);
            var packCommand = new PackCommand(_testAssetsManager.CreateTestProject(project, callingMethod: callingMethod, identifier: identifier));

            packCommand
                .Execute()
                .Should()
                .Pass();
            return packCommand.GetNuGetPackage(packageName, packageVersion: version);
        }
    }
}

