// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests
{
    [TestClass]
    public class CalculateTemplateVersionsTests : SdkTest
    {
        [TestMethod]
        public void BundledTemplatesShareTheWebProjectInstallerVersion()
        {
            string targetsPath = LayoutFiles.GetPath(
                "BundledTemplates.targets",
                Path.Combine("src", "Layout", "redist", "targets", "BundledTemplates.targets"));
            string projectPath = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "templates.proj");
            File.WriteAllText(projectPath,
                $"""
                <Project>
                  <PropertyGroup>
                    <TemplateFrameworkVersion>12.0</TemplateFrameworkVersion>
                    <MicrosoftDotNetWebItemTemplatesPackageVersion>12.0.0-alpha.1.12345.1</MicrosoftDotNetWebItemTemplatesPackageVersion>
                    <MicrosoftDotNetWebProjectTemplatesPackageVersion>12.0.0-alpha.1.12345.1</MicrosoftDotNetWebProjectTemplatesPackageVersion>
                    <MicrosoftMcpServerProjectTemplatesPackageVersion>12.0.0-alpha.1.12345.1</MicrosoftMcpServerProjectTemplatesPackageVersion>
                    <MicrosoftDotnetWinFormsProjectTemplatesPackageVersion>11.0.0</MicrosoftDotnetWinFormsProjectTemplatesPackageVersion>
                    <MicrosoftDotnetWpfProjectTemplatesPackageVersion>11.0.0</MicrosoftDotnetWpfProjectTemplatesPackageVersion>
                  </PropertyGroup>
                  <Import Project="{System.Security.SecurityElement.Escape(targetsPath)}" />
                </Project>
                """);

            using var collection = new ProjectCollection();
            Project project = collection.LoadProject(projectPath);
            TaskItem[] templates = project.GetItems("BundledTemplate")
                .Select(item => new TaskItem(item.EvaluatedInclude,
                    item.Metadata.ToDictionary(metadata => metadata.Name, metadata => metadata.EvaluatedValue)))
                .ToArray();
            templates.Select(item => item.ItemSpec).Should().Equal(
                "Microsoft.DotNet.Web.ItemTemplates",
                "Microsoft.DotNet.Web.ProjectTemplates",
                "Microsoft.McpServer.ProjectTemplates",
                "Microsoft.Dotnet.WinForms.ProjectTemplates",
                "Microsoft.Dotnet.Wpf.ProjectTemplates");

            var task = new CalculateTemplateVersions
            {
                BundledTemplates = templates,
                FullNugetVersion = "12.0.100-alpha.1.12345.1",
                ProductMonikerRid = "win-x64",
                InstallerExtension = ".msi",
                CombinedBuildNumberAndRevision = 123451,
                BuildEngine = new MockBuildEngine()
            };

            task.Execute().Should().BeTrue();
            task.TemplatesComponents.Should().ContainSingle();
            task.TemplatesComponents[0].ItemSpec.Should().Be("NetCore120Templates");
            task.TemplatesComponents[0].GetMetadata("TemplatesMajorMinorVersion")
                .Should().Be(project.GetPropertyValue("TemplateFrameworkVersion"));
            task.TemplatesComponents[0].GetMetadata("BrandNameWithoutVersion")
                .Should().Be("Microsoft .NET 12.0 Templates");
            task.BundledTemplatesWithInstallPaths.Select(item => item.GetMetadata("BundledTemplateInstallPath"))
                .Should().OnlyContain(path => path == "12.0.0-alpha.1.12345.1");
        }

        [TestMethod]
        public void WhenAspNetCoreTemplateMajorVersionLowerthan3ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.1.0");

            // The patch is 1 higher than aspnetTemplateVersion due to https://github.com/dotnet/core-sdk/issues/6243
            result.InstallPath.Should().Be("3.1.1");
            result.MajorMinorVersion.Should().Be("3.1");
            result.MajorMinorPatchVersion.Should().Be("3.1.1");
        }

        [TestMethod]
        public void WhenAspNetCoreTemplateMajorVersionLowerthan3ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("3.0.0-alpha.1.20071.6");

            result.InstallPath.Should().Be("3.0.1-alpha.1.20071.6");
            result.MajorMinorVersion.Should().Be("3.0");
            result.MajorMinorPatchVersion.Should().Be("3.0.1");
        }

        [TestMethod]
        public void WhenAspNetCoreTemplateMajorVersionHigherthan3ItCanCalculateTemplateVersionsInStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.1.0");

            // The patch align with AspNetCoreTemplateMajorVersion again, since there is no non-deterministic existing ComponentId under Major version 5.
            result.InstallPath.Should().Be("5.1.0");
            result.MajorMinorVersion.Should().Be("5.1");
            result.MajorMinorPatchVersion.Should().Be("5.1.0");
        }

        [TestMethod]
        public void WhenAspNetCoreTemplateMajorVersionHigherthan3ItCanCalculateTemplateVersionsInNonStableBuilds()
        {
            var result = CalculateTemplateVersions.Calculate("5.0.0-alpha.1.20071.6");

            result.InstallPath.Should().Be("5.0.0-alpha.1.20071.6");
            result.MajorMinorVersion.Should().Be("5.0");
            result.MajorMinorPatchVersion.Should().Be("5.0.0");
        }
    }
}
