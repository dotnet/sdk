using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
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
                .WithWorkingDirectory(projectDirectory);

            runCommand.ExecuteWithCapturedOutput()
                // Templates are still at 3.0 and will not run on 5.0, revert to commented out assertion when 5.0 templates land
                //.Should().Pass().And.HaveStdOutContaining("Hello World!");
                .Should().Fail().And.HaveStdErrContaining("https://aka.ms/dotnet-download");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [Fact(Skip = "Need support for ASP.NET on .NET Core 3.1")]
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
                .WithWorkingDirectory(projectDirectory);

            runCommand.ExecuteWithCapturedOutput()
                // Templates are still at 3.0 and will not run on 5.0, revert to commented out assertion when 5.0 templates land
                //.Should().Pass().And.HaveStdOutContaining("Hello World!");
                .Should().Fail().And.HaveStdErrContaining("https://aka.ms/dotnet-download");


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
        [InlineData("wpf")]
        [InlineData("winforms")]
        public void ItCanBuildDesktopTemplates(string templateName)
        {
            TestTemplateBuild(templateName);
        }

        private void TestTemplateBuild(string templateName)
        {
            var directory = TestAssets.CreateTestDirectory(identifier: templateName);
            string projectDirectory = directory.FullName;

            string newArgs = $"{templateName} --debug:ephemeral-hive --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            //  Work-around for MVC template test until ASP.Net publishes Preview 5 'Microsoft.AspNetCore.Mvc.NewtonsoftJson' to NuGet.org
            string restoreArgs = string.Equals(templateName, "mvc", StringComparison.OrdinalIgnoreCase) ? "/p:RestoreAdditionalProjectSources=https://dotnetfeed.blob.core.windows.net/aspnet-aspnetcore/index.json" : "";
            new RestoreCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute(restoreArgs)
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass();
        }
    }
}
