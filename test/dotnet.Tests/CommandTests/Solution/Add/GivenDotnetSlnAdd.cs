// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.DotNet.Cli.Commands;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Sln.Add.Tests
{
    public static class ProjectTypeGuids
    {
        public const string CSharpProjectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        public const string FSharpProjectTypeGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";
        public const string VBProjectTypeGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        public const string SolutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        public const string SharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";
        public const string DefaultProjectGuid = "{130159A9-F047-44B3-88CF-0CF7F02ED50F}";
    }

    [TestClass]
    public class GivenDotnetSlnAdd : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Add one or more projects to a solution file.

Usage:
  dotnet solution [<SLN_FILE>] add [<PROJECT_PATH>...] [options]

Arguments:
  <SLN_FILE>      The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtilities.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>  The paths to the projects to add to the solution.

Options:
  --in-root                                Place project in root of the solution, rather than creating a solution folder. [default: False]
  -s, --solution-folder <solution-folder>  The destination solution folder path to add the projects to.
  --include-references                     Recursively add projects' ReferencedProjects to solution [default: True]
  -?, -h, --help                           Show command line help";

        public GivenDotnetSlnAdd()
        {
        }

        [TestMethod]
        [DataRow("sln", "--help")]
        [DataRow("sln", "-h")]
        [DataRow("sln", "-?")]
        [DataRow("sln", "/?")]
        [DataRow("solution", "--help")]
        [DataRow("solution", "-h")]
        [DataRow("solution", "-?")]
        [DataRow("solution", "/?")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string solutionCommand, string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "add", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [TestMethod]
        [DataRow("sln", "")]
        [DataRow("sln", "unknownCommandName")]
        [DataRow("solution", "")]
        [DataRow("solution", "unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string solutionCommand, string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"{solutionCommand} {commandName}".Trim().Split());
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CliStrings.RequiredCommandNotPassed);
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenTooManyArgumentsArePassedItPrintsError(string solutionCommand)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "one.sln", "two.sln", "three.slnx", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CliStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CliStrings.UnrecognizedCommandOrArgument, "three.slnx")}");
        }

        [TestMethod]
        [DataRow("sln", "idontexist.sln")]
        [DataRow("sln", "ihave?invalidcharacters")]
        [DataRow("sln", "ihaveinv@lidcharacters")]
        [DataRow("sln", "ihaveinvalid/characters")]
        [DataRow("sln", "ihaveinvalidchar\\acters")]
        [DataRow("solution", "idontexist.sln")]
        [DataRow("solution", "ihave?invalidcharacters")]
        [DataRow("solution", "ihaveinv@lidcharacters")]
        [DataRow("solution", "ihaveinvalid/characters")]
        [DataRow("solution", "ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, solutionName, "add", "p.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"InvalidSolution{solutionExtension}", "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Match(string.Format(CliStrings.InvalidSolutionFormatString, Path.Combine(projectDirectory, $"InvalidSolution{solutionExtension}"), "*"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]

        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenInvalidSolutionIsFoundAddPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectoryRoot = TestAssetsManager
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
            cmd.StdErr.Should().Match(string.Format(CliStrings.InvalidSolutionFormatString, solutionPath, "*"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenNoProjectIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CliStrings.SpecifyAtLeastOneProjectToAdd);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenNoSolutionExistsInTheDirectoryAddPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionPath)
                .Execute(solutionCommand, "add", "App.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.SolutionDoesNotExist, solutionPath + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "add", projectToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]

        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        // needs https://github.com/microsoft/vs-solutionpersistence/pull/101
        // [DataRow("sln", true, ".sln")]
        // [DataRow("sln", false, ".sln")]
        // [DataRow("solution", true, ".sln")]
        // [DataRow("solution", false, ".sln")]
        [DataRow("sln", true, ".slnx")]
        [DataRow("solution", false, ".slnx")]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreatedBuild(string solutionCommand, bool fooFirst, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenNestedDuplicateProjectIsAddedToASolutionFolder(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("sln", "TestAppWithSlnAnd472CsprojFiles", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("solution", "TestAppWithSlnAnd472CsprojFiles", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [DataRow("solution", "TestAppWithSlnAnd472CsprojFiles", ".slnx")]
        public void WhenDirectoryContainingProjectIsGivenProjectIsAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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
                    CliStrings.CouldNotFindAnyProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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
                    CliStrings.MoreThanOneProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public async Task WhenMultipleProjectsFromSameDirectoryAreAddedSolutionFolderIsNotDuplicated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var firstProject = Path.Combine("Multiple", "First.csproj");
            var secondProject = Path.Combine("Multiple", "Second.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", firstProject, secondProject);
            cmd.Should().Pass();

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);

            // The solution already has App project, plus we added First and Second = 3 total
            var projectsInSolution = solution.SolutionProjects.ToList();
            projectsInSolution.Count.Should().Be(3);
            projectsInSolution.Should().Contain(p => p.FilePath.Contains("First.csproj"));
            projectsInSolution.Should().Contain(p => p.FilePath.Contains("Second.csproj"));
            
            // Should only have one solution folder for "Multiple", not two
            var solutionFolders = solution.SolutionFolders.ToList();
            solutionFolders.Count.Should().Be(1);
            solutionFolders.Single().Path.Should().Contain("Multiple");
            
            // Both new projects should be in the same solution folder
            var solutionFolder = solutionFolders.Single();
            var multipleProjects = projectsInSolution.Where(p => p.FilePath.Contains("Multiple")).ToList();
            multipleProjects.Count.Should().Be(2);
            multipleProjects.All(p => p.Parent?.Id == solutionFolder.Id).Should().BeTrue();
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public async Task WhenProjectDirectoryIsAddedSolutionFoldersAreNotCreated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenSharedProjectAddedShouldStillBuild(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".", ".sln")]
        [DataRow("sln", "", ".sln")]
        [DataRow("solution", ".", ".sln")]
        [DataRow("solution", "", ".sln")]
        [DataRow("sln", ".", ".slnx")]
        [DataRow("solution", "", ".slnx")]
        public async Task WhenSolutionFolderExistsItDoesNotGetAdded(string solutionCommand, string firstComponent, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", "ExpectedSlnFileAfterAddingLibProj", "", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", "ExpectedSlnFileAfterAddingLibProj", "84a45d44-b677-492d-a6da-b3a71135ab8e", ".sln")]
        [DataRow("sln", "TestAppWithEmptySln", "ExpectedSlnFileAfterAddingLibProjToEmptySln", "", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojFiles", "ExpectedSlnFileAfterAddingLibProj", "", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", "ExpectedSlnFileAfterAddingLibProj", "84a45d44-b677-492d-a6da-b3a71135ab8e", ".sln")]
        [DataRow("solution", "TestAppWithEmptySln", "ExpectedSlnFileAfterAddingLibProjToEmptySln", "", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", "ExpectedSlnFileAfterAddingLibProj", "", ".slnx")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", "ExpectedSlnFileAfterAddingLibProj", "84a45d44-b677-492d-a6da-b3a71135ab8e", ".slnx")]
        [DataRow("solution", "TestAppWithEmptySln", "ExpectedSlnFileAfterAddingLibProjToEmptySln", "", ".slnx")]
        public void WhenValidProjectIsPassedBuildConfigsAreAdded(
            string solutionCommand,
            string testAsset,
            string expectedSlnContentsTemplate,
            string expectedProjectGuid,
            string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [DataRow("sln", "TestAppWithEmptySln", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [DataRow("solution", "TestAppWithEmptySln", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [DataRow("solution", "TestAppWithEmptySln", ".slnx")]
        public void WhenValidProjectIsPassedItGetsAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var projectPath = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAddedToTheSolution, projectPath));
            cmd.StdErr.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenProjectIsAddedSolutionHasUTF8BOM(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
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
#if NET
                stream.ReadExactly(bytes, 0, bytes.Length);
#else
                int offset = 0;
                int count = bytes.Length;
                while (count > 0)
                {
                    int read = stream.Read(bytes, offset, count);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException();
                    }
                    offset += read;
                    count -= read;
                }
#endif
                bytes.Should().BeEquivalentTo(preamble);
            }
        }

        [TestMethod]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [DataRow("sln", "TestAppWithEmptySln", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [DataRow("solution", "TestAppWithEmptySln", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [DataRow("solution", "TestAppWithEmptySln", ".slnx")]
        public async Task WhenInvalidProjectIsPassedItDoesNotGetAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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
            cmd.StdErr.Should().Match(string.Format(CliStrings.InvalidProjectWithExceptionMessage, '*', '*'));

            solution = await serializer.OpenAsync(Path.Combine(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);
            solution.SolutionProjects.Count().Should().Be(expectedNumberOfProjects);
        }

        [TestMethod]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [DataRow("sln", "TestAppWithEmptySln", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [DataRow("solution", "TestAppWithEmptySln", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [DataRow("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [DataRow("solution", "TestAppWithEmptySln", ".slnx")]
        public void WhenValidProjectIsPassedTheSlnBuilds(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", "TestAppWithSlnAndExistingCsprojReferences", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndExistingCsprojReferences", ".sln")]
        [DataRow("solution", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".sln")]
        [DataRow("sln", "TestAppWithSlnAndExistingCsprojReferences", ".slnx")]
        [DataRow("solution", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".slnx")]
        public void WhenSolutionAlreadyContainsProjectItDoesntDuplicate(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}{solutionExtension}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.SolutionAlreadyContainsProject, solutionPath, projectToAdd));
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenPassedMultipleProjectsAndOneOfthemDoesNotExistItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindProjectOrDirectory, "idonotexist.csproj"));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [TestMethod]
        [Ignore("https://github.com/dotnet/sdk/issues/522")]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenPassedAnUnknownProjectTypeItFails(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
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
        [TestMethod]
        [DataRow("sln", "SlnFileWithNoProjectReferencesAndCSharpProject", "CSharpProject", "CSharpProject.csproj", ProjectTypeGuids.CSharpProjectTypeGuid)]
        [DataRow("sln", "SlnFileWithNoProjectReferencesAndFSharpProject", "FSharpProject", "FSharpProject.fsproj", ProjectTypeGuids.FSharpProjectTypeGuid)]
        [DataRow("sln", "SlnFileWithNoProjectReferencesAndVBProject", "VBProject", "VBProject.vbproj", ProjectTypeGuids.VBProjectTypeGuid)]
        [DataRow("sln", "SlnFileWithNoProjectReferencesAndUnknownProjectWithSingleProjectTypeGuid", "UnknownProject", "UnknownProject.unknownproj", ProjectTypeGuids.DefaultProjectGuid)]
        [DataRow("sln", "SlnFileWithNoProjectReferencesAndUnknownProjectWithMultipleProjectTypeGuids", "UnknownProject", "UnknownProject.unknownproj", ProjectTypeGuids.DefaultProjectGuid)]
        [DataRow("solution", "SlnFileWithNoProjectReferencesAndCSharpProject", "CSharpProject", "CSharpProject.csproj", ProjectTypeGuids.CSharpProjectTypeGuid)]
        [DataRow("solution", "SlnFileWithNoProjectReferencesAndFSharpProject", "FSharpProject", "FSharpProject.fsproj", ProjectTypeGuids.FSharpProjectTypeGuid)]
        [DataRow("solution", "SlnFileWithNoProjectReferencesAndVBProject", "VBProject", "VBProject.vbproj", ProjectTypeGuids.VBProjectTypeGuid)]
        [DataRow("solution", "SlnFileWithNoProjectReferencesAndUnknownProjectWithSingleProjectTypeGuid", "UnknownProject", "UnknownProject.unknownproj", ProjectTypeGuids.DefaultProjectGuid)]
        [DataRow("solution", "SlnFileWithNoProjectReferencesAndUnknownProjectWithMultipleProjectTypeGuids", "UnknownProject", "UnknownProject.unknownproj", ProjectTypeGuids.DefaultProjectGuid)]
        public async Task WhenPassedAProjectItAddsCorrectProjectTypeGuid(
            string solutionCommand,
            string testAsset,
            string projectDir,
            string projectName,
            string expectedTypeGuid)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset(testAsset, identifier: $"GivenDotnetSlnAdd-{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine(projectDir, projectName);
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAddedToTheSolution, projectToAdd));

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Combine(projectDirectory, "App.sln"));
            SolutionModel solution = await serializer.OpenAsync(Path.Combine(projectDirectory, "App.sln"), CancellationToken.None);
            var nonSolutionFolderProjects = solution.SolutionProjects;
            nonSolutionFolderProjects.Count().Should().Be(1);
            nonSolutionFolderProjects.Single().TypeId.Should().Be(new Guid(expectedTypeGuid));
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenPassedAProjectWithoutATypeGuidNorDefaultTypeGuidItErrors(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = TestAssetsManager
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
                    CliStrings.UnsupportedProjectType,
                    Path.Combine(solutionDirectory, projectToAdd)));
            cmd.StdOut.Should().BeEmpty();

            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(contentBefore);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenPassedAProjectWithDefaultProjectGuidItPasses(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndDefaultProjectType", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;
            
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "add", "Unknown.unknownproj");
            
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public async Task WhenSlnContainsSolutionFolderWithDifferentCasingItDoesNotCreateDuplicate(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenProjectWithoutMatchingConfigurationsIsAddedSolutionMapsToFirstAvailable(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenProjectWithMatchingConfigurationsIsAddedSolutionMapsAll(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenProjectWithAdditionalConfigurationsIsAddedSolutionDoesNotMapThem(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void ItAddsACSharpProjectThatIsMultitargeted(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = TestAssetsManager
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
                .HaveStdOutContaining(string.Format(CliStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void ItAddsAVisualBasicProjectThatIsMultitargeted(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = TestAssetsManager
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
                .HaveStdOutContaining(string.Format(CliStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void ItAddsAnFSharpProjectThatIsMultitargeted(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = TestAssetsManager
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
                .HaveStdOutContaining(string.Format(CliStrings.ProjectAddedToTheSolution, projectToAdd));
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenNestedProjectIsAddedAndInRootOptionIsPassedNoSolutionFoldersAreCreated(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenSolutionFolderIsPassedProjectsAreAddedThere(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenSolutionFolderAndInRootIsPassedItFails(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = TestAssetsManager
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
            cmd.StdErr.Should().Be(CliCommandStrings.SolutionFolderAndInRootMutuallyExclusive);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");

            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(contentBefore);
        }

        [TestMethod]
        [DataRow("sln", "/TestFolder//", "ForwardSlash", ".sln")]
        [DataRow("sln", "\\TestFolder\\\\", "BackwardSlash", ".sln")]
        [DataRow("solution", "/TestFolder//", "ForwardSlash", ".sln")]
        [DataRow("solution", "\\TestFolder\\\\", "BackwardSlash", ".sln")]

        [DataRow("sln", "/TestFolder//", "ForwardSlash", ".slnx")]
        [DataRow("solution", "\\TestFolder\\\\", "BackwardSlash", ".slnx")]
        public void WhenSolutionFolderIsPassedWithDirectorySeparatorFolderStructureIsCorrect(string solutionCommand, string solutionFolder, string testIdentifier, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".sln")]
        [DataRow("solution", ".slnx")]
        public async Task WhenAddingProjectOutsideDirectoryItShouldNotAddSolutionFolders(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInParentDir", identifier: $"GivenDotnetSlnAdd-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;
            var projectToAdd = Path.Combine("..", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(Path.Join(projectDirectory, "Dir"))
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd);
            cmd.Should().Pass();
            // Should have no solution folders
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Join(projectDirectory, "Dir", $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Join(projectDirectory, "Dir", $"App{solutionExtension}"), CancellationToken.None);
            solution.SolutionProjects.Count.Should().Be(1);
            solution.SolutionFolders.Count.Should().Be(0);
        }

        [TestMethod]
        [DataRow("sln", ".sln", "--include-references=true")]
        [DataRow("solution", ".sln", "--include-references=true")]
        [DataRow("sln", ".slnx", "--include-references=true")]
        [DataRow("solution", ".slnx", "--include-references=true")]
        [DataRow("sln", ".sln", "--include-references=false")]
        [DataRow("solution", ".sln", "--include-references=false")]
        [DataRow("sln", ".slnx", "--include-references=false")]
        [DataRow("solution", ".slnx", "--include-references=false")]
        public async Task WhenSolutionIsPassedAProjectWithReferenceItAddsOtherProjectUnlessSpecified(string solutionCommand, string solutionExtension, string option)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("SlnFileWithReferencedProjects", identifier: $"GivenDotnetSlnAdd-{solutionCommand}")
                .WithSource()
                .Path;
            var projectToAdd = Path.Combine("A", "A.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(Path.Join(projectDirectory))
                .Execute(solutionCommand, $"App{solutionExtension}", "add", projectToAdd, option);
            cmd.Should().Pass();
            // Should have two projects
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(Path.Join(projectDirectory, $"App{solutionExtension}"));
            SolutionModel solution = await serializer.OpenAsync(Path.Join(projectDirectory, $"App{solutionExtension}"), CancellationToken.None);

            if (option.Equals("--include-references=false")) // Option is true by default
            {
                solution.SolutionProjects.Count.Should().Be(1);
            }
            else
            {
                solution.SolutionProjects.Count.Should().Be(2);
            }
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

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenSolutionIsPassedAsProjectItPrintsSuggestionAndUsage(string solutionCommand, string solutionExtension)
        {
            VerifySuggestionAndUsage(solutionCommand, "", solutionExtension);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenSolutionIsPassedAsProjectWithInRootItPrintsSuggestionAndUsage(string solutionCommand, string solutionExtension)
        {
            VerifySuggestionAndUsage(solutionCommand, "--in-root", solutionExtension);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenSolutionIsPassedAsProjectWithSolutionFolderItPrintsSuggestionAndUsage(string solutionCommand, string solutionExtension)
        {
            VerifySuggestionAndUsage(solutionCommand, "--solution-folder", solutionExtension);
        }


        private void VerifySuggestionAndUsage(string solutionCommand, string arguments, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
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
                string.Format(CliStrings.SolutionArgumentMisplaced, $"App{solutionExtension}") + Environment.NewLine
                + CliStrings.DidYouMean + Environment.NewLine
                + $"  dotnet solution App{solutionExtension} add {arguments} Lib {projectArg}"
            );
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        private string GetSolutionFileTemplateContents(string templateFileName)
        {
            var templateContentDirectory = TestAssetsManager
                .CopyTestAsset("SolutionFilesTemplates", identifier: "SolutionFilesTemplates")
                .WithSource()
                .Path;
            return File.ReadAllText(Path.Join(templateContentDirectory, templateFileName));
        }

        // SLNF TESTS
        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenAddingProjectToSlnfItAddsOnlyIfInParentSolution(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnfFiles", identifier: $"GivenDotnetSlnAdd-Slnf-{solutionCommand}")
                .WithSource()
                .Path;

            var slnfFullPath = Path.Combine(projectDirectory, "App.slnf");
            
            // Try to add Lib project which is in parent solution
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.slnf", "add", Path.Combine("src", "Lib", "Lib.csproj"));
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain(string.Format(CliStrings.ProjectAddedToTheSolution, Path.Combine("src", "Lib", "Lib.csproj")));

            // Verify the project was added to the slnf file
            var slnfContent = File.ReadAllText(slnfFullPath);
            slnfContent.Should().Contain("src\\\\Lib\\\\Lib.csproj");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenRemovingProjectFromSlnfItRemovesSuccessfully(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnfFiles", identifier: $"GivenDotnetSlnAdd-SlnfRemove-{solutionCommand}")
                .WithSource()
                .Path;

            var slnfFullPath = Path.Combine(projectDirectory, "App.slnf");

            // Remove the App project from the filter
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.slnf", "remove", Path.Combine("src", "App", "App.csproj"));
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain(string.Format(CliStrings.ProjectRemovedFromTheSolution, Path.Combine("src", "App", "App.csproj")));

            // Verify the project was removed from the slnf file
            var slnfContent = File.ReadAllText(slnfFullPath);
            slnfContent.Should().NotContain("src\\\\App\\\\App.csproj");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenAddingProjectToSlnfWithInRootOptionItErrors(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnfFiles", identifier: $"GivenDotnetSlnAdd-SlnfInRoot-{solutionCommand}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.slnf", "add", "--in-root", Path.Combine("src", "Lib", "Lib.csproj"));
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(CliCommandStrings.SolutionFilterDoesNotSupportFolderOptions);
        }

        // Each path value below contains an unescaped Windows backslash before a character that is a
        // valid JSON escape letter (\b, \n), which must be repaired before JSON parsing.
        // The paths use the pattern "..\<dir>\..\App.slnx" so the intermediate directory cancels out
        // and the path resolves to the existing App.slnx regardless of whether <dir> exists on disk.
        [TestMethod]
        [DataRow("sln", @"..\App.slnx")]                    // \A – not a JSON escape char (baseline)
        [DataRow("solution", @"..\App.slnx")]
        [DataRow("sln", @"..\bins\..\App.slnx")]            // \b in \bins is a JSON backspace escape
        [DataRow("solution", @"..\bins\..\App.slnx")]
        [DataRow("sln", @"..\new\..\App.slnx")]             // \n in \new is a JSON newline escape
        [DataRow("solution", @"..\new\..\App.slnx")]
        public void WhenAddingProjectToSlnfWithUnescapedBackslashesInPathItSucceeds(string solutionCommand, string pathValue)
        {
            var identifier = pathValue.Replace('\\', '_').Replace('.', '_').Replace('/', '_');
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnfFiles", identifier: $"GivenDotnetSlnAdd-SlnfUnescapedBackslash-{solutionCommand}-{identifier}")
                .WithSource()
                .Path;

            // Create a filters subdirectory and a .slnf file with unescaped backslashes in the path,
            // simulating the output of "dotnet new slnf -s ..\App.slnx" on Windows.
            var filtersDirectory = Path.Combine(projectDirectory, "filters");
            Directory.CreateDirectory(filtersDirectory);
            var slnfFullPath = Path.Combine(filtersDirectory, "Filter.slnf");
            // Write pathValue directly into the JSON string – pathValue contains raw backslashes,
            // which is invalid JSON but mirrors what "dotnet new slnf" produced on Windows.
            File.WriteAllText(slnfFullPath, $$"""
                {
                    "solution": {
                        "path": "{{pathValue}}",
                        "projects": []
                    }
                }
                """);

            // Verify dotnet sln can parse the .slnf file with unescaped backslashes and add a project
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, Path.Combine("filters", "Filter.slnf"), "add", Path.Combine("src", "Lib", "Lib.csproj"));
            cmd.Should().Pass();
            cmd.StdOut.Should().Contain(string.Format(CliStrings.ProjectAddedToTheSolution, Path.Combine("src", "Lib", "Lib.csproj")));
        }

    }
}
