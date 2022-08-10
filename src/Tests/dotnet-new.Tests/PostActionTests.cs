﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Build.Evaluation;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.TemplateEngine.TestHelper;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.New.Tests
{
    public partial class PostActionTests : SdkTest
    {
        private readonly ITestOutputHelper _log;

        public PostActionTests(ITestOutputHelper log) : base(log)
        {
            _log = log;
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/Basic", "TestAssets.PostActions.RestoreNuGet.Basic")]
        [InlineData("PostActions/RestoreNuGet/BasicWithFiles", "TestAssets.PostActions.RestoreNuGet.BasicWithFiles")]
        [InlineData("PostActions/RestoreNuGet/CustomSourcePath", "TestAssets.PostActions.RestoreNuGet.CustomSourcePath")]
        [InlineData("PostActions/RestoreNuGet/CustomSourcePathFiles", "TestAssets.PostActions.RestoreNuGet.CustomSourcePathFiles")]
        [InlineData("PostActions/RestoreNuGet/CustomTargetPath", "TestAssets.PostActions.RestoreNuGet.CustomTargetPath", "./Custom/Path/")]
        [InlineData("PostActions/RestoreNuGet/CustomTargetPathFiles", "TestAssets.PostActions.RestoreNuGet.CustomTargetPathFiles", "./Custom/Path/")]
        [InlineData("PostActions/RestoreNuGet/CustomSourceTargetPath", "TestAssets.PostActions.RestoreNuGet.CustomSourceTargetPath", "./Target/Output/")]
        [InlineData("PostActions/RestoreNuGet/CustomSourceTargetPathFiles", "TestAssets.PostActions.RestoreNuGet.CustomSourceTargetPathFiles", "./Target/Output/")]
        public void Restore_Basic(string templateLocation, string templateName, string targetSubfolder = "")
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, targetSubfolder, $"MyProject.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, targetSubfolder, $"Program.cs")));

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(Path.Combine(workingDirectory, targetSubfolder))
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyProject");
        }

        [Fact]
        public void Restore_WithOutputAbsolutePath()
        {
            string templateLocation = "PostActions/RestoreNuGet/Basic";
            string expectedTemplateName = "TestAssets.PostActions.RestoreNuGet.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string outputDirectory = TestUtils.CreateTemporaryFolder("output");
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            Assert.True(File.Exists(Path.Combine(outputDirectory, $"MyProject.csproj")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, $"Program.cs")));

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(outputDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyProject");
        }

        [Fact]
        public void Restore_WithOutputRelativePath()
        {
            string templateLocation = "PostActions/RestoreNuGet/Basic";
            string expectedTemplateName = "TestAssets.PostActions.RestoreNuGet.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string outputDirectory = "output";
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, outputDirectory, "MyProject.csproj"));

            Assert.True(File.Exists(Path.Combine(workingDirectory, outputDirectory, $"MyProject.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, outputDirectory, $"Program.cs")));

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(Path.Combine(workingDirectory, outputDirectory))
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyProject");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/SourceRename", "TestAssets.PostActions.RestoreNuGet.SourceRename")]
        [InlineData("PostActions/RestoreNuGet/SourceRenameFiles", "TestAssets.PostActions.RestoreNuGet.SourceRenameFiles")]
        public void Restore_SourceRenameTest(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "MyProject", "--firstRename", "Awesome")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, $"MyAwesomeTestProject.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, $"Program.cs")));

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("MyAwesomeTestProject");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsPrimaryOutputs", "TestAssets.PostActions.RestoreNuGet.TwoProjectsPrimaryOutputs")]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsFiles", "TestAssets.PostActions.RestoreNuGet.TwoProjectsFiles")]
        public void Restore_RestoreOneProjectFromTwo(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "TemplateApplication")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, $"src/TemplateApplication/TemplateApplication.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, $"test/TemplateApplication.Tests/TemplateApplication.Tests.csproj")));

            new DotnetCommand(_log, "build", "src/TemplateApplication", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("TemplateApplication");

            new DotnetCommand(_log, "build", "test/TemplateApplication.Tests", "--no-restore")
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should().Fail()
                  .And.NotHaveStdOutContaining("Build succeeded.")
                  .And.HaveStdOutContaining("TemplateApplication.Tests");
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsWithSourceRenames", "TestAssets.PostActions.RestoreNuGet.TwoProjectsWithSourceRenames")]
        [InlineData("PostActions/RestoreNuGet/TwoProjectsWithSourceRenames2", "TestAssets.PostActions.RestoreNuGet.TwoProjectsWithSourceRenames2")]
        public void Restore_SourceRenameTwoProjectsTest(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName, "-n", "TemplateApplication")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Restore succeeded.")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'dotnet restore'");

            Assert.True(File.Exists(Path.Combine(workingDirectory, $"TemplateApplication.UI/TemplateApplication.UI.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, $"TemplateApplication.Tests/TemplateApplication.Tests.csproj")));

            new DotnetCommand(_log, "build", "TemplateApplication.UI", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr()
                .And.HaveStdOutContaining("Build succeeded.")
                .And.HaveStdOutContaining("TemplateApplication.UI");

            new DotnetCommand(_log, "build", "TemplateApplication.Tests", "--no-restore")
                  .WithWorkingDirectory(workingDirectory)
                  .Execute()
                  .Should()
                  .ExitWith(0)
                  .And
                  .NotHaveStdErr()
                  .And.HaveStdOutContaining("Build succeeded.")
                  .And.HaveStdOutContaining("TemplateApplication.Tests");
        }

        [Fact]
        public void RunScript_Basic()
        {
            string templateLocation = "PostActions/RunScript/Basic";
            string templateName = "TestAssets.PostActions.RunScript.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.NotHaveStdOutContaining("Run 'chmod +x *.sh'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.cmd'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.sh'");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                commandResult.Should().HaveStdOutContaining("Hello Windows");
            }
            else
            {
                commandResult.Should().HaveStdOutContaining("Hello Unix");
            }
        }

        [Fact]
        public void RunScript_DoNotRedirect()
        {
            string templateLocation = "PostActions/RunScript/DoNotRedirect";
            string templateName = "TestAssets.PostActions.RunScript.DoNotRedirect";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("This line goes to stdout")
                .And.HaveStdErrContaining("This line goes to stderr")
                .And.NotHaveStdOutContaining("Run 'chmod +x *.sh'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.cmd'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.sh'");
        }

        [Fact]
        public void RunScript_Redirect()
        {
            string templateLocation = "PostActions/RunScript/Redirect";
            string templateName = "TestAssets.PostActions.RunScript.Redirect";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.NotHaveStdOutContaining("This line goes to stdout")
                .And.NotHaveStdOutContaining("Run 'chmod +x *.sh'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.cmd'")
                .And.NotHaveStdOutContaining("Manual instructions: Run 'setup.sh'");
        }

        [Fact]
        public void RunScript_RedirectOnError()
        {
            string templateLocation = "PostActions/RunScript/RedirectOnError";
            string templateName = "TestAssets.PostActions.RunScript.RedirectOnError";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName, "--allow-scripts", "yes")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .Fail()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdErrContaining("Command failed")
                .And.HaveStdErrContaining("This line goes to stdout")
                .And.HaveStdErrContaining("This line goes to stderr")
                .And.NotHaveStdErrContaining("Run 'chmod +x *.sh'");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                commandResult.Should().HaveStdErrContaining("Manual instructions: Run 'setup.cmd'");
            }
            else
            {
                commandResult.Should().HaveStdErrContaining("Manual instructions: Run 'setup.sh'");
            }
        }

        [Theory]
        [InlineData("PostActions/AddPackageReference/Basic", "TestAssets.PostActions.AddPackageReference.Basic")]
        [InlineData("PostActions/AddPackageReference/BasicWithFiles", "TestAssets.PostActions.AddPackageReference.BasicWithFiles")]
        public void AddPackageReference_Basic(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added a reference to the project file.")
                .And.HaveStdOutContaining("Adding a package reference Newtonsoft.Json (version: 13.0.1) to project file")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add");

            new DotnetCommand(_log, "build")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddPackageReference_WithOutputAbsolutePath()
        {
            string templateLocation = "PostActions/AddPackageReference/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddPackageReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string outputDirectory = TestUtils.CreateTemporaryFolder("output");
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", outputDirectory, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("reference Newtonsoft.Json")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            new DotnetCommand(_log, "build", Path.Combine(outputDirectory, "MyProject.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddPackageReference_WithOutputRelativePath()
        {
            string templateLocation = "PostActions/AddPackageReference/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddPackageReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", "output", "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("reference Newtonsoft.Json")
                .And.NotHaveStdOutContaining("Manual instructions: Manually add")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, "output", "MyProject.csproj"));

            new DotnetCommand(_log, "build", Path.Combine("output", "MyProject.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddProjectReference_Basic()
        {
            string templateLocation = "PostActions/AddProjectReference/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added");

            new DotnetCommand(_log, "build", "Project1/Project1.csproj")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();

            new DotnetCommand(_log, "build", "Project2/Project2.csproj")
                 .WithWorkingDirectory(workingDirectory)
                 .Execute()
                 .Should()
                 .ExitWith(0)
                 .And
                 .NotHaveStdErr();
        }

        [Fact]
        public void AddProjectReference_WithOutputAbsolutePath()
        {
            string templateLocation = "PostActions/AddProjectReference/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string outputDirectory = TestUtils.CreateTemporaryFolder("output");
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "Project1", "Project1.csproj"))
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "Project2", "Project2.csproj"));

            new DotnetCommand(_log, "build", Path.Combine(outputDirectory, "Project1", "Project1.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Fact]
        public void AddProjectReference_WithOutputRelativePath()
        {
            string templateLocation = "PostActions/AddProjectReference/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddProjectReference.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            new DotnetNewCommand(_log, expectedTemplateName, "-o", "output")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, "output", "Project1", "Project1.csproj"))
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, "output", "Project2", "Project2.csproj"));

            new DotnetCommand(_log, "build", Path.Combine("output", "Project1", "Project1.csproj"))
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And
                .NotHaveStdErr();
        }

        [Theory]
        [InlineData("PostActions/RestoreNuGet/Invalid", "TestAssets.PostActions.RestoreNuGet.Invalid", true)]
        [InlineData("PostActions/RestoreNuGet/Invalid_ContinueOnError", "TestAssets.PostActions.RestoreNuGet.Invalid.ContinueOnError", false)]
        public void ErrorExitCodeOnFailedPostAction(string templateLocation, string templateName, bool errorExpected)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName)
                   .WithCustomHive(home)
                   .WithWorkingDirectory(workingDirectory)
                   .Execute();

            if (errorExpected)
            {
                commandResult.Should().Fail();
            }
            else
            {
                commandResult.Should().Pass();
            }

            commandResult
                  .Should()
                  .HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                  .And.HaveStdOutContaining("Processing post-creation actions...")
                  .And.HaveStdOutContaining("Restoring")
                  .And.NotHaveStdOutContaining("Restore succeeded.")
                  .And.HaveStdErrContaining("Post action failed.")
                  .And.HaveStdErrContaining("Manual instructions: Run 'dotnet restore'");

            new DotnetCommand(_log, "build", "--no-restore")
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should().Fail();
        }

        [Theory]
        [InlineData("PostActions/AddProjectToSolution/Basic", "TestAssets.PostActions.AddProjectToSolution.Basic")]
        [InlineData("PostActions/AddProjectToSolution/BasicWithFiles", "TestAssets.PostActions.AddProjectToSolution.BasicWithFiles")]
        public void AddProjectToSolution_Basic(string templateLocation, string templateName)
        {
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, templateName, "-n", "MyProject")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added project(s) to a solution file.")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.");

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(workingDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_WithOutputAbsolutePath()
        {
            string templateLocation = "PostActions/AddProjectToSolution/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string outputDirectory = TestUtils.CreateTemporaryFolder("output");
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.")
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MySolution.sln"))
                .And.HaveStdOutContaining(Path.Combine(outputDirectory, "MyProject.csproj"));

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(outputDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_WithOutputRelativePath()
        {
            string templateLocation = "PostActions/AddProjectToSolution/Basic";
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            string outputDirectory = "output";
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, expectedTemplateName, "-n", "MyProject", "-o", outputDirectory)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added")
                .And.HaveStdOutContaining("solution folder: src")
                .And.NotHaveStdOutContaining("Manual instructions: Add the generated files to solution manually.")
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, outputDirectory, "MySolution.sln"))
                .And.HaveStdOutContaining(Path.Combine(workingDirectory, outputDirectory, "MyProject.csproj"));

            Assert.Contains("MyProject.csproj", File.ReadAllText(Path.Combine(workingDirectory, outputDirectory, "MySolution.sln")));
        }

        [Fact]
        public void AddProjectToSolution_PrimaryOutputIndexes()
        {
            string templateLocation = "PostActions/AddProjectToSolution/BasicWithIndexes";
            string expectedTemplateName = "TestAssets.PostActions.AddProjectToSolution.BasicWithIndexes";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            //creating solution file to add to
            new DotnetNewCommand(_log, "sln", "-n", "MySolution")
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr();

            new DotnetNewCommand(_log, expectedTemplateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute()
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{expectedTemplateName}\" was created successfully.")
                .And.HaveStdOutContaining("Successfully added project(s) to a solution file.")
                .And.HaveStdOutContaining("solution folder: Server")
                .And.NotHaveStdOutContaining("Manual instructions: Add generated Server project to solution manually to folder 'Server'.");

            Assert.True(File.Exists(Path.Combine(workingDirectory, "MySolution.sln")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, "Server/Server.csproj")));
            Assert.True(File.Exists(Path.Combine(workingDirectory, "Client/Client.csproj")));

            string solutionFileContents = File.ReadAllText(Path.Combine(workingDirectory, "MySolution.sln"));
            Assert.Contains("Server.csproj", solutionFileContents);
            Assert.DoesNotContain("Client.csproj", solutionFileContents);
        }

        [Fact]
        public void PrintInstructions_Basic()
        {
            string templateLocation = "PostActions/Instructions/Basic";
            string templateName = "TestAssets.PostActions.Instructions.Basic";
            string home = TestUtils.CreateTemporaryFolder("Home");
            string workingDirectory = TestUtils.CreateTemporaryFolder();
            Helpers.InstallTestTemplate(templateLocation, _log, home, workingDirectory);

            var commandResult = new DotnetNewCommand(_log, templateName)
                .WithCustomHive(home)
                .WithWorkingDirectory(workingDirectory)
                .Execute();

            commandResult
                .Should()
                .ExitWith(0)
                .And.NotHaveStdErr()
                .And.HaveStdOutContaining($"The template \"{templateName}\" was created successfully.")
                .And.HaveStdOutContaining($"Description: Manual actions needed")
                .And.HaveStdOutContaining($"Manual instructions: Run the following command:")
                .And.HaveStdOutContaining($"Actual command: setup.cmd <your project name>");
        }
    }
}
