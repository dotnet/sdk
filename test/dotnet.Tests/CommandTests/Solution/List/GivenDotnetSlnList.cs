// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Sln.List.Tests
{
    [TestClass]
    public class GivenDotnetSlnList : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  List all projects in a solution file.

Usage:
  dotnet solution [<SLN_FILE>] list [options]

Arguments:
  <SLN_FILE>  The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtilities.EnsureTrailingSlash(defaultVal)}]

Options:
  --solution-folders     Display solution folder paths.
  --format <json|table>  The output format for the list of paths. JSON output is an array of paths. [default: table]
  -?, -h, --help         Show command line help.";


        public GivenDotnetSlnList()
        {
        }

        [TestMethod]
        [DataRow("sln", "--help")]
        [DataRow("sln", "-h")]
        [DataRow("solution", "--help")]
        [DataRow("solution", "-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string solutionCommand, string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "list", helpArg);
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
                .Execute(solutionCommand, commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CliStrings.RequiredCommandNotPassed);
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenTooManyArgumentsArePassedItPrintsError(string solutionCommand)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "one.sln", "two.sln", "three.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CliStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CliStrings.UnrecognizedCommandOrArgument, "three.sln")}");
        }

        [TestMethod]
        [DataRow("sln", "idontexist.sln")]
        [DataRow("sln", "ihave?invalidcharacters.sln")]
        [DataRow("sln", "ihaveinv@lidcharacters.sln")]
        [DataRow("sln", "ihaveinvalid/characters")]
        [DataRow("sln", "ihaveinvalidchar\\acters")]
        [DataRow("solution", "idontexist.sln")]
        [DataRow("solution", "ihave?invalidcharacters.sln")]
        [DataRow("solution", "ihaveinv@lidcharacters.sln")]
        [DataRow("solution", "ihaveinvalid/characters")]
        [DataRow("solution", "ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, solutionName, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: $"GivenDotnetSlnList-InvalidSolutionPassed-{solutionCommand}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "InvalidSolution.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(
                string.Format(CliStrings.InvalidSolutionFormatString, Path.Combine(projectDirectory, "InvalidSolution.sln"), "").TrimEnd('.'));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenInvalidSolutionIsFoundListPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectRootDirectory = TestAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: $"GivenDotnetSlnList-InvalidSolutionFound-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var projectDirectory = solutionExtension == ".sln"
                ? Path.Join(projectRootDirectory, "Sln")
                : Path.Join(projectRootDirectory, "Slnx");

            var solutionFullPath = Path.Combine(projectDirectory, $"InvalidSolution{solutionExtension}");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(
                string.Format(CliStrings.InvalidSolutionFormatString, solutionFullPath, "").TrimEnd('.'));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenNoSolutionExistsInTheDirectoryListPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"GivenDotnetSlnList-{solutionCommand}")
                .WithSource()
                .Path;

            var solutionDir = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDir)
                .Execute(solutionCommand, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.SolutionDoesNotExist, solutionDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: $"GivenDotnetSlnList-{solutionCommand}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenNoProjectsArePresentInTheSolutionItPrintsANoProjectMessage(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithEmptySln", identifier: $"GivenDotnetSlnList-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(CliStrings.NoProjectsFound);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenProjectsPresentInTheSolutionItListsThem(string solutionCommand, string solutionExtension)
        {
            var expectedOutput = $@"{CliCommandStrings.ProjectsHeader}
{new string('-', CliCommandStrings.ProjectsHeader.Length)}
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: $"GivenDotnetSlnList-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(expectedOutput);
        }

        [TestMethod]
        [DataRow(".sln", "table")]
        [DataRow(".slnx", "table")]
        [DataRow(".sln", "json")]
        [DataRow(".slnx", "json")]
        public void WhenFormatIsSpecifiedItListsProjectsInThatFormat(string solutionExtension, string format)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: $"{solutionExtension}-{format}")
                .WithSource()
                .Path;
            string[] expectedPaths = [Path.Combine("App", "App.csproj"), Path.Combine("Lib", "Lib.csproj")];

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", $"App{solutionExtension}", "list", "--format", format);

            cmd.Should().Pass();
            if (format == "json")
            {
                JsonSerializer.Deserialize<string[]>(cmd.StdOut!).Should().Equal(expectedPaths);
            }
            else
            {
                cmd.StdOut.Should().BeVisuallyEquivalentTo(
                    $"{CliCommandStrings.ProjectsHeader}\n{new string('-', CliCommandStrings.ProjectsHeader.Length)}\n{string.Join('\n', expectedPaths)}");
            }
        }

        [TestMethod]
        [DataRow(".sln", false)]
        [DataRow(".slnx", false)]
        [DataRow(".sln", true)]
        [DataRow(".slnx", true)]
        public void WhenSolutionIsEmptyJsonOutputIsAnEmptyArray(string solutionExtension, bool solutionFolders)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithEmptySln", identifier: $"{solutionExtension}-{solutionFolders}")
                .WithSource()
                .Path;
            string[] args = ["sln", $"App{solutionExtension}", "list", "--format", "json"];
            if (solutionFolders)
            {
                args = [.. args, "--solution-folders"];
            }

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(args);

            cmd.Should().Pass();
            cmd.StdOut.Should().Be("[]");
        }

        [TestMethod]
        [DataRow(".sln")]
        [DataRow(".slnx")]
        public void WhenJsonFormatIsSpecifiedItListsSolutionFolders(string solutionExtension)
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("SlnFileWithSolutionItemsInNestedFolders", identifier: solutionExtension)
                .WithSource()
                .Path;
            var table = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", $"App{solutionExtension}", "list", "--solution-folders");
            var json = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", $"App{solutionExtension}", "list", "--solution-folders", "--format", "json");

            table.Should().Pass();
            json.Should().Pass();
            JsonSerializer.Deserialize<string[]>(json.StdOut!).Should().Equal(
                table.StdOut!.Split(Environment.NewLine).Skip(2));
        }

        [TestMethod]
        public void WhenJsonFormatIsSpecifiedItListsFilteredProjects()
        {
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnxAndSolutionFilters")
                .WithSource()
                .Path;
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("sln", "App.slnf", "list", "--format", "json");

            cmd.Should().Pass();
            JsonSerializer.Deserialize<string[]>(cmd.StdOut!).Should().Equal(Path.Combine("src", "App", "App.csproj"));
        }

        [TestMethod]
        [DataRow(new string[] { "--format" })]
        [DataRow(new string[] { "--format", "xml" })]
        public void WhenFormatIsMissingOrInvalidItPrintsError(string[] args)
        {
            var cmd = new DotnetCommand(Log).Execute(["sln", "list", .. args]);

            cmd.Should().Fail();
            cmd.StdErr.Should().NotBeNullOrEmpty();
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenProjectsPresentInTheReadonlySolutionItListsThem(string solutionCommand, string solutionExtension)
        {
            var expectedOutput = $@"{CliCommandStrings.ProjectsHeader}
{new string('-', CliCommandStrings.ProjectsHeader.Length)}
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: $"GivenDotnetSlnList-Readonly-{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var slnFileName = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var attributes = File.GetAttributes(slnFileName);
            File.SetAttributes(slnFileName, attributes | FileAttributes.ReadOnly);

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(expectedOutput);
        }

        [TestMethod]
        [DataRow("sln", ".sln")]
        [DataRow("solution", ".sln")]
        [DataRow("sln", ".slnx")]
        [DataRow("solution", ".slnx")]
        public void WhenProjectsInSolutionFoldersPresentInTheSolutionItListsSolutionFolderPaths(string solutionCommand, string solutionExtension)
        {
            string[] expectedOutput = { $"{CliCommandStrings.SolutionFolderHeader}",
$"{new string('-', CliCommandStrings.SolutionFolderHeader.Length)}",
$"{Path.Combine("NestedSolution", "NestedFolder", "NestedFolder")}" };

            var projectDirectory = TestAssetsManager
                .CopyTestAsset("SlnFileWithSolutionItemsInNestedFolders", identifier: $"GivenDotnetSlnList-{solutionCommand}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "list", "--solution-folders");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainAll(expectedOutput);
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenSolutionFilterIsPassedItListsProjectsMatching(string solutionCommand)
        {
            string[] expectedOutput = { $"{CliCommandStrings.ProjectsHeader}",
                $"{new string('-', CliCommandStrings.ProjectsHeader.Length)}",
                $"{Path.Combine("src", "App", "App.csproj")}" };
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnxAndSolutionFilters", identifier: "GivenDotnetSlnList-Filter")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.slnf", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainAll(expectedOutput);
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenSolutionFilterOriginalPathContainsSpecialCharactersTheyAreUnescaped(string solutionCommand)
        {
            string[] expectedOutput = { $"{CliCommandStrings.ProjectsHeader}",
                $"{new string('-', CliCommandStrings.ProjectsHeader.Length)}",
                $"{Path.Combine("src", "App", "App.csproj")}" };
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithSlnAndSlnfWithSpecialCharactersInPath", identifier: "GivenDotnetSlnList-Filter-Unescape")
                .WithSource()
                .Path;
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.slnf", "list");

            cmd.Should().Pass();
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenSolutionFilterWithTrailingCommaIsPassedItListsProjects(string solutionCommand)
        {
            string[] expectedOutput = { $"{CliCommandStrings.ProjectsHeader}",
                $"{new string('-', CliCommandStrings.ProjectsHeader.Length)}",
                $"{Path.Combine("App", "App.csproj")}",
                $"{Path.Combine("Lib", "Lib.csproj")}" };
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithTrailingCommaSlnf", identifier: "GivenDotnetSlnList-TrailingComma")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "AppWithTrailingComma.slnf", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainAll(expectedOutput);
        }

        [TestMethod]
        [DataRow("sln")]
        [DataRow("solution")]
        public void WhenSolutionFilterWithCommentsIsPassedItListsProjects(string solutionCommand)
        {
            string[] expectedOutput = { $"{CliCommandStrings.ProjectsHeader}",
                $"{new string('-', CliCommandStrings.ProjectsHeader.Length)}",
                $"{Path.Combine("App", "App.csproj")}" };
            var projectDirectory = TestAssetsManager
                .CopyTestAsset("TestAppWithTrailingCommaSlnf", identifier: "GivenDotnetSlnList-Comments")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "AppWithComments.slnf", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainAll(expectedOutput);
        }
    }
}
