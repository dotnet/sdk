// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Restore.Test
{
    public class GivenThatIWantToRestoreApp : SdkTest
    {
        public GivenThatIWantToRestoreApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CreateTestDirectory(identifier: useStaticGraphEvaluation.ToString()).Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            var sln = "TestAppWithSlnAndSolutionFolders";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(sln, identifier: useStaticGraphEvaluation.ToString())
                .WithSource()
                .Path;

            string[] args = new[] { "App.sln", "--packages", fullPath };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(projectDirectory)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true, ".csproj")]
        [InlineData(false, ".csproj")]
        [InlineData(true, ".fsproj")]
        [InlineData(false, ".fsproj")]
        public void ItRestoresLibToSpecificDirectory(bool useStaticGraphEvaluation, string extension)
        {
            var testProject = new TestProject()
            {
                Name = "RestoreToDir",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                TargetExtension = extension,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            if (extension == ".fsproj")
            {
                testProject.PackageReferences.Add(new TestPackageReference("FSharp.Core", "6.0.1", updatePackageReference: true));
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: useStaticGraphEvaluation.ToString() + extension);

            var rootPath = Path.Combine(testAsset.TestRoot, testProject.Name);

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            var dllCount = 0;

            if (Directory.Exists(fullPath))
            {
                dllCount = Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count();
            }

            if (dllCount == 0)
            {
                Log.WriteLine("Assets file contents:");
                Log.WriteLine(File.ReadAllText(Path.Combine(rootPath, "obj", "project.assets.json")));
            }

            Directory.Exists(fullPath).Should().BeTrue();
            dllCount.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresTestAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CopyTestAsset("VSTestCore", identifier: useStaticGraphEvaluation.ToString())
                .WithSource()
                .WithVersionVariables()
                .Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresWithTheSpecifiedVerbosity(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CreateTestDirectory(identifier: useStaticGraphEvaluation.ToString()).Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] newArgs = new[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string[] args = new[] { "--packages", dir, "--verbosity", "quiet" };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(rootPath)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr()
                 .And.NotHaveStdOut();
        }

        [Fact]
        public void ItAcceptsArgumentsAfterProperties()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string[] newArgs = new[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string[] args = new[] { "/p:prop1=true", "/m:1" };
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(rootPath)
                 .Execute(args)
                 .Should()
                 .Pass();
        }
        
        /// <summary>
        /// Tests for RID-specific restore options: -r/--runtime, --os, and -a/--arch
        /// </summary>
        [Theory]
        [InlineData("-r", "linux-x64")]
        [InlineData("--runtime", "win-x64")]
        [InlineData("--os", "linux")]
        [InlineData("-a", "arm64")]
        [InlineData("--arch", "x64")]
        [InlineData("--os", "linux", "-a", "arm64")]
        public void ItRestoresWithRidSpecificOptions(params string[] ridOptions)
        {
            // Skip test for #24251
            var testProject = new TestProject()
            {
                Name = "RestoreWithRidOptions",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: string.Join("_", ridOptions));
            
            var rootPath = Path.Combine(testAsset.TestRoot, testProject.Name);

            // Create the command with the RID-specific options
            var restoreCommand = new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(ridOptions);

            // Verify that the command runs successfully
            restoreCommand.Should().Pass();
            
            // Verify that assets file was created
            var assetsFilePath = Path.Combine(rootPath, "obj", "project.assets.json");
            File.Exists(assetsFilePath).Should().BeTrue();

            // Verify that the assets file contains the expected RID-specific target
            var assetsContents = JObject.Parse(File.ReadAllText(assetsFilePath));
            var targets = assetsContents["targets"];
            targets.Should().NotBeNull("assets file should contain targets section");
            
            // Determine the expected RID based on the options provided
            string expectedRid = GetExpectedRid(ridOptions);
            string expectedTarget = $"{ToolsetInfo.CurrentTargetFramework}/{expectedRid}";
            
            // Check that the specific target exists
            var specificTarget = targets[expectedTarget];
            specificTarget.Should().NotBeNull($"assets file should contain target '{expectedTarget}' when using RID options: {string.Join(" ", ridOptions)}");
        }

        private static string GetExpectedRid(string[] ridOptions)
        {
            // Check if explicit runtime is provided
            for (int i = 0; i < ridOptions.Length; i++)
            {
                if ((ridOptions[i] == "-r" || ridOptions[i] == "--runtime") && i + 1 < ridOptions.Length)
                {
                    return ridOptions[i + 1];
                }
            }

            // Get current platform defaults
            string currentOs = GetCurrentOsPart();
            string currentArch = GetCurrentArchPart();

            // Check for --os and --arch options to synthesize RID
            string targetOs = currentOs;
            string targetArch = currentArch;

            for (int i = 0; i < ridOptions.Length; i++)
            {
                if (ridOptions[i] == "--os" && i + 1 < ridOptions.Length)
                {
                    targetOs = ridOptions[i + 1];
                }
                else if ((ridOptions[i] == "-a" || ridOptions[i] == "--arch") && i + 1 < ridOptions.Length)
                {
                    targetArch = ridOptions[i + 1];
                }
            }

            return $"{targetOs}-{targetArch}";
        }

        private static string GetCurrentOsPart()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "osx";
            else
                throw new PlatformNotSupportedException("Unsupported platform for RID determination");
        }

        private static string GetCurrentArchPart()
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => throw new PlatformNotSupportedException($"Unsupported architecture: {RuntimeInformation.OSArchitecture}")
            };
        }

        private static string[] HandleStaticGraphEvaluation(bool useStaticGraphEvaluation, string[] args) =>
            useStaticGraphEvaluation ?
                args.Append("/p:RestoreUseStaticGraphEvaluation=true").ToArray() :
                args;
    }
}
