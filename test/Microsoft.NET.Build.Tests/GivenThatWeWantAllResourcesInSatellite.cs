// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Basic.CompilerLog.Util;

namespace Microsoft.NET.Build.Tests
{
    [TestClass]
    public class GivenThatWeWantAllResourcesInSatellite : SdkTest
    {

        [TestMethod]
        [RequiresMSBuildVersion("17.1.0.60101")]
        public void It_retrieves_strings_successfully()
        {
            TestSatelliteResources(Log, TestAssetsManager);
        }

        [TestMethod]
        [DataRow("7.3")]
        [DataRow("preview")]
        [DataRow("default")]
        [DataRow(null)]
        public void It_compiles_satellite_assemblies_with_the_project_language_version(string languageVersion)
        {
            var testAsset = TestAssetsManager
                .CopyTestAsset("AllResourcesInSatellite", identifier: languageVersion ?? "unspecified")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    if (languageVersion != null)
                    {
                        project.Root.Element("PropertyGroup").Add(new XElement("LangVersion", languageVersion));
                    }
                });

            var buildCommand = new BuildCommand(testAsset);
            string binlogPath = Path.Combine(testAsset.Path, "satellites.binlog");
            buildCommand.Execute($"/bl:{binlogPath}").Should().Pass();

            using var reader = BinaryLogReader.Create(binlogPath);
            var compilerCalls = reader.ReadAllCompilerCalls();
            var mainCompilation = compilerCalls.Should().ContainSingle(call => call.Kind == CompilerCallKind.Regular).Subject;
            var satelliteCompilation = compilerCalls.Should().ContainSingle(call => call.Kind == CompilerCallKind.Satellite).Subject;
            var mainLanguageVersion = mainCompilation.GetArguments()
                .Where(argument => argument.StartsWith("/langversion:", StringComparison.Ordinal));

            if (languageVersion != null)
            {
                mainLanguageVersion.Should().ContainSingle().Which.Should().Be($"/langversion:{languageVersion}");
            }

            satelliteCompilation.GetArguments()
                .Where(argument => argument.StartsWith("/langversion:", StringComparison.Ordinal))
                .Should().Equal(mainLanguageVersion);

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);
            outputDirectory.File(Path.Combine("en", "AllResourcesInSatellite.resources.dll")).Should().Exist();
            new DotnetCommand(Log, Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.dll"))
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World from en satellite assembly");
        }

        internal static void TestSatelliteResources(
            ITestOutputHelper log,
            TestAssetsManager testAssetsManager,
            Action<XDocument> projectChanges = null,
            Action<BuildCommand> setup = null,
            [CallerMemberName] string callingMethod = null)
        {
            var testAsset = testAssetsManager
                .CopyTestAsset("AllResourcesInSatellite", callingMethod)
                .WithSource();

            if (projectChanges != null)
            {
                testAsset = testAsset.WithProjectChanges(projectChanges);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //  Also target desktop on Windows to get more test coverage:
                //    * Desktop requires satellites to have same public key as parent whereas coreclr does not.
                //    * Reference path handling of satellite assembly generation used to be incorrect for desktop.
                testAsset = testAsset.WithTargetFrameworks($"{ToolsetInfo.CurrentTargetFramework};net46");
            }

            var buildCommand = new BuildCommand(testAsset);

            if (setup != null)
            {
                setup(buildCommand);
            }

            buildCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var targetFramework in new[] { "net46", ToolsetInfo.CurrentTargetFramework })
            {
                if (targetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    continue;
                }

                var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

                var outputFiles = new List<string>
                {
                    "AllResourcesInSatellite.pdb",
                    "en/AllResourcesInSatellite.resources.dll"
                };

                TestCommand command;
                if (targetFramework == "net46")
                {
                    outputFiles.Add("AllResourcesInSatellite.exe");
                    outputFiles.Add("AllResourcesInSatellite.exe.config");
                    command = new RunExeCommand(log, Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.exe"));
                }
                else
                {
                    outputFiles.Add($"AllResourcesInSatellite{EnvironmentInfo.ExecutableExtension}");
                    outputFiles.Add("AllResourcesInSatellite.dll");
                    outputFiles.Add("AllResourcesInSatellite.deps.json");
                    outputFiles.Add("AllResourcesInSatellite.runtimeconfig.json");
                    outputFiles.Add("AllResourcesInSatellite.runtimeconfig.dev.json");
                    command = new DotnetCommand(log, Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.dll"));
                }

                outputDirectory.Should().OnlyHaveFiles(outputFiles);

                command
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World from en satellite assembly");
            }
        }
    }
}
