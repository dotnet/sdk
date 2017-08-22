using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildANetCoreApp : SdkTest
    {
        [Theory]
        //  TargetFramework, RuntimeFrameworkVersion, ExpectedPackageVersion, ExpectedRuntimeFrameworkVersion
        [InlineData("netcoreapp1.0", null, "1.0.5", "1.0.5")]
        [InlineData("netcoreapp1.0", "1.0.0", "1.0.0", "1.0.0")]
        [InlineData("netcoreapp1.0", "1.0.3", "1.0.3", "1.0.3")]
        [InlineData("netcoreapp1.1", null, "1.1.2", "1.1.2")]
        [InlineData("netcoreapp1.1", "1.1.0", "1.1.0", "1.1.0")]
        [InlineData("netcoreapp1.1.1", null, "1.1.1", "1.1.1")]
        public void It_targets_the_right_shared_framework(string targetFramework, string runtimeFrameworkVersion,
            string expectedPackageVersion, string expectedRuntimeVersion)
        {
            string testIdentifier = "SharedFrameworkTargeting_" + string.Join("_", targetFramework, runtimeFrameworkVersion ?? "null");

            It_targets_the_right_framework(testIdentifier, targetFramework, runtimeFrameworkVersion,
                selfContained: false, isExe: true,
                expectedPackageVersion: expectedPackageVersion, expectedRuntimeVersion: expectedRuntimeVersion);
        }

        //  Test behavior when implicit version differs for framework-dependent and self-contained apps
        [Theory]
        [InlineData("netcoreapp1.0", false, true, "1.0.5")]
        [InlineData("netcoreapp1.0", true, true, RepoInfo.ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0)]
        [InlineData("netcoreapp1.0", false, false, "1.0.5")]
        [InlineData("netcoreapp1.1", false, true, "1.1.2")]
        [InlineData("netcoreapp1.1", true, true, RepoInfo.ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1)]
        [InlineData("netcoreapp1.1", false, false, "1.1.2")]
        public void It_targets_the_right_framework_depending_on_output_type(string targetFramework, bool selfContained, bool isExe, string expectedFrameworkVersion)
        {
            string testIdentifier = "Framework_targeting_" + (isExe ? "App_" : "Lib_") + (selfContained ? "SelfContained" : "FrameworkDependent");

            It_targets_the_right_framework(testIdentifier, targetFramework, null, selfContained, isExe, expectedFrameworkVersion, expectedFrameworkVersion);
        }

        private void It_targets_the_right_framework(
            string testIdentifier,
            string targetFramework,
            string runtimeFrameworkVersion,
            bool selfContained,
            bool isExe,
            string expectedPackageVersion,
            string expectedRuntimeVersion,
            string extraMSBuildArguments = null)
        {
            string runtimeIdentifier = null;
            if (selfContained)
            {
                runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            }

            var testProject = new TestProject()
            {
                Name = "FrameworkTargetTest",
                TargetFrameworks = targetFramework,
                RuntimeFrameworkVersion = runtimeFrameworkVersion,
                IsSdkProject = true,
                IsExe = isExe,
                RuntimeIdentifier = runtimeIdentifier
            };

            var extraArgs = extraMSBuildArguments?.Split(' ') ?? Array.Empty<string>();

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testIdentifier)
                .Restore(testProject.Name, extraArgs);

            var buildCommand = new BuildCommand(Stage0MSBuild, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute(extraArgs)
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);
            if (isExe)
            {
                //  Self-contained apps don't write a framework version to the runtimeconfig, so only check this for framework-dependent apps
                if (!selfContained)
                {
                    string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
                    string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
                    JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

                    string actualRuntimeFrameworkVersion = ((JValue)runtimeConfig["runtimeOptions"]["framework"]["version"]).Value<string>();
                    actualRuntimeFrameworkVersion.Should().Be(expectedRuntimeVersion);
                }

                var runtimeconfigDevFileName = testProject.Name + ".runtimeconfig.dev.json";
                outputDirectory.Should()
                        .HaveFile(runtimeconfigDevFileName);
            }

            LockFile lockFile = LockFileUtilities.GetLockFile(Path.Combine(buildCommand.ProjectRootPath, "obj", "project.assets.json"), NullLogger.Instance);

            var target = lockFile.GetTarget(NuGetFramework.Parse(targetFramework), null);
            var netCoreAppLibrary = target.Libraries.Single(l => l.Name == "Microsoft.NETCore.App");
            netCoreAppLibrary.Version.ToString().Should().Be(expectedPackageVersion);
        }

        [Fact]
        public void It_restores_only_ridless_tfm()
        {
            //  Disable this test when using full Framework MSBuild, until MSBuild is updated 
            //  to provide conditions in NuGet ImportBefore/ImportAfter props/targets
            //  https://github.com/dotnet/sdk/issues/874
            if (UsingFullFrameworkMSBuild)
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore();

            var getValuesCommand = new GetValuesCommand(Stage0MSBuild, testAsset.TestRoot,
                "netcoreapp1.1", "TargetDefinitions", GetValuesCommand.ValueType.Item);

            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            // When RuntimeIdentifier is not specified, the assets file
            // should only contain one target with no RIDs
            var targetDefs = getValuesCommand.GetValues();
            targetDefs.Count.Should().Be(1);
            targetDefs.Should().Contain(".NETCoreApp,Version=v1.1");
        }
    }
}
