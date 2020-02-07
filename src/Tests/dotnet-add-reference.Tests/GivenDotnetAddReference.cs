// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Msbuild.Tests.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Add.Reference.Tests
{
    public class GivenDotnetAddReference : SdkTest
    {
        private const string HelpText = @"Usage: dotnet add <PROJECT> reference [options] <PROJECT_PATH>

Arguments:
  <PROJECT>        The project file to operate on. If a file is not specified, the command will search the current directory for one.
  <PROJECT_PATH>   The paths to the projects to add as references.

Options:
  -h, --help                    Show command line help.
  -f, --framework <FRAMEWORK>   Add the reference only when targeting a specific framework.
  --interactive                 Allows the command to stop and wait for user input or action (for example to complete authentication).";

        private const string AddCommandHelpText = @"Usage: dotnet add [options] <PROJECT> [command]

Arguments:
  <PROJECT>   The project file to operate on. If a file is not specified, the command will search the current directory for one.

Options:
  -h, --help   Show command line help.

Commands:
  package <PACKAGE_NAME>     Add a NuGet package reference to the project.
  reference <PROJECT_PATH>   Add a project-to-project reference to the project.";

        const string FrameworkNet451 = "net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        const string FrameworkNetCoreApp10 = "netcoreapp1.0";
        const string ConditionFrameworkNetCoreApp10 = "== 'netcoreapp1.0'";
        static readonly string ProjectNotCompatibleErrorMessageRegEx = string.Format(CommonLocalizableStrings.ProjectNotCompatibleWithFrameworks, "[^`]*");
        static readonly string ProjectDoesNotTargetFrameworkErrorMessageRegEx = string.Format(CommonLocalizableStrings.ProjectDoesNotTargetFramework, "[^`]*", "[^`]*");
        static readonly string[] DefaultFrameworks = new string[] { "netcoreapp1.0", "net451" };

        public GivenDotnetAddReference(ITestOutputHelper log) : base(log)
        {
        }

        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                _testAssetsManager.CopyTestAsset(TestSetup.ProjectName, callingMethod, identifier, testAssetSubdirectory: TestSetup.TestGroup)
                    .WithSource()
                    .Path);
        }

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(_testAssetsManager.CreateTestDirectory(testName: callingMethod, identifier: identifier).Path);
        }

        private ProjDir NewLib(string dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var projDir = dir == null ? NewDir(callingMethod: callingMethod, identifier: identifier) : new ProjDir(dir);

            try
            {
                new DotnetCommand(Log, "new", "classlib", "-o", projDir.Path, "--debug:ephemeral-hive",  "--no-restore")
                    .WithWorkingDirectory(projDir.Path)
                    .Execute()
                .Should().Pass();
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new Exception($"Intermittent error in `dotnet new` occurred when running it in dir `{projDir.Path}`\nException:\n{e}");
            }

            return projDir;
        }

        private static void SetTargetFrameworks(ProjDir proj, string[] frameworks)
        {
            var csproj = proj.CsProj();
            csproj.AddProperty("TargetFrameworks", string.Join(";", frameworks));
            csproj.Save();
        }

        private ProjDir NewLibWithFrameworks(string dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var ret = NewLib(dir, callingMethod: callingMethod, identifier: identifier);
            SetTargetFrameworks(ret, DefaultFrameworks);
            return ret;
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log, "add", "reference").Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute($"add", commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CommonLocalizableStrings.RequiredCommandNotPassed);
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(AddCommandHelpText);
        }

        [Fact]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "add", "one", "two", "three", "reference")
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CommandLine.LocalizableStrings.UnrecognizedCommandOrArgument, "two")}
{string.Format(CommandLine.LocalizableStrings.UnrecognizedCommandOrArgument, "three")}");
        }

        [Theory]
        [InlineData("idontexist.csproj")]
        [InlineData("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsErrorAndUsage(string projName)
        {
            var setup = Setup();

            var cmd = new DotnetCommand(Log, "add", projName, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenBrokenProjectIsPassedItPrintsErrorAndUsage()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            var cmd = new DotnetCommand(Log, "add", projName, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.ProjectIsInvalid, "Broken/Broken.csproj"));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new DotnetCommand(Log, "add", "reference")
                    .WithWorkingDirectory(workingDir)
                    .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, workingDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var cmd = new DotnetCommand(Log, "add", "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, setup.TestRoot + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
        }

        [Fact]
        public void ItAddsRefWithoutCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);
            
            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsRefWithCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithoutCondIsPresentItAddsDifferentRefWithoutCond()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.LibCsprojPath)
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjName, "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondIsPresentItAddsDifferentRefWithCond()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.LibCsprojPath)
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj")); ;
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondIsPresentItAddsRefWithDifferentCond()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNetCoreApp10, setup.ValidRefCsprojPath)
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithConditionIsPresentItAddsDifferentRefWithoutCond()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.LibCsprojPath)
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithNoCondAlreadyExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath)
                .Should().Pass();

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjName, "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @"ValidRef\ValidRef.csproj"));

            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondOnItemAlreadyExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithExistingRefCondOnItem"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute("-f", FrameworkNet451, setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithCondOnItemGroupAlreadyExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath)
                .Should().Pass();

            var csprojContentBefore = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @"ValidRef\ValidRef.csproj"));
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [Fact]
        public void WhenRefWithCondWithWhitespaceOnItemGroupExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithExistingRefCondWhitespaces"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjName, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute("-f", FrameworkNet451, setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefNoCondNonUniform"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjName, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute(setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefNoCondNonUniform"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"..\ValidRef\ValidRef.csproj"));
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefCondNonUniform"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjName, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute("-f", FrameworkNet451, setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefCondNonUniform"));

            int condBefore = proj.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, "..\\ValidRef\\ValidRef.csproj"));
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenEmptyItemGroupPresentItAddsRefInIt()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "EmptyItemGroup"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"..\ValidRef\ValidRef.csproj"));
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsMultipleRefsNoCondToTheSameItemGroup()
        {
            string OutputText = $@"{string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"Lib\Lib.csproj")}
{string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj")}";

            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.LibCsprojPath, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.LibCsprojName).Should().Be(1);
        }

        [Fact]
        public void ItAddsMultipleRefsWithCondToTheSameItemGroup()
        {
            string OutputText = $@"{string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"Lib\Lib.csproj")}
{string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj")}";

            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.LibCsprojPath, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(OutputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.LibCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void WhenProjectNameIsNotPassedItFindsItAndAddsReference()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [Fact]
        public void WhenPassedReferenceDoesNotExistItShowsAnError()
        {
            var lib = NewLibWithFrameworks();

            var contentBefore = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjName, "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute("IDoNotExist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, "IDoNotExist.csproj"));
            lib.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenPassedMultipleRefsAndOneOfthemDoesNotExistItCancelsWholeOperation()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            var contentBefore = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath, "IDoNotExist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, "IDoNotExist.csproj"));
            lib.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [Fact]
        public void WhenPassedReferenceIsUsingSlashesItNormalizesItToBackslashes()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjName, "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojPath.Replace('\\', '/'));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojRelPath.Replace('/', '\\')).Should().Be(1);
        }

        [Fact]
        public void WhenReferenceIsRelativeAndProjectIsNotInCurrentDirectoryReferencePathIsFixed()
        {
            var setup = Setup();
            var proj = new ProjDir(setup.LibDir);

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", setup.LibCsprojPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"..\ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojRelToOtherProjPath.Replace('/', '\\')).Should().Be(1);
        }

        [Fact]
        public void ItCanAddReferenceWithConditionOnCompatibleFramework()
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .Execute("-f", FrameworkNet451, net45lib.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"..\Net45Lib\Net45Lib.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(net45lib.CsProjName, ConditionFrameworkNet451).Should().Be(1);
        }

        [Fact]
        public void ItCanAddRefWithoutCondAndTargetingSupersetOfFrameworksAndOneOfReferencesCompatible()
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net452netcoreapp10lib = new ProjDir(Path.Combine(setup.TestRoot, "Net452AndNetCoreApp10Lib"));

            int noCondBefore = net452netcoreapp10lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", net452netcoreapp10lib.CsProjPath, "reference")
                    .Execute(lib.CsProjPath);
            cmd.Should().Pass();
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"..\Lib\Lib.csproj"));
            var csproj = net452netcoreapp10lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(lib.CsProjName).Should().Be(1);
        }

        [Theory]
        [InlineData("net45")]
        [InlineData("net40")]
        [InlineData("netcoreapp1.1")]
        [InlineData("nonexistingframeworkname")]
        public void WhenFrameworkSwitchIsNotMatchingAnyOfTargetedFrameworksItPrintsError(string framework)
        {
            var setup = Setup(framework);
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            var csProjContent = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .Execute($"-f", framework, net45lib.CsProjPath);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CommonLocalizableStrings.ProjectDoesNotTargetFramework, setup.LibCsprojPath, framework));

            lib.CsProjContent().Should().BeEquivalentTo(csProjContent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WhenIncompatibleFrameworkDetectedItPrintsError(bool useFrameworkArg)
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            List<string> args = new List<string>();
            if (useFrameworkArg)
            {
                args.Add("-f");
                args.Add("net45");
            }
            args.Add(lib.CsProjPath);

            var csProjContent = net45lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", net45lib.CsProjPath, "reference")
                    .Execute(args);
            cmd.Should().Fail();
            cmd.StdErr.Should().MatchRegex(ProjectNotCompatibleErrorMessageRegEx);
            cmd.StdErr.Should().MatchRegex(" - net45");
            net45lib.CsProjContent().Should().BeEquivalentTo(csProjContent);
        }

        [Fact]
        public void WhenDirectoryContainingProjectIsGivenReferenceIsAdded()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var result = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(Path.GetDirectoryName(setup.ValidRefCsprojPath));

            result.Should().Pass();
            result.StdOut.Should().Be(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            result.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "Empty";
            var result = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
            result.StdErr.Should().Be(string.Format(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, reference));
        }

        [Fact]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "MoreThanOne";
            var result = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText);
            result.StdErr.Should().Be(string.Format(CommonLocalizableStrings.MoreThanOneProjectInDirectory, reference));
        }
    }
}
