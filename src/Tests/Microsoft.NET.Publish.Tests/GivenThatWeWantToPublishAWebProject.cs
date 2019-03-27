using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAWebProject : SdkTest
    {
        public GivenThatWeWantToPublishAWebProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_should_publish_self_contained()
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebTest",
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(
                    (filename, project) =>
                    {
                        project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
                    });

            var command = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            command
                .Execute("/restore", $"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(
                targetFramework: tfm,
                runtimeIdentifier: rid);

            output.Should().HaveFiles(new[] {
                $"{testProject.Name}{Constants.ExeSuffix}",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
            });

            output.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });

            Command.Create(Path.Combine(output.FullName, $"{testProject.Name}{Constants.ExeSuffix}"), new string[] {})
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Theory]
        [InlineData("Microsoft.AspNetCore.App")]
        [InlineData("Microsoft.AspNetCore.All")]
        public void It_should_publish_framework_dependent(string platformLibrary)
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebTest",
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference(platformLibrary));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(
                    (filename, project) =>
                    {
                        project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
                    });

            var command = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            command
                .Execute("/restore", $"/p:RuntimeIdentifier={rid}", "/p:SelfContained=false")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(
                targetFramework: tfm,
                runtimeIdentifier: rid);

            output.Should().OnlyHaveFiles(new[] {
                $"{testProject.Name}{Constants.ExeSuffix}",
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
            });

            Command.Create(Path.Combine(output.FullName, $"{testProject.Name}{Constants.ExeSuffix}"), new string[] {})
               .EnvironmentVariable(
                    Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                    Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath))
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void It_publishes_with_a_publish_profile(bool selfContained, bool useAppHost)
        {
            var tfm = "netcoreapp2.2";

            var testProject = new TestProject()
            {
                Name = "WebWithPublishProfile",
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("AspNetCoreHostingModel", "InProcess");
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.App"));
            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.AspNetCore.Razor.Design", version: "2.2.0", privateAssets: "all"));

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(
                    (filename, project) =>
                    {
                        project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";
                    });

            var projectDirectory = Path.Combine(testProjectInstance.Path, testProject.Name);
            var publishProfilesDirectory = Path.Combine(projectDirectory, "Properties", "PublishProfiles");
            Directory.CreateDirectory(publishProfilesDirectory);

            File.WriteAllText(Path.Combine(publishProfilesDirectory, "test.pubxml"), $@"
<Project>
  <PropertyGroup>
    <RuntimeIdentifier>{EnvironmentInfo.GetCompatibleRid(tfm)}</RuntimeIdentifier>
    <SelfContained>{selfContained}</SelfContained>
    <UseAppHost>{selfContained || useAppHost}</UseAppHost>
  </PropertyGroup>
</Project>
");

            var command = new PublishCommand(Log, projectDirectory);
            command
                .Execute("/restore", "/p:PublishProfile=test")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(tfm);

            output.Should().HaveFiles(new[] {
                $"{testProject.Name}.dll",
                $"{testProject.Name}.pdb",
                $"{testProject.Name}.deps.json",
                $"{testProject.Name}.runtimeconfig.json",
                "web.config",
            });

            if (selfContained)
            {
                output.Should().HaveFiles(new[] {
                    $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                });
            }
            else
            {
                output.Should().NotHaveFiles(new[] {
                    $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                    $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                });
            }

            if (selfContained || useAppHost)
            {
                output.Should().HaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }
            else
            {
                output.Should().NotHaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            }
        }
    }
}
