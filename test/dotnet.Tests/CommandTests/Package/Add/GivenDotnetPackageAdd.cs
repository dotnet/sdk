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
            var projectDirectory = TestAssetsManager
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

            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: inputVersions.GetHashCode().ToString());

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

            var testAsset = TestAssetsManager.CreateTestProject(testProject, identifier: inputVersions.GetHashCode().ToString());

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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
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
            var projectDirectory = TestAssetsManager
                .CopyTestAsset(testAsset)
                .WithSource()
                .Path;

            var packageName = "Newtonsoft.Json";
            var packageVersion = ToolsetInfo.GetNewtonsoftJsonPackageVersion().Split('.')[0] + ".*";
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

        private string[]? GetFileBasedAppArgs(bool legacyForm, bool? versionOption, bool fileOption, bool noRestore, string packageName = "Newtonsoft.Json", string? packageVersion = null)
        {
            if (!legacyForm && !fileOption)
            {
                Log.WriteLine("Skipping invalid combination of parameters");
                return null;
            }

            (string, string) commandArgs = legacyForm
                ? ("add", "package")
                : ("package", "add");

            packageVersion ??= ToolsetInfo.GetNewtonsoftJsonPackageVersion();

            return [
                commandArgs.Item1,
                .. (ReadOnlySpan<string>)(fileOption ? [] : ["Program.cs"]),
                commandArgs.Item2,
                .. (ReadOnlySpan<string>)(versionOption switch
                {
                    true => [packageName, "--version", packageVersion],
                    false => [$"{packageName}@{packageVersion}"],
                    null => [packageName],
                }),
                .. (ReadOnlySpan<string>)(fileOption ? ["--file", "Program.cs"] : []),
                .. (ReadOnlySpan<string>)(noRestore ? ["--no-restore"] : []),
            ];
        }

        [Theory, CombinatorialData]
        public void FileBasedApp(bool legacyForm, bool versionOption, bool fileOption, bool noRestore)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption, fileOption, noRestore) is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                #:package Newtonsoft.Json@{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}

                Console.WriteLine();
                """);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_ReplaceExisting(
            [CombinatorialValues("Newtonsoft.Json", "newtonsoft.json")] string sourceFilePackageId,
            bool legacyForm, bool versionOption, bool fileOption, bool noRestore)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption, fileOption, noRestore) is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, $"""
                #:package {sourceFilePackageId}@13.0.1
                Console.WriteLine();
                """);

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                #:package Newtonsoft.Json@{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}
                Console.WriteLine();
                """);
        }

        [Theory, MemberData(nameof(PackageVersionsTheoryData))]
        public void FileBasedApp_NoVersion(string[] inputVersions, string? expectedVersion, string _)
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();

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
            var testInstance = TestAssetsManager.CreateTestDirectory();

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

        [Theory, CombinatorialData]
        public void FileBasedApp_NoVersionAndNoRestore(bool legacyForm, bool fileOption)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption: null, fileOption, noRestore: true) is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Newtonsoft.Json@*

                Console.WriteLine();
                """);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_VersionAndPrerelease(bool legacyForm, bool versionOption, bool fileOption, bool noRestore)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption, fileOption, noRestore) is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            new DotnetCommand(Log, [.. args, "--prerelease"])
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(CliCommandStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime);

            File.ReadAllText(file).Should().Be(source);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_InvalidPackage(bool legacyForm, bool fileOption)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption: null, fileOption, noRestore: false, packageName: "Microsoft.ThisPackageDoesNotExist") is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;
            File.WriteAllText(file, source);

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail();

            File.ReadAllText(file).Should().Be(source);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_InvalidPackage_NoRestore(bool legacyForm, bool fileOption)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption: null, fileOption, noRestore: true, packageName: "Microsoft.ThisPackageDoesNotExist") is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Microsoft.ThisPackageDoesNotExist@*

                Console.WriteLine();
                """);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_CentralPackageManagement(bool legacyForm, bool versionOption, bool fileOption, bool noRestore)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption, fileOption, noRestore) is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
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

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be($"""
                #:package Newtonsoft.Json

                {source}
                """);

            File.ReadAllText(directoryPackagesProps).Should().Be($"""
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Newtonsoft.Json" Version="{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_CentralPackageManagement_ReplaceExisting(bool wasInFile, bool legacyForm, bool versionOption, bool fileOption, bool noRestore)
        {
            const string OlderVersion = "13.0.1";

            if (GetFileBasedAppArgs(legacyForm, versionOption, fileOption, noRestore) is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = """
                Console.WriteLine();
                """;

            if (wasInFile)
            {
                source = $"""
                    #:package Newtonsoft.Json@{OlderVersion}

                    {source}
                    """;
            }

            File.WriteAllText(file, source);

            var directoryPackagesProps = Path.Join(testInstance.Path, "Directory.Packages.props");
            File.WriteAllText(directoryPackagesProps, $"""
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Newtonsoft.Json" Version="{OlderVersion}" />
                  </ItemGroup>
                </Project>
                """);

            new DotnetCommand(Log, args)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            File.ReadAllText(file).Should().Be("""
                #:package Newtonsoft.Json

                Console.WriteLine();
                """);

            File.ReadAllText(directoryPackagesProps).Should().Be($"""
                <Project>
                  <PropertyGroup>
                    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageVersion Include="Newtonsoft.Json" Version="{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Theory, CombinatorialData]
        public void FileBasedApp_CentralPackageManagement_NoVersionSpecified(bool legacyForm, bool fileOption)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption: null, fileOption, noRestore: false, packageName: "A") is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();

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

            new DotnetCommand(Log, args)
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

        [Theory, CombinatorialData]
        public void FileBasedApp_CentralPackageManagement_NoVersionSpecified_KeepExisting(bool legacyForm, bool fileOption, bool noRestore)
        {
            if (GetFileBasedAppArgs(legacyForm, versionOption: null, fileOption, noRestore, packageName: "A") is not { } args) return;

            var testInstance = TestAssetsManager.CreateTestDirectory();

            string[] versions = ["0.0.5", "0.9.0", "1.0.0-preview.3"];
            var packages = versions.Select(e => GetPackagePath(ToolsetInfo.CurrentTargetFramework, "A", e, identifier: e + versions.GetHashCode().ToString())).ToArray();

            var restoreSources = string.Join(";", packages.Select(package => Path.GetDirectoryName(package)));

            var file = Path.Join(testInstance.Path, "Program.cs");
            var source = $"""
                #:property RestoreSources=$(RestoreSources);{restoreSources}
                #:package A
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
                    <PackageVersion Include="A" Version="0.0.5" />
                  </ItemGroup>
                </Project>
                """;
            File.WriteAllText(directoryPackagesProps, directoryPackagesPropsSource);

            new DotnetCommand(Log, args)
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
            var packCommand = new PackCommand(TestAssetsManager.CreateTestProject(project, callingMethod: callingMethod, identifier: identifier));

            packCommand
                .Execute()
                .Should()
                .Pass();
            return packCommand.GetNuGetPackage(packageName, packageVersion: version);
        }
    }
}

