// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Common;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Microsoft.DotNet.Cli.Sln.Remove.Tests
{
    public class GivenDotnetSlnRemove : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove one or more projects from a solution file.

Usage:
  dotnet solution <SLN_FILE> remove [<PROJECT_PATH>...] [options]

Arguments:
  <SLN_FILE>        The solution file to operate on. If not specified, the command will search the current directory for one. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the projects to remove from the solution.

Options:
  -?, -h, --help    Show command line help.";

        public GivenDotnetSlnRemove(ITestOutputHelper log) : base(log)
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
                .Execute(solutionCommand, "remove", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenTooManyArgumentsArePassedItPrintsError(string solutionCommand)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, "one.sln", "two.sln", "three.slnx", "remove");
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "two.sln")}
{string.Format(CommandLineValidation.LocalizableStrings.UnrecognizedCommandOrArgument, "three.slnx")}");
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
        [InlineData("sln", "idontexist.sln")]
        [InlineData("sln", "idontexist.slnx")]
        [InlineData("sln", "ihave?invalidcharacters")]
        [InlineData("sln", "ihaveinv@lidcharacters")]
        [InlineData("sln", "ihaveinvalid/characters")]
        [InlineData("sln", "ihaveinvalidchar\\acters")]
        [InlineData("solution", "idontexist.sln")]
        [InlineData("solution", "idontexist.slnx")]
        [InlineData("solution", "ihave?invalidcharacters")]
        [InlineData("solution", "ihaveinv@lidcharacters")]
        [InlineData("solution", "ihaveinvalid/characters")]
        [InlineData("solution", "ihaveinvalidchar\\acters")]
        public void WhenNonExistingSolutionIsPassedItPrintsErrorAndUsage(string solutionCommand, string solutionName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute(solutionCommand, solutionName, "remove", "p.csproj");
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
                .CopyTestAsset("InvalidSolution", identifier: $"{solutionCommand}GivenDotnetSlnRemove")
                .WithSource()
                .Path;

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"InvalidSolution{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Match(string.Format(CommonLocalizableStrings.InvalidSolutionFormatString, Path.Combine(projectDirectory, $"InvalidSolution{solutionExtension}"), "*"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenInvalidSolutionIsFoundRemovePrintsErrorAndUsage(string solutionCommand, string solutionExtension)
        {
            var projectDirectoryRoot = _testAssetsManager
                .CopyTestAsset("InvalidSolution", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectDirectory = solutionExtension == ".sln"
                ? Path.Join(projectDirectoryRoot, "Sln")
                : Path.Join(projectDirectoryRoot, "Slnx");

            var solutionPath = Path.Combine(projectDirectory, $"InvalidSolution{solutionExtension}");
            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "remove", projectToRemove);
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
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}GivenDotnetSlnRemove")
                .WithSource()
                .Path;

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenNoSolutionExistsInTheDirectoryRemovePrintsErrorAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(solutionPath)
                .Execute(solutionCommand, "remove", "App.csproj");
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
                .CopyTestAsset("TestAppWithMultipleSlnFiles", identifier: $"{solutionCommand}GivenDotnetSlnRemove")
                .WithSource()
                .Path;

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "remove", projectToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneSolutionInDirectory, projectDirectory + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenPassedAReferenceNotInSlnItPrintsStatus(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");
            var contentBefore = File.ReadAllText(solutionPath);
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", "referenceDoesNotExistInSln.csproj");
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectNotFoundInTheSolution, "referenceDoesNotExistInSln.csproj"));
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(contentBefore);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenPassedAReferenceItRemovesTheReferenceButNotOtherReferences(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);

            solution.SolutionProjects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove));

            solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);
            solution.SolutionProjects.Count.Should().Be(1);
            solution.SolutionProjects.Single().FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenSolutionItemsExistInFolderParentFoldersAreNotRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithSolutionItemsInNestedFolders", identifier: $"{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var projectToRemove = Path.Combine("ConsoleApp1", "ConsoleApp1.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove));

            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemoveProjectInSolutionWithNestedSolutionItems{solutionExtension}");
            File.ReadAllText(solutionPath)
                .Should()
                .BeVisuallyEquivalentTo(templateContents);
        }

        [Theory(Skip = "vs-solutionpersistence does not allow duplicate references.")]
        [InlineData("sln")]
        [InlineData("solution")]
        public async Task WhenDuplicateReferencesArePresentItRemovesThemAll(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndDuplicateProjectReferences", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, "App.sln");
            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);
            solution.SolutionProjects.Count.Should().Be(3);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "remove", projectToRemove);
            cmd.Should().Pass();

            string outputText = string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove);
            outputText += Environment.NewLine + outputText;
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);

            solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);
            solution.SolutionProjects.Count.Should().Be(1);
            solution.SolutionProjects.Single().FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenPassedMultipleReferencesAndOneOfThemDoesNotExistItRemovesTheOneThatExists(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndExistingCsprojReferences", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);

            solution.SolutionProjects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", "idontexist.csproj", projectToRemove, "idontexisteither.csproj");
            cmd.Should().Pass();

            string outputText = $@"{string.Format(CommonLocalizableStrings.ProjectNotFoundInTheSolution, "idontexist.csproj")}
{string.Format(CommonLocalizableStrings.ProjectRemovedFromTheSolution, projectToRemove)}
{string.Format(CommonLocalizableStrings.ProjectNotFoundInTheSolution, "idontexisteither.csproj")}";

            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);

            solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);
            solution.SolutionProjects.Count.Should().Be(1);
            solution.SolutionProjects.Single().FilePath.Should().Be(Path.Combine("App", "App.csproj"));
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenReferenceIsRemovedBuildConfigsAreAlsoRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);

            solution.SolutionProjects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();

            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemove{solutionExtension}");
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(templateContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenDirectoryContainingProjectIsGivenProjectIsRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);

            solution.SolutionProjects.Count.Should().Be(2);

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", "Lib");
            cmd.Should().Pass();

            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemove{solutionExtension}");
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(templateContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;
            var directoryToRemove = "Empty";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", directoryToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToRemove)));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;
            var directoryToRemove = "Multiple";

            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", directoryToRemove);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(
                string.Format(
                    CommonLocalizableStrings.MoreThanOneProjectInDirectory,
                    Path.Combine(projectDirectory, directoryToRemove)));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenReferenceIsRemovedSlnBuilds(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}{solutionExtension}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);

            solution.SolutionProjects.Count.Should().Be(2);

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore", $"App{solutionExtension}")
                .Should().Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("build", $"App{solutionExtension}", "--configuration", "Release", "/p:ProduceReferenceAssembly=false")
                .Should().Pass();

            var reasonString = "should be built in release mode, otherwise it means build configurations are missing from the sln file";

            var outputCalculator = OutputPathCalculator.FromProject(Path.Combine(projectDirectory, "App"));

            new DirectoryInfo(outputCalculator.GetOutputDirectory(configuration: "Debug")).Should().NotExist(reasonString);

            var outputDirectory = new DirectoryInfo(outputCalculator.GetOutputDirectory(configuration: "Release"));
            outputDirectory.Should().Exist();
            outputDirectory.Should().HaveFile("App.dll");
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenProjectIsRemovedSolutionHasUTF8BOM(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectToRemove = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "App.sln", "remove", projectToRemove);
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
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public async Task WhenFinalReferenceIsRemovedEmptySectionsAreRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath) ?? throw new InvalidOperationException($"Unable to get solution serializer for {solutionPath}.");
            SolutionModel solution = await serializer.OpenAsync(solutionPath, CancellationToken.None);
            solution.SolutionProjects.Count.Should().Be(2);

            var appPath = Path.Combine("App", "App.csproj");
            var libPath = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", libPath, appPath);
            cmd.Should().Pass();

            var solutionContents = File.ReadAllText(solutionPath);
            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemoveAllProjects{solutionExtension}");
            solutionContents.Should().BeVisuallyEquivalentTo(templateContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenNestedProjectIsRemovedItsSolutionFoldersAreRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojInSubDirToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var projectToRemove = Path.Combine("src", "NotLastProjInSrc", "NotLastProjInSrc.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();

            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemoveNestedProj{solutionExtension}");
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(templateContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenFinalNestedProjectIsRemovedSolutionFoldersAreRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndLastCsprojInSubDirToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var projectToRemove = Path.Combine("src", "Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();

            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemoveLastNestedProj{solutionExtension}");
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(templateContents);
        }

        [Theory]
        [InlineData("sln", ".sln")]
        [InlineData("solution", ".sln")]
        [InlineData("sln", ".slnx")]
        [InlineData("solution", ".slnx")]
        public void WhenProjectIsRemovedThenDependenciesOnProjectAreAlsoRemoved(string solutionCommand, string solutionExtension)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnProjectDependencyToRemove", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var solutionPath = Path.Combine(projectDirectory, $"App{solutionExtension}");

            var projectToRemove = Path.Combine("Second", "Second.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, $"App{solutionExtension}", "remove", projectToRemove);
            cmd.Should().Pass();

            var templateContents = GetSolutionFileTemplateContents($"ExpectedSlnContentsAfterRemoveProjectWithDependencies{solutionExtension}");
            File.ReadAllText(solutionPath)
                .Should().BeVisuallyEquivalentTo(templateContents);
        }

        [Theory]
        [InlineData("sln")]
        [InlineData("solution")]
        public void WhenSolutionIsPassedAsProjectItPrintsSuggestionAndUsage(string solutionCommand)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppWithSlnAndCsprojFiles", identifier: $"{solutionCommand}")
                .WithSource()
                .Path;

            var projectArg = Path.Combine("Lib", "Lib.csproj");
            var cmd = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute(solutionCommand, "remove", "App.sln", projectArg);
            cmd.Should().Fail();
            cmd.StdErr.Should().BeVisuallyEquivalentTo(
                string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, "App.sln") + Environment.NewLine
                + CommonLocalizableStrings.DidYouMean + Environment.NewLine
                 + $"  dotnet solution App.sln remove {projectArg}"
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
