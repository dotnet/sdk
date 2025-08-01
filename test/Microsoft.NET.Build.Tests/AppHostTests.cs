// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tests
{
    public class AppHostTests : SdkTest
    {
        private static string[] GetExpectedFilesFromBuild(TestAsset testAsset, string targetFramework)
        {
            var testProjectName = testAsset.TestProject?.Name ?? testAsset.Name;
            var expectedFiles = new List<string>()
            {
                $"{testProjectName}{Constants.ExeSuffix}",
                $"{testProjectName}.dll",
                $"{testProjectName}.pdb",
                $"{testProjectName}.deps.json",
                $"{testProjectName}.runtimeconfig.json"
            };

            if (!string.IsNullOrEmpty(targetFramework))
            {
                var parsedTargetFramework = NuGetFramework.Parse(targetFramework);

                if (parsedTargetFramework.Version.Major < 6)
                    expectedFiles.Add($"{testProjectName}.runtimeconfig.dev.json");
            }

            return expectedFiles.ToArray();
        }

        public AppHostTests(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.1.0.60101")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_builds_a_runnable_apphost_by_default(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                // Windows Server requires setting on preview features for
                // global using directives.
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "LangVersion", "preview")));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory();
            var hostExecutable = $"HelloWorld{Constants.ExeSuffix}";
            outputDirectory.Should().OnlyHaveFiles(GetExpectedFilesFromBuild(testAsset, targetFramework));
            new RunExeCommand(Log, Path.Combine(outputDirectory.FullName, hostExecutable))
                .WithEnvironmentVariable(
                    Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                    Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [PlatformSpecificTheory(TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1", "win-x64")]
        [InlineData("net5.0", "win-x64")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "win-x64")]
        [InlineData("netcoreapp3.1", "linux-x64")]
        [InlineData("net5.0", "linux-x64")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "linux-x64")]
        public void It_does_not_try_to_codesign_non_osx_app_hosts(string targetFramework, string rid)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework, allowCopyIfPresent: true)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new string[] {
                    $"/p:RuntimeIdentifier={rid}",
                })
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid);
            var hostExecutable = $"HelloWorld{(rid.StartsWith("win") ? ".exe" : string.Empty)}";
            var appHostFullPath = Path.Combine(outputDirectory.FullName, hostExecutable);

            // Check that the apphost was not signed
            var codesignPath = @"/usr/bin/codesign";
            new RunExeCommand(Log, codesignPath, new string[] { "-d", appHostFullPath })
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining($"{appHostFullPath}: code object is not signed at all");

            var buildProjDir = Path.Combine(outputDirectory.FullName, "../..");
            Directory.Delete(buildProjDir, true);
        }

        [Theory]
        [InlineData("net8.0", "osx-x64", true)]
        [InlineData("net8.0", "osx-arm64", true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-x64", true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-arm64", true)]
        [InlineData("net8.0", "osx-x64", false)]
        [InlineData("net8.0", "osx-arm64", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-x64", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-arm64", false)]
        [InlineData("net8.0", "osx-x64", null)]
        [InlineData("net8.0", "osx-arm64", null)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-x64", null)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-arm64", null)]
        public void It_codesigns_an_app_targeting_osx(string targetFramework, string rid, bool? enableMacOSCodesign)
        {
            const bool CodesignsByDefault = true;
            const string testAssetName = "HelloWorld";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);

            var buildArgs = new List<string>() { $"/p:RuntimeIdentifier={rid}" };
            if (enableMacOSCodesign.HasValue)
            {
                buildArgs.Add($"/p:_EnableMacOSCodeSign={enableMacOSCodesign.Value}");
            }

            buildCommand
                .Execute(buildArgs.ToArray())
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid);
            var appHostFullPath = Path.Combine(outputDirectory.FullName, testAssetName);

            // Check that the apphost is signed if expected
            var shouldBeSigned = enableMacOSCodesign ?? CodesignsByDefault;
            MachOSignature.HasMachOSignatureLoadCommand(new FileInfo(appHostFullPath)).Should().Be(shouldBeSigned, $"The app host should {(shouldBeSigned ? "" : "not ")}have a Mach-O signature load command.");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                MachOSignature.HasValidMachOSignature(new FileInfo(appHostFullPath), Log)
                    .Should()
                    .Be(shouldBeSigned, $"The app host should have a valid Mach-O signature for {rid}.");
            }
        }

        [Theory]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp2.2")]
        public void It_does_not_build_with_an_apphost_by_default_before_netcoreapp_3(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
            });
        }

        [WindowsOnlyTheory]
        [InlineData("x86")]
        [InlineData("x64")]
        [InlineData("AnyCPU")]
        [InlineData("")]
        public void It_uses_an_apphost_based_on_platform_target(string target)
        {
            var targetFramework = "netcoreapp3.1";

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: target)
                .WithTargetFramework(targetFramework)
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new string[] {
                    $"/p:PlatformTarget={target}",
                    $"/p:NETCoreSdkRuntimeIdentifier={EnvironmentInfo.GetCompatibleRid(targetFramework)}"
                })
                .Should()
                .Pass();

            var apphostPath = Path.Combine(buildCommand.GetOutputDirectory().FullName, "HelloWorld.exe");
            if (target == "x86")
            {
                IsPE32(apphostPath).Should().BeTrue();
            }
            else if (target == "x64")
            {
                IsPE32(apphostPath).Should().BeFalse();
            }
            else
            {
                IsPE32(apphostPath).Should().Be(!Environment.Is64BitProcess);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public void It_can_disable_cetcompat(bool? cetCompat)
        {
            string rid = "win-x64"; // CET compat support is currently only on Windows x64
            var testProject = new TestProject()
            {
                Name = "CetCompat",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                RuntimeIdentifier = rid,
                IsExe = true,
            };
            if (cetCompat.HasValue)
            {
                testProject.AdditionalProperties.Add("CetCompat", cetCompat.ToString());
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: cetCompat.HasValue ? cetCompat.Value.ToString() : "default");
            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(runtimeIdentifier: rid);
            string apphostPath = Path.Combine(outputDirectory.FullName, $"{testProject.Name}.exe");
            bool isCetCompatible = PeReaderUtils.IsCetCompatible(apphostPath);

            // CetCompat not set : enabled
            // CetCompat = true  : enabled
            // CetCompat = false : disabled
            isCetCompatible.Should().Be(!cetCompat.HasValue || cetCompat.Value);
        }

        [Fact]
        public void It_does_not_configure_dotnet_search_options_on_build()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = new TestProject()
            {
                Name = "AppHostDotNetSearch",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("AppHostDotNetSearch", "AppRelative");
            testProject.AdditionalProperties.Add("AppHostRelativeDotNet", "subdirectory");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(runtimeIdentifier: runtimeIdentifier);
            outputDirectory.Should().HaveFiles(new[] { $"{testProject.Name}{Constants.ExeSuffix}" });

            // Value in default apphost executable for configuration of how it will search for the .NET install
            const string dotNetSearchPlaceholder = "\0\019ff3e9c3602ae8e841925bb461a0adb064a1f1903667a5e0d87e8f608f425ac";

            // Output apphost should not have .NET search location options changed, so it
            // should have the same placeholder sequence as in the default apphost binary
            ReadOnlySpan<byte> expectedBytes = Encoding.UTF8.GetBytes(dotNetSearchPlaceholder);
            ReadOnlySpan<byte> appBytes = File.ReadAllBytes(Path.Combine(outputDirectory.FullName, $"{testProject.Name}{Constants.ExeSuffix}"));
            bool found = false;
            for (int i = 0; i < appBytes.Length - expectedBytes.Length; i++)
            {
                if (!appBytes.Slice(i, expectedBytes.Length).SequenceEqual(expectedBytes))
                    continue;

                found = true;
                break;
            }

            Assert.True(found, "Expected placeholder sequence for .NET install search options was not found");
        }

        [WindowsOnlyFact]
        public void AppHost_contains_resources_from_the_managed_dll()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var version = "5.6.7.8";
            var testProject = new TestProject()
            {
                Name = "ResourceTest",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("AssemblyVersion", version);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(runtimeIdentifier: runtimeIdentifier);
            outputDirectory.Should().HaveFiles(new[] { testProject.Name + ".exe" });

            string apphostPath = Path.Combine(outputDirectory.FullName, testProject.Name + ".exe");
            var apphostVersion = FileVersionInfo.GetVersionInfo(apphostPath).FileVersion;
            apphostVersion.Should().Be(version);
        }

        [WindowsOnlyFact]
        public void FSharp_app_can_customize_the_apphost()
        {
            var targetFramework = "netcoreapp3.1";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldFS")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute("/p:CopyLocalLockFileAssemblies=false")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.deps.json",
                "TestApp.dll",
                "TestApp.exe",
                "TestApp.pdb",
                "TestApp.runtimeconfig.dev.json",
                "TestApp.runtimeconfig.json",
            });
        }

        [Fact]
        public void If_UseAppHost_is_false_it_does_not_try_to_find_an_AppHost()
        {
            var testProject = new TestProject()
            {
                Name = "NoAppHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                //  Use "any" as RID so that it will fail to find AppHost
                RuntimeIdentifier = "any",
                IsExe = true,
                SelfContained = "false"
            };
            testProject.AdditionalProperties["UseAppHost"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

        }

        [WindowsOnlyFact] // fails on Unix platforms, see https://github.com/dotnet/sdk/issues/48202
        public void It_retries_on_failure_to_create_apphost()
        {
            var testProject = new TestProject()
            {
                Name = "RetryAppHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // enable generating apphost even on macOS
            testProject.AdditionalProperties.Add("UseApphost", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var intermediateDirectory = buildCommand.GetIntermediateDirectory().FullName;

            File.SetLastWriteTimeUtc(
                Path.Combine(
                    intermediateDirectory,
                    testProject.Name + ".dll"),
                DateTime.UtcNow.AddSeconds(5));

            var intermediateAppHost = Path.Combine(intermediateDirectory, "apphost" + Constants.ExeSuffix);

            using (var stream = new FileStream(intermediateAppHost, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                const int Retries = 1;

                var result = buildCommand.Execute(
                    "/clp:NoSummary",
                    $"/p:CopyRetryCount={Retries}",
                    "/warnaserror",
                    "/p:CopyRetryDelayMilliseconds=0");

                result
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining("NETSDK1113");

                Regex.Matches(result.StdOut, "NETSDK1113", RegexOptions.None).Count.Should().Be(Retries);
            }
        }

        private static bool IsPE32(string path)
        {
            using (var reader = new PEReader(File.OpenRead(path)))
            {
                return reader.PEHeaders.PEHeader.Magic == PEMagic.PE32;
            }
        }
    }
}
