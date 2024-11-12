// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli.Sln.Add.Tests;

public class GivenDotnetSlnAddFile(ITestOutputHelper log) : SdkTest(log)
{
    private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Add one or more solution items to a solution file.

Usage:
  dotnet solution [<SLN_FILE>] add [<PROJECT_PATH>...] file <FILE_PATH>... [options]

Arguments:
  <SLN_FILE>        The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the projects to add to the solution.
  <FILE_PATH>     The paths to the solution items to add to the solution.

Options:
  --in-root                                  Place project in root of the solution, rather than creating a solution folder.
  -s, --solution-folder <solution-folder>    The destination solution folder path to add the projects to.
  -?, -h, --help                             Show command line help.";

    private const string ExpectedSlnFileAfterAddingNestedFile = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""Solution Items"", ""Solution Items"", ""__DEFAULT_SOLUTION_FOLDER_GUID__""
	ProjectSection(SolutionItems) = preProject
		__NESTED_FILE_PATH__ = __NESTED_FILE_PATH__
	EndProjectSection
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";
    private const string ExpectedSlnFileAfterAddingFileWithSolutionFolderOption = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""TestFolder"", ""TestFolder"", ""__SOLUTION_FOLDER_GUID__""
	ProjectSection(SolutionItems) = preProject
		src\Lib\README = src\Lib\README
	EndProjectSection
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.ActiveCfg = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x64.Build.0 = Debug|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.ActiveCfg = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Debug|x86.Build.0 = Debug|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|Any CPU.Build.0 = Release|Any CPU
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.ActiveCfg = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x64.Build.0 = Release|x64
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.ActiveCfg = Release|x86
		{7072A694-548F-4CAE-A58F-12D257D5F486}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

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
            .Execute(solutionCommand, "add", "file", helpArg);
        cmd.Should().Pass()
            .And.HaveStdErr("");
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
        cmd.Should().Fail()
            .And.HaveStdErr(CommonLocalizableStrings.RequiredCommandNotPassed);
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenTooManyArgumentsArePassedItPrintsError(string solutionCommand)
    {
        var cmd = new DotnetCommand(Log)
            .Execute(solutionCommand, "one.sln", "two.sln", "three.sln", "add", "file", "README");
        cmd.Should().Fail();
        cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.sln")}");
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
            .Execute(solutionCommand, solutionName, "add", "file", "README");
        cmd.Should().Fail()
            .And.HaveStdOut("")
            .And.HaveStdErr(string.Format(CommonLocalizableStrings.CouldNotFindSolutionOrDirectory, solutionName));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenInvalidSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("InvalidSolution", identifier: solutionCommand)
            .WithSource()
            .Path;

        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "InvalidSolution.sln", "add", "file", "README");
        cmd.Should().Fail()
            .And.HaveStdOut("")
            .And.HaveStdErr(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, "InvalidSolution.sln", LocalizableStrings.FileHeaderMissingError));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenInvalidSolutionIsFoundAddPrintsErrorAndUsage(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("InvalidSolution", identifier: solutionCommand)
            .WithSource()
            .Path;

        var solutionPath = Path.Combine(projectDirectory, "InvalidSolution.sln");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "add", "file", "README");
        cmd.Should().Fail()
            .And.HaveStdOut("")
            .And.HaveStdErr(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, solutionPath, LocalizableStrings.FileHeaderMissingError));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenNoFileIsPassedItPrintsErrorAndUsage(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: solutionCommand)
            .WithSource()
            .Path;

        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file");
        cmd.Should().Fail()
            .And.HaveStdErr("Required argument missing for command: 'file'.");
        cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(projectDirectory));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenNoSolutionExistsInTheDirectoryAddPrintsErrorAndUsage(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: solutionCommand)
            .WithSource()
            .Path;

        var solutionPath = Path.Combine(projectDirectory, "App");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(solutionPath)
            .Execute(solutionCommand, "add", "file", "README");
        cmd.Should().Fail()
            .And.HaveStdErr(string.Format(CommonLocalizableStrings.SolutionDoesNotExist, solutionPath + Path.DirectorySeparatorChar))
            .And.HaveStdOut("");
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenMoreThanOneSolutionExistsInTheDirectoryItPrintsErrorAndUsage(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnAddFile")
            .WithSource()
            .Path;

        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "add", "file", "README");
        cmd.Should().Fail()
            .And.HaveStdOut("")
            .And.HaveStdErr(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenNestedFolderIsAddedSolutionFoldersAreCreated(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndReadmeInSubDir")
            .WithSource()
            .Path;

        var fileToAdd = Path.Combine("src", "Lib", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
        cmd.Should().Pass();
        cmd.StdOut.Should().BeVisuallyEquivalentTo(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, fileToAdd));
        cmd.StdErr.Should().BeEmpty();

        var slnPath = Path.Combine(projectDirectory, "App.sln");
        var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingNestedFile, nestedFilePath: fileToAdd);
        File.ReadAllText(slnPath)
            .Should().BeVisuallyEquivalentTo(expectedSlnContents);
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenNestedSolutionItemsAreAddedToASolutionFolder(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
           .CopyTestAsset("TestAppWithSlnAndReadmeInSubDir", identifier: solutionCommand)
           .WithSource()
           .Path;
        string fileToAdd;
        CommandResult cmd;

        fileToAdd = Path.Combine("src", "Lib", "README");
        cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
        cmd.Should().Pass()
            .And.HaveStdOut(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, fileToAdd))
            .And.HaveStdErr("");

        fileToAdd = Path.Combine("src", "README");
        cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
        cmd.Should().Pass()
            .And.HaveStdOut(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, fileToAdd))
            .And.HaveStdErr("");
    }

    [Theory]
    [InlineData("sln", "TestAppWithSlnAndCsprojFiles")]
    [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles")]
    [InlineData("sln", "TestAppWithEmptySln")]
    [InlineData("solution", "TestAppWithSlnAndCsprojFiles")]
    [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles")]
    [InlineData("solution", "TestAppWithEmptySln")]
    public void WhenValidFileIsPassedItGetsAdded(string solutionCommand, string testAsset)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
            .WithSource()
            .Path;

        var fileToAdd = Path.Combine("Empty", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
        cmd.Should().Pass();
        cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, fileToAdd));
        cmd.StdErr.Should().BeEmpty();
    }

    [Theory]
    [InlineData("sln", "TestAppWithSlnAndCsprojFiles")]
    [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles")]
    [InlineData("sln", "TestAppWithEmptySln")]
    [InlineData("solution", "TestAppWithSlnAndCsprojFiles")]
    [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles")]
    [InlineData("solution", "TestAppWithEmptySln")]
    public void WhenValidFileInSolutionRootIsPassedItGetsAdded(string solutionCommand, string testAsset)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
            .WithSource()
            .Path;

        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", "README");
        cmd.Should().Pass();
        cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, "README"));
        cmd.StdErr.Should().BeEmpty();
    }

    [Theory]
    [InlineData("sln", "TestAppWithSlnAndCsprojFiles")]
    [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles")]
    [InlineData("sln", "TestAppWithEmptySln")]
    [InlineData("solution", "TestAppWithSlnAndCsprojFiles")]
    [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles")]
    [InlineData("solution", "TestAppWithEmptySln")]
    public void WhenValidFileInFolderIsPassedItGetsAdded(string solutionCommand, string testAsset)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
            .WithSource()
            .Path;

        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", "README");
        cmd.Should().Pass();
        cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, "README"), because: File.ReadAllText(Path.Combine(projectDirectory, "App.sln")));
        cmd.StdErr.Should().BeEmpty();
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenFileIsAddedSolutionHasUTF8BOM(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithEmptySln", solutionCommand)
            .WithSource()
            .Path;

        var fileToAdd = Path.Combine("Empty", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
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
    [InlineData("sln", "TestAppWithSlnAndExistingCsprojReferences")]
    [InlineData("sln", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep")]
    [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferences")]
    [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep")]
    public void WhenSolutionAlreadyContainsFileItDoesntDuplicate(string solutionCommand, string testAsset)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
            .WithSource()
            .Path;
        TestCommand cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory);

        var solutionPath = Path.Combine(projectDirectory, "App.sln");
        const string fileToAdd = "README";

        cmd.Execute(solutionCommand, "App.sln", "add", "file", fileToAdd)
            .Should().Pass()
            .And.HaveStdErr("")
            .And.HaveStdOut(string.Format(CommonLocalizableStrings.SolutionItemAddedToTheSolution, fileToAdd));

        cmd.Execute(solutionCommand, "App.sln", "add", "file", fileToAdd)
            .Should().Fail()
            .And.HaveStdOut("")
            .And.HaveStdErr(string.Format(Tools.Sln.LocalizableStrings.SolutionItemWithTheSameNameExists, fileToAdd, "Solution Items"));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenPassedMultipleFilesAndOneOfthemDoesNotExistItCancelsWholeOperation(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: solutionCommand)
            .WithSource()
            .Path;

        var slnFullPath = Path.Combine(projectDirectory, "App.sln");
        var contentBefore = File.ReadAllText(slnFullPath);

        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", "README", "idonotexist.txt");
        cmd.Should().Fail();
        cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindFile, "idonotexist.txt"));

        File.ReadAllText(slnFullPath)
            .Should().BeVisuallyEquivalentTo(contentBefore);
    }

    [Theory(Skip = "https://github.com/dotnet/sdk/issues/522")]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenPassedAnUnknownProjectTypeItFails(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProject", identifier: solutionCommand)
            .WithSource()
            .Path;

        var slnFullPath = Path.Combine(projectDirectory, "App.sln");
        var contentBefore = File.ReadAllText(slnFullPath);

        var fileToAdd = Path.Combine("UnknownProject", "UnknownProject.unknownproj");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
        cmd.Should().Fail();
        cmd.StdErr.Should().BeVisuallyEquivalentTo("has an unknown project type and cannot be added to the solution file. Contact your SDK provider for support.");

        File.ReadAllText(slnFullPath)
            .Should().BeVisuallyEquivalentTo(contentBefore);
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenSlnContainsSolutionFolderWithDifferentCasingItDoesNotCreateDuplicate(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndCaseSensitiveSolutionFolders", identifier: solutionCommand)
            .WithSource()
            .Path;

        var fileToAdd = Path.Combine("src", "App", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", fileToAdd);
        cmd.Should().Pass();

        var slnFilePath = Path.Combine(projectDirectory, "App.sln");
        var slnFile = SlnFile.Read(slnFilePath);
        var solutionFolderProjects = slnFile.Projects.Where(
            p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
        solutionFolderProjects.Count().Should().Be(2, because: File.ReadAllText(slnFilePath));
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenSolutionFolderIsPassedFilesAreAddedThere(string solutionCommand)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndReadmeInSubDir", identifier: solutionCommand)
            .WithSource()
            .Path;

        var fileToAdd = Path.Combine("src", "Lib", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", "--solution-folder", "TestFolder", fileToAdd);
        cmd.Should().Pass();

        var slnPath = Path.Combine(projectDirectory, "App.sln");
        var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingFileWithSolutionFolderOption, solutionFolderName: "TestFolder");
        File.ReadAllText(slnPath)
            .Should().BeVisuallyEquivalentTo(expectedSlnContents);
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenSolutionFolderAndInRootIsPassedItFails(string solutionCommand)
    {
        var solutionDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndReadmeInSubDir", identifier: solutionCommand)
            .WithSource()
            .Path;

        var solutionPath = Path.Combine(solutionDirectory, "App.sln");
        var contentBefore = File.ReadAllText(solutionPath);

        var fileToAdd = Path.Combine("src", "Lib", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(solutionDirectory)
            .Execute(solutionCommand, "App.sln", "add", "--solution-folder", "blah", "--in-root", fileToAdd);
        cmd.Should().Fail();
        cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        cmd.StdErr.Should().Be(Tools.Sln.LocalizableStrings.SolutionFolderAndInRootMutuallyExclusive);

        File.ReadAllText(solutionPath)
            .Should()
            .BeVisuallyEquivalentTo(contentBefore);
    }

    [Theory]
    [InlineData("sln", "/TestFolder//", "ForwardSlash")]
    [InlineData("sln", "\\TestFolder\\\\", "BackwardSlash")]
    [InlineData("solution", "/TestFolder//", "ForwardSlash")]
    [InlineData("solution", "\\TestFolder\\\\", "BackwardSlash")]
    public void WhenSolutionFolderIsPassedWithDirectorySeparatorFolderStructureIsCorrect(string solutionCommand, string solutionFolder, string testIdentifier)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndReadmeInSubDir", identifier: $"{solutionCommand}{testIdentifier}")
            .WithSource()
            .Path;

        var fileToAdd = Path.Combine("src", "Lib", "README");
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "App.sln", "add", "file", "--solution-folder", solutionFolder, fileToAdd);
        cmd.Should().Pass();

        var slnPath = Path.Combine(projectDirectory, "App.sln");
        var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingFileWithSolutionFolderOption, solutionFolderName: solutionFolder);
        File.ReadAllText(slnPath)
            .Should().BeVisuallyEquivalentTo(expectedSlnContents);
    }

    private static string GetExpectedSlnContents(
        string slnPath,
        string slnTemplate,
        string solutionFolderName = "Solution Items",
        string nestedFilePath = null)
    {
        var slnFile = SlnFile.Read(slnPath);

        var matchingProjects = slnFile.Projects
            .Where((p) => p.FilePath == solutionFolderName.Trim('/', '\\'))
            .ToList();

        matchingProjects.Count.Should().Be(1, because: File.ReadAllText(slnPath)); // TODO: revert
        var slnProject = matchingProjects[0];

        var slnContents = slnTemplate.Replace("__DEFAULT_SOLUTION_FOLDER_GUID__", slnProject.Id);

        if (nestedFilePath != null)
        {
            slnContents = slnContents.Replace("__NESTED_FILE_PATH__", nestedFilePath);
        }

        var matchingSrcFolder = slnFile.Projects
                .Where((p) => p.FilePath == "src")
                .ToList();
        if (matchingSrcFolder.Count == 1)
        {
            slnContents = slnContents.Replace("__SRC_FOLDER_GUID__", matchingSrcFolder[0].Id);
        }

        var matchingSolutionFolder = slnFile.Projects
                .Where((p) => p.FilePath == "TestFolder")
                .ToList();
        if (matchingSolutionFolder.Count == 1)
        {
            slnContents = slnContents.Replace("__SOLUTION_FOLDER_GUID__", matchingSolutionFolder[0].Id);
        }

        return slnContents;
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenSolutionIsPassedAsProjectItPrintsSuggestionAndUsage(string solutionCommand)
    {
        VerifySuggestionAndUsage(solutionCommand, "");
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenSolutionIsPassedAsProjectWithInRootItPrintsSuggestionAndUsage(string solutionCommand)
    {
        VerifySuggestionAndUsage(solutionCommand, "--in-root");
    }

    [Theory]
    [InlineData("sln")]
    [InlineData("solution")]
    public void WhenSolutionIsPassedAsProjectWithSolutionFolderItPrintsSuggestionAndUsage(string solutionCommand)
    {
        VerifySuggestionAndUsage(solutionCommand, "--solution-folder");
    }
    private void VerifySuggestionAndUsage(string solutionCommand, string arguments)
    {
        var projectDirectory = _testAssetsManager
            .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}{arguments}")
            .WithSource()
            .Path;

        const string folderArg = "Lib";
        var cmd = new DotnetCommand(Log)
            .WithWorkingDirectory(projectDirectory)
            .Execute(solutionCommand, "add", "file", arguments, folderArg, "App.sln");
        cmd.Should().Fail();
        cmd.StdErr.Should().BeVisuallyEquivalentTo(
            string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, "App.sln") + Environment.NewLine
            + CommonLocalizableStrings.DidYouMean + Environment.NewLine
            + $"  dotnet solution App.sln add file {arguments} {folderArg}"
        );
        cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
    }
}
