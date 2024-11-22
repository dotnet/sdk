// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;

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

        private const string ExpectedSlnFileAfterAddingLibProj = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App\App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
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
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingLibProjToEmptySln = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
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
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingNestedProj = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""src"", ""src"", ""__SRC_FOLDER_GUID__""
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
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		__LIB_PROJECT_GUID__ = __SRC_FOLDER_GUID__
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithInRootOption = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""{84A45D44-B677-492D-A6DA-B3A71135AB8E}""
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
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x64.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x64.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x86.ActiveCfg = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Debug|x86.Build.0 = Debug|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|Any CPU.Build.0 = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x64.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x64.Build.0 = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x86.ActiveCfg = Release|Any CPU
		{84A45D44-B677-492D-A6DA-B3A71135AB8E}.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""App"", ""App.csproj"", ""{7072A694-548F-4CAE-A58F-12D257D5F486}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""TestFolder"", ""TestFolder"", ""__SOLUTION_FOLDER_GUID__""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Lib"", ""src\Lib\Lib.csproj"", ""__LIB_PROJECT_GUID__""
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
		__LIB_PROJECT_GUID__.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|Any CPU.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x64.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.ActiveCfg = Debug|Any CPU
		__LIB_PROJECT_GUID__.Debug|x86.Build.0 = Debug|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|Any CPU.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x64.Build.0 = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.ActiveCfg = Release|Any CPU
		__LIB_PROJECT_GUID__.Release|x86.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution
		__LIB_PROJECT_GUID__ = __SOLUTION_FOLDER_GUID__
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
                .CopyTestAsset("InvalidSolution", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("InvalidSolution", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: "GivenDotnetSlnAdd")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreated(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingNestedProj);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);

            cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build", "App.sln");
            cmd.Should().Pass();
        }

        [Theory]
        [InlineData("sln", true, ".sln")]
        [InlineData("sln", false, ".sln")]
        [InlineData("solution", true, ".sln")]
        [InlineData("solution", false, ".sln")]
        [InlineData("sln", true, ".slnx")]
        [InlineData("sln", false, ".slnx")]
        [InlineData("solution", true, ".slnx")]
        [InlineData("solution", false, ".slnx")]
        public void WhenNestedProjectIsAddedSolutionFoldersAreCreatedBuild(string solutionCommand, bool fooFirst, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirVS", identifier: $"{solutionCommand}{fooFirst}")
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

        // TODO: Update to slnx
        [Theory(Skip = "Having projects with the same name in different paths is allowed.")]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNestedDuplicateProjectIsAddedToASolutionFolder(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
               .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirVSErrors", identifier: $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles")]
        [InlineData("sln", "TestAppWithSlnAnd472CsprojFiles")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles")]
        [InlineData("solution", "TestAppWithSlnAnd472CsprojFiles")]
        public void WhenDirectoryContainingProjectIsGivenProjectIsAdded(string solutionCommand, string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", "Lib");
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingLibProj);
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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(slnFullPath);
            var directoryToAdd = "Empty";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", directoryToAdd);
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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(slnFullPath);
            var directoryToAdd = "Multiple";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", directoryToAdd);
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(
                string.Format(
                    CommonLocalizableStrings.MoreThanOneProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToAdd)));

            File.ReadAllText(slnFullPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenProjectDirectoryIsAddedSolutionFoldersAreNotCreated(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var solutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            solutionFolderProjects.Count().Should().Be(0);
            slnFile.Sections.GetSection("NestedProjects").Should().BeNull();
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenSharedProjectAddedShouldStillBuild(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln", ".")]
        [InlineData("sln", "")]
        [InlineData("solution", ".")]
        [InlineData("solution", "")]
        public void WhenSolutionFolderExistsItDoesNotGetAdded(string solutionCommand, string firstComponent)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndSolutionFolders", identifier: $"{solutionCommand}{firstComponent}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine($"{firstComponent}", "src", "src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            slnFile.Projects.Count().Should().Be(4);

            var solutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            solutionFolderProjects.Count().Should().Be(2);

            var solutionFolders = slnFile.Sections.GetSection("NestedProjects").Properties;
            solutionFolders.Count.Should().Be(3);

            solutionFolders["{DDF3765C-59FB-4AA6-BE83-779ED13AA64A}"]
                .Should().Be("{72BFCA87-B033-4721-8712-4D12166B4A39}");

            var newlyAddedSrcFolder = solutionFolderProjects.Single(p => p.Id != "{72BFCA87-B033-4721-8712-4D12166B4A39}");
            solutionFolders[newlyAddedSrcFolder.Id]
                .Should().Be("{72BFCA87-B033-4721-8712-4D12166B4A39}");

            var libProject = slnFile.Projects.Single(p => p.Name == "Lib");
            solutionFolders[libProject.Id]
                .Should().Be(newlyAddedSrcFolder.Id);
        }

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ExpectedSlnFileAfterAddingLibProj, "")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ExpectedSlnFileAfterAddingLibProj, "{84A45D44-B677-492D-A6DA-B3A71135AB8E}")]
        [InlineData("sln", "TestAppWithEmptySln", ExpectedSlnFileAfterAddingLibProjToEmptySln, "")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ExpectedSlnFileAfterAddingLibProj, "")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ExpectedSlnFileAfterAddingLibProj, "{84A45D44-B677-492D-A6DA-B3A71135AB8E}")]
        [InlineData("solution", "TestAppWithEmptySln", ExpectedSlnFileAfterAddingLibProjToEmptySln, "")]
        public void WhenValidProjectIsPassedBuildConfigsAreAdded(
            string solutionCommand,
            string testAsset,
            string expectedSlnContentsTemplate,
            string expectedProjectGuid)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, $"{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Lib.csproj";
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");

            var expectedSlnContents = GetExpectedSlnContents(
                slnPath,
                expectedSlnContentsTemplate,
                expectedProjectGuid);

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
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("sln", "TestAppWithEmptySln", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("solution", "TestAppWithEmptySln", ".slnx")]
        public void WhenValidProjectIsPassedItGetsAdded(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
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
                .CopyTestAsset("TestAppWithEmptySln", $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles")]
        [InlineData("sln", "TestAppWithEmptySln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles")]
        [InlineData("solution", "TestAppWithEmptySln")]
        public void WhenInvalidProjectIsPassedItDoesNotGetAdded(string solutionCommand, string testAsset)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, $"{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var projectToAdd = "Lib/Library.cs";
            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var expectedNumberOfProjects = slnFile.Projects.Count();

            var cmd = new DotnetCommand(Log)
                    .WithWorkingDirectory(projectDirectory)
                    .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeEmpty();
            cmd.StdErr.Should().Match(string.Format(CommonLocalizableStrings.InvalidProjectWithExceptionMessage, '*', '*'));

            slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            slnFile.Projects.Count().Should().Be(expectedNumberOfProjects);
        }

        [Theory]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("sln", "TestAppWithEmptySln", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".sln")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".sln")]
        [InlineData("solution", "TestAppWithEmptySln", ".sln")]
        [InlineData("sln", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("sln", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("sln", "TestAppWithEmptySln", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojFiles", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndCsprojProjectGuidFiles", ".slnx")]
        [InlineData("solution", "TestAppWithEmptySln", ".slnx")]
        public void WhenValidProjectIsPassedTheSlnBuilds(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
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
        [InlineData("sln", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferences", ".slnx")]
        [InlineData("solution", "TestAppWithSlnAndExistingCsprojReferencesWithEscapedDirSep", ".slnx")]
        public void WhenSolutionAlreadyContainsProjectItDoesntDuplicate(string solutionCommand, string testAsset, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProject", identifier: $"{solutionCommand}")
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
        public void WhenPassedAProjectItAddsCorrectProjectTypeGuid(
            string solutionCommand,
            string testAsset,
            string projectDir,
            string projectName,
            string expectedTypeGuid)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, identifier: $"{solutionCommand}{testAsset}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine(projectDir, projectName);
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();
            cmd.StdErr.Should().BeEmpty();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAddedToTheSolution, projectToAdd));

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var nonSolutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid);
            nonSolutionFolderProjects.Count().Should().Be(1);
            nonSolutionFolderProjects.Single().TypeGuid.Should().Be(expectedTypeGuid);
        }

        // TODO: This should error even if guids do not exist on slnx as a concept?
        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenPassedAProjectWithoutATypeGuidItErrors(string solutionCommand, string solutionExtension)
        {
            var solutionDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferencesAndUnknownProjectType", identifier: $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        private void WhenSlnContainsSolutionFolderWithDifferentCasingItDoesNotCreateDuplicate(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCaseSensitiveSolutionFolders", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", projectToAdd);
            cmd.Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, "App.sln"));
            var solutionFolderProjects = slnFile.Projects.Where(
                p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            solutionFolderProjects.Count().Should().Be(1);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectWithoutMatchingConfigurationsIsAddedSolutionMapsToFirstAvailable(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs", identifier: $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectWithAdditionalConfigurationsIsAddedSolutionDoesNotMapThem(string solutionCommand, string solutionExtension)
        {
            var slnDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndProjectConfigs", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects", identifier: $"{solutionCommand}")
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
                .CopyTestAsset("TestAppsWithSlnAndMultitargetedProjects", identifier: $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNestedProjectIsAddedAndInRootOptionIsPassedNoSolutionFoldersAreCreated(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", "--in-root", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            // var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingProjectWithInRootOption);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(ExpectedSlnFileAfterAddingProjectWithInRootOption);
        }

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenSolutionFolderIsPassedProjectsAreAddedThere(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", "--solution-folder", "TestFolder", projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption);
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
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"{solutionCommand}")
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

        // TODO: Update to slnx
        [Theory]
        [InlineData("sln", "/TestFolder//", "ForwardSlash")]
        [InlineData("sln", "\\TestFolder\\\\", "BackwardSlash")]
        [InlineData("solution", "/TestFolder//", "ForwardSlash")]
        [InlineData("solution", "\\TestFolder\\\\", "BackwardSlash")]
        public void WhenSolutionFolderIsPassedWithDirectorySeparatorFolderStructureIsCorrect(string solutionCommand, string solutionFolder, string testIdentifier)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDir", identifier: $"{solutionCommand}{testIdentifier}")
                .WithSource()
                .Path;

            var projectToAdd = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "add", "--solution-folder", solutionFolder, projectToAdd);
            cmd.Should().Pass();

            var slnPath = Path.Combine(projectDirectory, "App.sln");
            var expectedSlnContents = GetExpectedSlnContents(slnPath, ExpectedSlnFileAfterAddingProjectWithSolutionFolderOption);
            File.ReadAllText(slnPath)
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        // TODO: Update to vs-solutionpersistence
        private string GetExpectedSlnContents(
            string slnPath,
            string slnTemplate,
            string expectedLibProjectGuid = null)
        {
            var slnFile = SlnFile.Read(slnPath);

            if (string.IsNullOrEmpty(expectedLibProjectGuid))
            {
                var matchingProjects = slnFile.Projects
                    .Where((p) => p.FilePath.EndsWith("Lib.csproj"))
                    .ToList();

                matchingProjects.Count.Should().Be(1);
                var slnProject = matchingProjects[0];
                expectedLibProjectGuid = slnProject.Id;
            }
            var slnContents = slnTemplate.Replace("__LIB_PROJECT_GUID__", expectedLibProjectGuid);

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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}{arguments}")
                .WithSource()
                .Path;

            // TODO: Move to different location
            if (solutionExtension == ".sln")
            {
                File.Delete(Path.Join(projectDirectory, "App.slnx"));
            }
            else if (solutionExtension == ".slnx")
            {
                File.Delete(Path.Join(projectDirectory, "App.slnx"));
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
    }
}
