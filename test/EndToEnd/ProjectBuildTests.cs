using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace EndToEnd.Tests
{
    public class ProjectBuildTests : TestBase
    {
        [Fact]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;

            string newArgs = "console --debug:ephemeral-hive --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            var runCommand = new RunCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()
                // Templates are still at 3.1 and will not run on 5.0, revert to commented out assertion when 5.0 templates land
                //.Should().Pass().And.HaveStdOutContaining("Hello World!");
                .Should().Fail().And.HaveStdErrContaining("https://aka.ms/dotnet-core-applaunch");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [Fact]
        public void ItCanRunAnAppUsingTheWebSdk()
        {
            var directory = TestAssets.CreateTestDirectory();
            string projectDirectory = directory.FullName;

            string newArgs = "console --debug:ephemeral-hive --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            string projectPath = Path.Combine(projectDirectory, directory.Name + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            project.Root.Attribute("Sdk").Value = "Microsoft.NET.Sdk.Web";

            project.Save(projectPath);

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            var runCommand = new RunCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput()
                // Templates are still at 3.1 and will not run on 5.0, revert to commented out assertion when 5.0 templates land
                //.Should().Pass().And.HaveStdOutContaining("Hello World!");
                .Should().Fail().And.HaveStdErrContaining("https://aka.ms/dotnet-core-applaunch");

        }

        [Theory]
        [InlineData("console")]
        [InlineData("classlib")]
        [InlineData("mstest")]
        [InlineData("nunit")]
        [InlineData("web")]
        [InlineData("mvc")]
        public void ItCanBuildTemplates(string templateName)
        {
            TestTemplateBuild(templateName);
        }

        [WindowsOnlyTheory]
        [InlineData("wpf", Skip = "https://github.com/dotnet/wpf/issues/2363")]
        [InlineData("winforms", Skip = "https://github.com/dotnet/wpf/issues/2363")]
        public void ItCanBuildDesktopTemplates(string templateName)
        {
            TestTemplateBuild(templateName);
        }

        [WindowsOnlyTheory]
        [InlineData("wpf", Skip = "https://github.com/dotnet/wpf/issues/2363")]
        public void ItCanBuildDesktopTemplatesSelfContained(string templateName)
        {
            TestTemplateBuild(templateName);
        }

        [Theory]
        [InlineData("web")]
        [InlineData("console")]
        public void ItCanBuildTemplatesSelfContained(string templateName)
        {
            TestTemplateBuild(templateName, selfContained: true);
        }

        private void TestTemplateBuild(string templateName, bool selfContained = false)
        {
            var directory = TestAssets.CreateTestDirectory(identifier: templateName);
            string projectDirectory = directory.FullName;

            string newArgs = $"{templateName} --debug:ephemeral-hive --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            var buildArgs = selfContained ? "" :$"-r {RuntimeEnvironment.GetRuntimeIdentifier()}";
            var dotnetRoot = Path.GetDirectoryName(RepoDirectoriesProvider.DotnetUnderTest);
            new BuildCommand()
                 .WithEnvironmentVariable("PATH", dotnetRoot) // override PATH since razor rely on PATH to find dotnet
                 .WithWorkingDirectory(projectDirectory)
                 .Execute(buildArgs)
                 .Should().Pass();
        }
    }
}
