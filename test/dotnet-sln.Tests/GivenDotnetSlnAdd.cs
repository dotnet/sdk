// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Cli.Sln.Add.Tests
{
    public class GivenDotnetSlnAdd : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Add one or more projects to a solution file.

Usage:
  dotnet solution <SLN_FILE> add [<PROJECT_PATH>...] [options]

Arguments:
  <SLN_FILE>        The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the projects to add to the solution.

Options:
  --in-root                                  Place project in root of the solution, rather than creating a solution folder.
  -s, --solution-folder <solution-folder>    The destination solution folder path to add the projects to.
  -?, -h, --help                             Show command line help";

        public GivenDotnetSlnAdd(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("sln", "--help")]
        [InlineData("sln", "-h")]
        [InlineData("sln", "-?")]
        [InlineData("sln", "/?")]
        [InlineData("solution", "--help")]
        [InlineData("solution", "-h")]
        [InlineData("solution", "-?")]
        [InlineData("solution", "/?")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string solutionCommand, string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "add", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData("sln", "")]
        [InlineData("sln", "unknownCommandName")]
        [InlineData("solution", "")]
        [InlineData("solution", "unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string solutionCommand, string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"{solutionCommand} {commandName}".Trim().Split());
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenTooManyArgumentsArePassedItPrintsError(string solutionCommand)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "one.sln", "two.sln", "three.slnx", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.slnx")}");
        }

        [Theory]
        [InlineData("sln", "idontexist.sln")]
        [InlineData("sln", "ihave?invalidcharacters")]
        [InlineData("sln", "ihaveinv@lidcharacters")]
        [InlineData("sln", "ihaveinvalid/characters")]
        [InlineData("sln", "ihaveinvalidchar\\acters")]
        [InlineData("solution", "idontexist.sln")]
        [InlineData("solution", "ihave?invalidcharacters")]
        [InlineData("solution", "ihaveinv@lidcharacters")]
        [InlineData("solution", "ihaveinvalid/characters")]
        [InlineData("solution", "ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, solutionName, "add", "p.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"InvalidSolution{solutionExtension}", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Match(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, Path.Combine(projectDirectory, $"InvalidSolution{solutionExtension}"), "*"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]

        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenInvalidSolutionIsFoundAddPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectoryRoot = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectDirectory = solutionExtension == ".sln"
                ? Path.Join(projectDirectoryRoot, "Sln")
                : Path.Join(projectDirectoryRoot, "Slnx");

            var solutionPath = Path.Combine(projectDirectory, $"InvalidSolution{solutionExtension}");
            var projectToAdd = Path.Combine("..", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Match(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionPath, "*"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenNoProjectIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNoSolutionExistsInTheDirectoryAddPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionPath)
                .Execute(solutionCommand, "add", "App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionPath + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]

        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                $"ExpectedSlnFileAfterAddingNestedProj{solutionExtension}",
                solutionExtension: solutionExtension);

            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build", $"App{solutionExtension}");
            cmd.Should().Pass();
        }

        [Theory]
        [InlineData("sln", true, ".sln")]
        [InlineData("sln", false, ".sln")]
        [InlineData("solution", true, ".sln")]
        [InlineData("solution", false, ".sln")]
        [InlineData("sln", true, ".slnx")]
        [InlineData("solution", false, ".slnx")]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreatedBuild(string solutionCommand, bool fooFirst, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirVS", identifier: $"GivenDotnetSlnAdd{solutionCommand}{fooFirst}{solutionExtension}")
                .WithSource()
                .Path;
            string projectToAdd;
            CommandResult cmd;

            if (fooFirst)
            {
                projectToAdd = "foo";
                cmd = new DotnetCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
                cmd.Should().Pass();
            }

            projectToAdd = Path.Combine("foo", "bar");
            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();

            if (!fooFirst)
            {
                projectToAdd = "foo";
                cmd = new DotnetCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
                cmd.Should().Pass();
            }

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build", $"App{solutionExtension}");
            cmd.Should().Pass();

        }

        [Theory(Skip = "Having projects with the same name in different paths is allowed.")]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNestedDuplicateProjectIsAddedToASolutionFolder(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
               .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirVSErrors", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
               .WithSource()
               .Path;
            string projectToAdd;
            CommandResult cmd;

            projectToAdd = Path.Combine("Base", "Second", "TestCollision.csproj");
            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Fail()
                .And.HaveStdErrContaining("TestCollision")
                .And.HaveStdErrContaining("Base");

            projectToAdd = Path.Combine("Base", "Second", "Third", "Second.csproj");
            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Fail()
                .And.HaveStdErrContaining("Second")
                .And.HaveStdErrContaining("Base");
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("sln", "TestAppWithSlnAnd472CsprojFiles", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("solution", "TestAppWithSlnAnd472CsprojFiles", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAnd472CsprojFiles", ".slnx")]
        public void WhenDirectoryContainingProjectIsGivenProjectIsAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "Lib");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                $"ExpectedSlnFileAfterAddingLibProj{solutionExtension}", solutionExtension: solutionExtension);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(slnFullPath);
            var directoryToAdd = "Empty";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", directoryToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(
                string.Format(
                    CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(slnFullPath);
            var directoryToAdd = "Multiple";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", directoryToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(
                string.Format(
                    CommonLocalizableStrings.MoreThanOneProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenProjectDirectoryIsAddedSolutionFoldersAreNotCreated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);

            solution.SolutionFolders.Count().Should().Be(0);
            solution.SolutionProjects
                .Where(p => p.Parent != null)
                .Count()
                .Should().Be(0);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenSharedProjectAddedShouldStillBuild(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Shared", "Shared.shproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();

            cmd = new DotnetBuildCommand(Log, $"App{solutionExtension}")
                .WithWorkingDirectory(projectDirectory)
                .Execute();
            cmd.Should().Pass();
        }

        [Theory]
        [InlineData("sln", ".", ".sln")]
        [InlineData("sln", "", ".sln")]
        [InlineData("solution", ".", ".sln")]
        [InlineData("solution", "", ".sln")]
        [InlineData("sln", ".", ".slnx")]
        [InlineData("solution", "", ".slnx")]
        public async Task WhenSolutionFolderExistsItDoesNotGetAdded(string solutionCommand, string firstComponent, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndSolutionFolders", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{firstComponent}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine($"{firstComponent}", "src", "src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);

            solution.SolutionItems.Count().Should().Be(4);
            solution.SolutionFolders.Count().Should().Be(2);

            solution.SolutionItems.Where(item => item.Parent != null).Count().Should().Be(3);

            var newlyAddedSrcFolder = solution.SolutionFolders.Single(p => p.Parent != null);
            newlyAddedSrcFolder.Parent.Id.Should().Be(solution.SolutionFolders.Single(p => p.Parent == null).Id);

            var libProject = solution.SolutionProjects.Single(p => p.ActualDisplayName.Contains("Lib"));
            libProject.Parent.Id.Should().Be(newlyAddedSrcFolder.Id);
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", "ExpectedSlnFileAfterAddingLibProj", "", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", "ExpectedSlnFileAfterAddingLibProj", "84a45d44-b677-492d-a6da-b3a71135ab8e", ".sln")]
        [InlineData("sln", "TestAppWithEmptySln", "ExpectedSlnFileAfterAddingLibProjToEmptySln", "", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", "ExpectedSlnFileAfterAddingLibProj", "", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", "ExpectedSlnFileAfterAddingLibProj", "84a45d44-b677-492d-a6da-b3a71135ab8e", ".sln")]
        [InlineData("solution", "TestAppWithEmptySln", "ExpectedSlnFileAfterAddingLibProjToEmptySln", "", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", "ExpectedSlnFileAfterAddingLibProj", "", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", "ExpectedSlnFileAfterAddingLibProj", "84a45d44-b677-492d-a6da-b3a71135ab8e", ".slnx")]
        [InlineData("solution", "TestAppWithEmptySln", "ExpectedSlnFileAfterAddingLibProjToEmptySln", "", ".slnx")]
        public void WhenValidProjectIsPassedBuildConfigsAreAdded(
            string solutionCommand,
            string testAsset,
            string expectedSlnContentsTemplate,
            string expectedProjectGuid,
            string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                $"{expectedSlnContentsTemplate}{solutionExtension}",
                expectedProjectGuid,
                solutionExtension: solutionExtension);

            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("sln", "TestAppWithEmptySln", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("solution", "TestAppWithEmptySln", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("solution", "TestAppWithEmptySln", ".slnx")]
        public void WhenValidProjectIsPassedItGetsAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var projectPath = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectPath));
            cmd.StdErr.Should().BeEmpty();
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenProjectIsAddedSolutionHasUTF8BOM(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithEmptySln", $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var preamble = Encoding.UTF8.GetPreamble();
            preamble.Length.Should().Be(3);
            using (var stream = new FileStream(Path.Combine(projectDirectory, "App.sln"), FileMode.Open))
            {
                var bytes = new byte[preamble.Length];
#pragma warning disable CA2022 // Avoid inexact read
                stream.Read(bytes, 0, bytes.Length);
#pragma warning restore CA2022 // Avoid inexact read
                bytes.Should().BeEquivalentTo(preamble);
            }
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("sln", "TestAppWithEmptySln", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("solution", "TestAppWithEmptySln", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("solution", "TestAppWithEmptySln", ".slnx")]
        public async Task WhenInvalidProjectIsPassedItDoesNotGetAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Library.cs";

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);

            var expectedNumberOfProjects = solution.SolutionProjects.Count();

            var cmd = new DotnetCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeEmpty();
            cmd.StdErr.Should().Match(string.Format(CommonLocalizableStrings.InvalidProjectWithExceptionMessage, '*', '*'));

            solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);
            solution.SolutionProjects.Count().Should().Be(expectedNumberOfProjects);
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("sln", "TestAppWithEmptySln", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("solution", "TestAppWithEmptySln", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("solution", "TestAppWithEmptySln", ".slnx")]
        public void WhenValidProjectIsPassedTheSlnBuilds(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "App/App.csproj", "Lib/Lib.csproj");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore", $"App{solutionExtension}")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("build", $"App{solutionExtension}", "--configuration", "Release")
                .Should().Pass();

            var reasonString = "should be built in release mode, otherwise it means build configurations are missing from the sln file";

            var appPathCalculator = OutputPathCalculator.FromProject(Path.Combine(projectDirectory, "App", "App.csproj"));
            new DirectoryInfo(appPathCalculator.GetOutputDirectory(configuration: "Debug")).Should().NotExist(reasonString);
            new DirectoryInfo(appPathCalculator.GetOutputDirectory(configuration: "Release")).Should().Exist()
                .And.HaveFile("App.dll");

            var libPathCalculator = OutputPathCalculator.FromProject(Path.Combine(projectDirectory, "Lib", "Lib.csproj"));
            new DirectoryInfo(libPathCalculator.GetOutputDirectory(configuration: "Debug")).Should().NotExist(reasonString);
            new DirectoryInfo(libPathCalculator.GetOutputDirectory(configuration: "Release")).Should().Exist()
                .And.HaveFile("Lib.dll");
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndExistingCsprojReferences", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferences", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndExistingCsprojReferences", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".slnx")]
        public void WhenSolutionAlreadyContainsProjectItDoesntDuplicate(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.SolutionAlreadyContainsProject, solutionPath, projectToAdd));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenPassedMultipleProjectsAndOneOfthemDoesNotExistItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(slnFullPath);

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd, "idonotexist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, "idonotexist.csproj"));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory(Skip = "https://github.com/dotnet/sdk/issues/522")]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenPassedAnUnknownProjectTypeItFails(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProject", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "App.sln");
            var contentBefore = File.ReadAllText(slnFullPath);

            var projectToAdd = Path.Combine("UnknownProject", "UnknownProject.unknownproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo("has an unknown project type and cannot be added to the solution file. Contact your SDK provider for support.");

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        // SLN ONLY
        [Theory]
        [InlineData("sln", "SlnFileWithNoProjectReferencesAndCSharpProject", "CSharpProject", "CSharpProject.csproj", ProjectTypeGuids.CSharpProjectTypeGuid)]
        [InlineData("sln", "SlnFileWithNoProjectReferencesAndFSharpProject", "FSharpProject", "FSharpProject.fsproj", ProjectTypeGuids.FSharpProjectTypeGuid)]
        [InlineData("sln", "SlnFileWithNoProjectReferencesAndVBProject", "VBProject", "VBProject.vbproj", ProjectTypeGuids.VBProjectTypeGuid)]
        [InlineData("sln", "SlnFileWithNoProjectReferencesAndUnknownProjectWithSingleProjectTypeGuid", "UnknownProject", "UnknownProject.unknownproj", "{130159A9-F047-44B3-88CF-0CF7F02ED50F}")]
        [InlineData("sln", "SlnFileWithNoProjectReferencesAndUnknownProjectWithMultipleProjectTypeGuids", "UnknownProject", "UnknownProject.unknownproj", "{130159A9-F047-44B3-88CF-0CF7F02ED50F}")]
        [InlineData("solution", "SlnFileWithNoProjectReferencesAndCSharpProject", "CSharpProject", "CSharpProject.csproj", ProjectTypeGuids.CSharpProjectTypeGuid)]
        [InlineData("solution", "SlnFileWithNoProjectReferencesAndFSharpProject", "FSharpProject", "FSharpProject.fsproj", ProjectTypeGuids.FSharpProjectTypeGuid)]
        [InlineData("solution", "SlnFileWithNoProjectReferencesAndVBProject", "VBProject", "VBProject.vbproj", ProjectTypeGuids.VBProjectTypeGuid)]
        [InlineData("solution", "SlnFileWithNoProjectReferencesAndUnknownProjectWithSingleProjectTypeGuid", "UnknownProject", "UnknownProject.unknownproj", "{130159A9-F047-44B3-88CF-0CF7F02ED50F}")]
        [InlineData("solution", "SlnFileWithNoProjectReferencesAndUnknownProjectWithMultipleProjectTypeGuids", "UnknownProject", "UnknownProject.unknownproj", "{130159A9-F047-44B3-88CF-0CF7F02ED50F}")]
        public async Task WhenPassedAProjectItAddsCorrectProjectTypeGuid(
            string solutionCommand,
            string testAsset,
            string projectDir,
            string projectName,
            string expectedTypeGuid)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine(projectDir, projectName);
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, "App.sln"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, "App.sln"), CancellationToken.None);
            var nonSolutionFolderProjects = solution.SolutionProjects;
            nonSolutionFolderProjects.Count().Should().Be(1);
            nonSolutionFolderProjects.Single().TypeId.Should().Be(new Guid(expectedTypeGuid));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenPassedAProjectWithoutATypeGuidItErrors(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProjectType", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(solutionDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(solutionPath);

            var projectToAdd = Path.Combine("UnknownProject", "UnknownProject.unknownproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.UnsupportedProjectType,
                    Path.Combine(solutionDirectory, projectToAdd)));
            cmd.StdOut.Should().BeEmpty();

            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        private async Task WhenSlnContainsSolutionFolderWithDifferentCasingItDoesNotCreateDuplicate(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCaseSensitiveSolutionFolders", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);
            solution.SolutionFolders.Count().Should().Be(1);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectWithoutMatchingConfigurationsIsAddedSolutionMapsToFirstAvailable(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(slnDirectory, $"App{solutionExtension}");

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "ProjectWithoutMatchingConfigs");
            result.Should().Pass();

            var expectedResult = File.ReadAllText(Path.Combine(slnDirectory, "Results", $"ExpectedSlnFileAfterAddingProjectWithoutMatchingConfigs{solutionExtension}"));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(expectedResult);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectWithMatchingConfigurationsIsAddedSolutionMapsAll(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(slnDirectory, $"App{solutionExtension}");

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "ProjectWithMatchingConfigs");
            result.Should().Pass();

            var expectedResult = File.ReadAllText(Path.Combine(slnDirectory, "Results", $"ExpectedSlnFileAfterAddingProjectWithMatchingConfigs{solutionExtension}"));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(expectedResult);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectWithAdditionalConfigurationsIsAddedSolutionDoesNotMapThem(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(slnDirectory, $"App{solutionExtension}");

            var result = new DotnetCommand(Log)
                .WithWorkingDirectory(slnDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "ProjectWithAdditionalConfigs");
            result.Should().Pass();

            var expectedResult = File.ReadAllText(Path.Combine(slnDirectory, "Results", $"ExpectedSlnFileAfterAddingProjectWithAdditionalConfigs{solutionExtension}"));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(expectedResult);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void ItAddsACSharpProjectThatIsMultitargeted(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("MultitargetedCS", "MultitargetedCS.csproj");

            new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute(solutionCommand, $"TestAppsWithSlnAndMultitargetedProjects{solutionExtension}", "add", projectToAdd)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void ItAddsAVisualBasicProjectThatIsMultitargeted(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("MultitargetedVB", "MultitargetedVB.vbproj");

            new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute(solutionCommand, $"TestAppsWithSlnAndMultitargetedProjects{solutionExtension}", "add", projectToAdd)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void ItAddsAnFSharpProjectThatIsMultitargeted(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(solutionDirectory, "App.sln");
            var projectToAdd = Path.Combine("MultitargetedFS", "MultitargetedFS.fsproj");

            new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute(solutionCommand, $"TestAppsWithSlnAndMultitargetedProjects{solutionExtension}", "add", projectToAdd)
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenNestedProjectIsAddedAndInRootOptionIsPassedNoSolutionFoldersAreCreated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;
            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "--in-root", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                $"ExpectedSlnFileAfterAddingProjectWithInRootOption{solutionExtension}",
                solutionExtension: solutionExtension);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenSolutionFolderIsPassedProjectsAreAddedThere(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "--solution-folder", "TestFolder", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                $"ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption{solutionExtension}",
                solutionExtension: solutionExtension);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenSolutionFolderAndInRootIsPassedItFails(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(solutionDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(solutionPath);

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "--solution-folder", "blah", "--in-root", projectToAdd);
            cmd.Should().Fail();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            cmd.StdErr.Should().Be(Tools.Sln.LocalizableStrings.SolutionFolderAndInRootMutuallyExclusive);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");

            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("sln", "/TestFolder//", "ForwardSlash", ".sln")]
        [InlineData("sln", "\\TestFolder\\\\", "BackwardSlash", ".sln")]
        [InlineData("solution", "/TestFolder//", "ForwardSlash", ".sln")]
        [InlineData("solution", "\\TestFolder\\\\", "BackwardSlash", ".sln")]

        [InlineData("sln", "/TestFolder//", "ForwardSlash", ".slnx")]
        [InlineData("solution", "\\TestFolder\\\\", "BackwardSlash", ".slnx")]
        public void WhenSolutionFolderIsPassedWithDirectorySeparatorFolderStructureIsCorrect(string solutionCommand, string solutionFolder, string testIdentifier, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testIdentifier}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "--solution-folder", solutionFolder, projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                $"ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption{solutionExtension}",
                solutionExtension: solutionExtension);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        private string GetExpectedSlnContents(
            string slnPath,
            string slnTemplateName,
            string expectedLibProjectGuid = null,
            string solutionExtension = ".sln")
        {
            var slnTemplate = GetSolutionFileTemplateContents(slnTemplateName);
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(slnPath);
            SolutionModel solution = serializer.OpenAsync(slnPath, CancellationToken.None).Result;

            if (string.IsNullOrEmpty(expectedLibProjectGuid))
            {
                var matchingProjects = solution.SolutionProjects
                    .Where(p => p.FilePath.EndsWith("Lib.csproj"))
                    .ToList();

                matchingProjects.Count.Should().Be(1);
                var slnProject = matchingProjects[0];
                expectedLibProjectGuid = slnProject.Id.ToString();
            }

            var slnContents = slnTemplate.Replace(
                "__LIB_PROJECT_GUID__",
                solutionExtension == ".sln"
                    ? $"{{{expectedLibProjectGuid.ToUpper()}}}"
                    : expectedLibProjectGuid);

            var matchingSrcFolder = solution.SolutionFolders
                .Where(p => p.Path.Contains("src"))
                .ToList();
            if (matchingSrcFolder.Count == 1)
            {
                slnContents = slnContents.Replace(
                    "__SRC_FOLDER_GUID__",
                    solutionExtension == ".sln"
                        ? $"{{{matchingSrcFolder[0].Id.ToString().ToUpper()}}}"
                        : matchingSrcFolder[0].Id.ToString());
            }

            var matchingSolutionFolder = solution.SolutionFolders
                .Where(p => p.Path.Contains("TestFolder"))
                .ToList();
            if (matchingSolutionFolder.Count == 1)
            {
                slnContents = slnContents.Replace(
                    "_SOLUTION_FOLDER_GUID__",
                    solutionExtension == ".sln"
                        ? $"{{{matchingSolutionFolder[0].Id.ToString().ToUpper()}}}"
                        : matchingSolutionFolder[0].Id.ToString());
            }

            return slnContents;
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        public void WhenSolutionIsPassedAsProjectItPrintsSuggestionAndUsage(string solutionCommand, string solutionExtension)
        {
            VerifySuggestionAndUsage(solutionCommand, "", solutionExtension);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        public void WhenSolutionIsPassedAsProjectWithInRootItPrintsSuggestionAndUsage(string solutionCommand, string solutionExtension)
        {
            VerifySuggestionAndUsage(solutionCommand, "--in-root", solutionExtension);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        public void WhenSolutionIsPassedAsProjectWithSolutionFolderItPrintsSuggestionAndUsage(string solutionCommand, string solutionExtension)
        {
            VerifySuggestionAndUsage(solutionCommand, "--solution-folder", solutionExtension);
        }
        private void VerifySuggestionAndUsage(string solutionCommand, string arguments, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}{arguments}{solutionExtension}")
                .WithSource()
                .Path;

            // TODO: Move to different location
            if (solutionExtension == ".sln")
            {
                File.Delete(Path.Join(projectDirectory, "App.slnx"));
            }
            else if (solutionExtension == ".slnx")
            {
                File.Delete(Path.Join(projectDirectory, "App.sln"));
            }

            var projectArg = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "add", arguments, "Lib", $"App{solutionExtension}", projectArg);
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo(
                string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, $"App{solutionExtension}") + Environment.NewLine
                + CommonLocalizableStrings.DidYouMean + Environment.NewLine
                + $"  dotnet solution App{solutionExtension} add {arguments} Lib {projectArg}"
            );
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        private string GetSolutionFileTemplateContents(string templateFileName)
        {
            var templateContentDirectory = _testAssetsManager
                .CopyTestAsset("SolutionFilesTemplates", identifier: "SolutionFilesTemplates")
                .WithSource()
                .Path;
            return File.ReadAllText(Path.Join(templateContentDirectory, templateFileName));
        }
    }
}
