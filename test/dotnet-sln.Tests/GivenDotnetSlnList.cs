// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using CommandLocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Sln.List.Tests
{
    public class GivenDotnetSlnList : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  List all projects in a solution file.

Usage:
  dotnet solution <SLN_FILE> list [options]

Arguments:
  <SLN_FILE>    The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  --solution-folders  Display solution folder paths.
  -?, -h, --help    Show command line help.";


        public GivenDotnetSlnList(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("sln", "--help")]
        [InlineData("sln", "-h")]
        [InlineData("solution", "--help")]
        [InlineData("solution", "-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string solutionCommand, string helpArg)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "list", helpArg);
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
                .Execute(solutionCommand, commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenTooManyArgumentsArePassedItPrintsError(string solutionCommand)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "one.sln", "two.sln", "three.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
        }

        [Theory]
        [InlineData("sln", "idontexist.sln")]
        [InlineData("sln", "ihave?invalidcharacters.sln")]
        [InlineData("sln", "ihaveinv@lidcharacters.sln")]
        [InlineData("sln", "ihaveinvalid/characters")]
        [InlineData("sln", "ihaveinvalidchar\\acters")]
        [InlineData("solution", "idontexist.sln")]
        [InlineData("solution", "ihave?invalidcharacters.sln")]
        [InlineData("solution", "ihaveinv@lidcharacters.sln")]
        [InlineData("solution", "ihaveinvalid/characters")]
        [InlineData("solution", "ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, solutionName, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: "GivenDotnetSlnList-InvalidSolutionPassed")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "InvalidSolution.sln", "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(
                string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, Path.Combine(projectDirectory, "InvalidSolution.sln"), "").TrimEnd('.'));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenInvalidSolutionIsFoundListPrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectRootDirectory = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: "GivenDotnetSlnList-InvalidSolutionFound")
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
                string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionFullPath, "").TrimEnd('.'));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNoSolutionExistsInTheDirectoryListPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var solutionDir = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionDir)
                .Execute(solutionCommand, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "list");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenNoProjectsArePresentInTheSolutionItPrintsANoProjectMessage(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithEmptySln", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(CommonLocalizableStrings.NoProjectsFound);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectsPresentInTheSolutionItListsThem(string solutionCommand, string solutionExtension)
        {
            var expectedOutput = $@"{CommandLocalizableStrings.ProjectsHeader}
{new string('-', CommandLocalizableStrings.ProjectsHeader.Length)}
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "list");
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(expectedOutput);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectsPresentInTheReadonlySolutionItListsThem(string solutionCommand, string solutionExtension)
        {
            var expectedOutput = $@"{CommandLocalizableStrings.ProjectsHeader}
{new string('-', CommandLocalizableStrings.ProjectsHeader.Length)}
{Path.Combine("App", "App.csproj")}
{Path.Combine("Lib", "Lib.csproj")}";

            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: "GivenDotnetSlnList-Readonly")
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

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenProjectsInSolutionFoldersPresentInTheSolutionItListsSolutionFolderPaths(string solutionCommand)
        {
            string[] expectedOutput = { $"{CommandLocalizableStrings.SolutionFolderHeader}",
$"{new string('-', CommandLocalizableStrings.SolutionFolderHeader.Length)}",
$"{Path.Combine("NestedSolution", "NestedFolder", "NestedFolder")}" };

            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithSolutionItemsInNestedFolders", identifier: "GivenDotnetSlnList")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "list", "--solution-folders");
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainAll(expectedOutput);
        }
    }
}
