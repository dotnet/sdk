// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Msbuild.Tests.Utilities;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Cli.Add.Reference.Tests
{
    [TestClass]
    public class GivenDotnetAddReference : SdkTest
    {
        private Func<string, string, string> HelpText = (defaultVal, frameworkVal) => $@"Description:
  Add a project-to-project reference to the project.

Usage:
  dotnet reference add <PROJECT_PATH>... [options]

Arguments:
  <PROJECT_PATH>  The paths to the projects to add as references.

Options:
  -f, --framework <FRAMEWORK>  Add the reference only when targeting a specific framework.
  --interactive                Allows the command to stop and wait for user input or action (for example to complete authentication). [default: False]
  --file <file>                The file-based app to operate on.
  --project                    The project file to operate on. If a file is not specified, the command will search the current directory for one.
  -?, -h, --help               Show command line help.";

        private Func<string, string> AddCommandHelpText = (defaultVal) => $@"Description:
  .NET Remove Command

Usage:
  dotnet reference [command] [options]

Options:
  --project <project>  The project file to operate on. If a file is not specified, the command will search the current
                       directory for one.
  -?, -h, --help       Show command line help.

Commands:
  add <PROJECT_PATH>     Add a project-to-project reference to the project.
  list                   List all project-to-project references of the project.
  remove <PROJECT_PATH>  Remove a project-to-project reference from the project.";

        const string FrameworkNet451 = "net451";
        const string ConditionFrameworkNet451 = "== 'net451'";
        static readonly string ProjectNotCompatibleErrorMessageRegEx = string.Format(CliStrings.ProjectNotCompatibleWithFrameworks, "[^`]*");
        static readonly string ProjectDoesNotTargetFrameworkErrorMessageRegEx = string.Format(CliStrings.ProjectDoesNotTargetFramework, "[^`]*", "[^`]*");
        static readonly string[] DefaultFrameworks = new string[] { ToolsetInfo.CurrentTargetFramework, "net451" };

        public GivenDotnetAddReference()
        {
        }

        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                TestAssetsManager.CopyTestAsset(TestSetup.ProjectName, callingMethod + nameof(GivenDotnetAddReference), identifier: identifier + callingMethod, testAssetSubdirectory: TestSetup.TestGroup)
                    .WithSource()
                    .Path);
        }

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(TestAssetsManager.CreateTestDirectory(testName: callingMethod, identifier: identifier).Path);
        }

        private ProjDir NewLib(string? dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var projDir = dir == null ? NewDir(callingMethod: callingMethod, identifier: identifier) : new ProjDir(dir);

            try
            {
                new DotnetNewCommand(Log, "classlib", "-o", projDir.Path, "--no-restore")
                    .WithVirtualHive()
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

        private ProjDir NewLibWithFrameworks(string? dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var ret = NewLib(dir, callingMethod: callingMethod, identifier: identifier);
            SetTargetFrameworks(ret, DefaultFrameworks);
            return ret;
        }

        private static string CreateFileBasedApp(string directory, string content = "Console.WriteLine();")
        {
            var appFile = Path.Join(directory, "Program.cs");
            File.WriteAllText(appFile, content);
            return appFile;
        }

        private static string CreateMinimalProject(string directory, string projectName)
        {
            var projectDirectory = Path.Join(directory, projectName);
            Directory.CreateDirectory(projectDirectory);
            var projectFile = Path.Join(projectDirectory, projectName + ".csproj");
            File.WriteAllText(projectFile, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    </PropertyGroup>
                </Project>
                """);
            return projectFile;
        }

        [TestMethod]
        [DataRow("--help")]
        [DataRow("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log, "reference", "add").Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory(), "FRAMEWORK"));
        }

        [TestMethod]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            if (!File.Exists("proj.csproj"))
            {
                File.Create("proj.csproj");
            }
            var cmd = new DotnetCommand(Log, "add", "one", "two", "three", "reference")
                    .Execute("proj.csproj");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CliStrings.UnrecognizedCommandOrArgument, "two")}
{string.Format(CliStrings.UnrecognizedCommandOrArgument, "three")}");
        }

        [TestMethod]
        [DataRow("idontexist.csproj")]
        [DataRow("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsError(string projName)
        {
            var setup = Setup(identifier: projName);

            var cmd = new DotnetCommand(Log, "add", projName, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindProjectOrDirectory, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        public void WhenBrokenProjectIsPassedItPrintsError()
        {
            string projName = "Broken/Broken.csproj";
            var setup = Setup();

            string brokenFolder = Path.Combine(setup.TestRoot, "Broken");
            Directory.CreateDirectory(brokenFolder);
            string brokenProjectPath = Path.Combine(brokenFolder, "Broken.csproj");
            File.WriteAllText(brokenProjectPath, $@"<Project Sdk=""Microsoft.NET.Sdk"" ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <TargetFrameworks>net451;{ToolsetInfo.CurrentTargetFramework}</TargetFrameworks>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include=""**\*.cs""/>
        <EmbeddedResource Include=""**\*.resx""/>
    <!--intentonally broken-->");

            var cmd = new DotnetCommand(Log, "add", projName, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"{setup.ValidRefCsprojPath}");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CliStrings.ProjectIsInvalid, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsErrorAndUsage()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new DotnetCommand(Log, "add", "reference")
                    .WithWorkingDirectory(workingDir)
                    .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CliStrings.MoreThanOneProjectInDirectory, workingDir + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        public void WhenNoProjectsExistsInTheDirectoryItPrintsError()
        {
            var setup = Setup();

            var cmd = new DotnetCommand(Log, "add", "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindAnyProjectInDirectory, setup.TestRoot + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        public void ItFailsToAddInvalidRefWithProperlyFormattedError()
        {
            var invalidProjDirectory = Path.Combine(TestAssetsManager.CreateTestDirectory().Path, "InvalidProj");
            var invalidProjPath = Path.Combine(invalidProjDirectory, "InvalidProj.csproj");
            Directory.CreateDirectory(invalidProjDirectory);
            File.WriteAllText(invalidProjPath, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
  </PropertyGroup>
  <Import Project=""fake.props"" />
</Project>");

            var cmd = new DotnetCommand(Log, "add", "reference", invalidProjPath)
                .WithWorkingDirectory(invalidProjDirectory)
                .Execute();
            cmd.Should().Fail();
            cmd.StdErr.Should().Contain(string.Format(
                CliStrings.ProjectCouldNotBeEvaluated.Substring(0, CliStrings.ProjectCouldNotBeEvaluated.Length - 4), // Remove the '{0}.' from the end
                invalidProjPath));
            cmd.StdErr.Should().NotContain("Microsoft.DotNet.Cli.Utils.GracefulException");
        }

        [TestMethod]
        public void ItAddsRefWithoutCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
        public void ItAddsRefWithoutCondAndPrintsStatus_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path);
            CreateMinimalProject(testInstance.Path, "Lib");

            new DotnetCommand(Log, "reference", "add", "Lib", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ReferenceAddedToTheProject, "Lib"));

            File.ReadAllText(appFile).Should().Be("""
                #:project Lib

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItAddsRefWithoutCondAndPrintsStatus_LegacyForm_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path);
            CreateMinimalProject(testInstance.Path, "Lib");

            new DotnetCommand(Log, "add", "reference", "Lib", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ReferenceAddedToTheProject, "Lib"));

            File.ReadAllText(appFile).Should().Be("""
                #:project Lib

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItAddsFileBasedAppReferenceDirective_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true

                Console.WriteLine();
                """);
            File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
                #:property OutputType=Library
                public class Util { }
                """);

            new DotnetCommand(Log, "reference", "add", "Util.cs", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ReferenceAddedToTheProject, "Util.cs"));

            File.ReadAllText(appFile).Should().Be($$"""
                #:ref Util.cs
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void WhenFileBasedAppReferenceAlreadyExistsItDoesntDuplicate_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref Util.cs

                Console.WriteLine();
                """);
            File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
                #:property OutputType=Library
                public class Util { }
                """);
            var contentBefore = File.ReadAllText(appFile);

            new DotnetCommand(Log, "reference", "add", "Util.cs", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ProjectAlreadyHasAreference, "Util.cs"));

            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        public void ItMatchesMSBuildPropertyRefDirectiveWhenAddingReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref $(MSBuildThisFileDirectory)Util.cs

                Console.WriteLine();
                """);
            File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
                #:property OutputType=Library
                public class Util { }
                """);
            var contentBefore = File.ReadAllText(appFile);

            new DotnetCommand(Log, "reference", "add", "Util.cs", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ProjectAlreadyHasAreference, "$(MSBuildThisFileDirectory)Util.cs"));

            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        public void ItPreservesMSBuildPropertyProjectDirectiveWhenAddingReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project $(MSBuildThisFileDirectory)Lib/Lib.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            CreateMinimalProject(testInstance.Path, "Other");

            new DotnetCommand(Log, "reference", "add", "Other", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ReferenceAddedToTheProject, "Other"));

            File.ReadAllText(appFile).Should().Be("""
                #:project $(MSBuildThisFileDirectory)Lib/Lib.csproj
                #:project Other

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItPreservesDirectoryProjectDirectiveWhenAddingReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project Lib

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            CreateMinimalProject(testInstance.Path, "Other");

            new DotnetCommand(Log, "reference", "add", "Other", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ReferenceAddedToTheProject, "Other"));

            File.ReadAllText(appFile).Should().Be("""
                #:project Lib
                #:project Other

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItAddsRefWithCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [TestMethod]
        public void ItRejectsFrameworkOption_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var originalContent = """
                Console.WriteLine();
                """;
            var appFile = CreateFileBasedApp(testInstance.Path);
            CreateMinimalProject(testInstance.Path, "Lib");

            new DotnetCommand(Log, "reference", "add", "Lib", "--file", "Program.cs", "--framework", ToolsetInfo.CurrentTargetFramework)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidOptionForFileBasedApp, "--framework"));

            File.ReadAllText(appFile).Should().Be(originalContent);
        }

        [TestMethod]
        public void ItRejectsProjectPathPassedToFileOption_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var projectFile = CreateMinimalProject(testInstance.Path, "App");
            CreateMinimalProject(testInstance.Path, "Lib");

            new DotnetCommand(Log, "reference", "add", "Lib", "--file", projectFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFilePath, projectFile));
        }

        [TestMethod]
        public void ItRejectsProjectAndFileOptions_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path);
            var projectFile = CreateMinimalProject(testInstance.Path, "App");
            CreateMinimalProject(testInstance.Path, "Lib");

            new DotnetCommand(Log, "reference", "add", "Lib", "--project", projectFile, "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.CannotCombineOptions, "--file", "--project"));
        }

        [TestMethod]
        public void ItRejectsProjectAndFileOptions_LegacyForm_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path);
            var projectFile = CreateMinimalProject(testInstance.Path, "App");
            CreateMinimalProject(testInstance.Path, "Lib");

            new DotnetCommand(Log, "add", "reference", "Lib", "--project", projectFile, "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.CannotCombineOptions, "--file", "--project"));
        }

        [TestMethod]
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
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
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
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj")); ;
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [TestMethod]
        public void WhenRefWithCondIsPresentItAddsRefWithDifferentCond()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", ToolsetInfo.CurrentTargetFramework, setup.ValidRefCsprojPath)
                .Should().Pass();

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [TestMethod]
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
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
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
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, @"ValidRef\ValidRef.csproj"));

            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
        public void WhenRefWithNoCondAlreadyExistsItDoesntDuplicate_FileBasedApp()
        {
            var setup = Setup();
            var appFile = CreateFileBasedApp(setup.TestRoot, """
                #:project ValidRef/ValidRef.csproj

                Console.WriteLine();
                """);
            var contentBefore = File.ReadAllText(appFile);

            var cmd = new DotnetCommand(Log, "reference", "add", setup.ValidRefCsprojPath, "--file", appFile)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, "ValidRef/ValidRef.csproj"));
            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        public void WhenRefWithCondOnItemAlreadyExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithExistingRefCondOnItem"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute("-f", FrameworkNet451, setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [TestMethod]
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
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, @"ValidRef\ValidRef.csproj"));
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [TestMethod]
        public void WhenRefWithCondWithWhitespaceOnItemGroupExistsItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithExistingRefCondWhitespaces"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjName, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute("-f", FrameworkNet451, setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [TestMethod]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefNoCondNonUniform"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjName, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute(setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [TestMethod]
        public void WhenRefWithoutCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefNoCondNonUniform"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"..\ValidRef\ValidRef.csproj"));
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItDoesntDuplicate()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefCondNonUniform"));

            string contentBefore = proj.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjName, "reference")
                    .WithWorkingDirectory(proj.Path)
                    .Execute("-f", FrameworkNet451, setup.LibCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectAlreadyHasAreference, @"..\Lib\Lib.csproj"));
            proj.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [TestMethod]
        public void WhenRefWithCondAlreadyExistsInNonUniformItemGroupItAddsDifferentRefInDifferentGroup()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithRefCondNonUniform"));

            int condBefore = proj.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute("-f", FrameworkNet451, setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, "..\\ValidRef\\ValidRef.csproj"));
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(setup.ValidRefCsprojName, ConditionFrameworkNet451).Should().Be(1);
        }

        [TestMethod]
        public void WhenEmptyItemGroupPresentItAddsRefInIt()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "EmptyItemGroup"));

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", proj.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"..\ValidRef\ValidRef.csproj"));
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
        public void ItAddsMultipleRefsNoCondToTheSameItemGroup()
        {
            string OutputText = $@"{string.Format(CliStrings.ReferenceAddedToTheProject, @"Lib\Lib.csproj")}
{string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj")}";

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

        [TestMethod]
        public void ItAddsMultipleRefsNoCondToTheSameItemGroup_FileBasedApp()
        {
            var outputText = $@"{string.Format(CliStrings.ReferenceAddedToTheProject, "Lib/Lib.csproj")}
{string.Format(CliStrings.ReferenceAddedToTheProject, "ValidRef/ValidRef.csproj")}";

            var setup = Setup();
            var appFile = CreateFileBasedApp(setup.TestRoot);

            var cmd = new DotnetCommand(Log, "reference", "add", setup.LibCsprojPath, setup.ValidRefCsprojPath, "--file", appFile)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            File.ReadAllText(appFile).Should().Be("""
                #:project Lib/Lib.csproj
                #:project ValidRef/ValidRef.csproj

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItAddsMultipleRefsWithCondToTheSameItemGroup()
        {
            string OutputText = $@"{string.Format(CliStrings.ReferenceAddedToTheProject, @"Lib\Lib.csproj")}
{string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj")}";

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

        [TestMethod]
        public void WhenProjectNameIsNotPassedItFindsItAndAddsReference()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojName).Should().Be(1);
        }

        [TestMethod]
        public void WhenPassedReferenceDoesNotExistItShowsAnError()
        {
            var lib = NewLibWithFrameworks();

            var contentBefore = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjName, "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute("IDoNotExist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindProjectOrDirectory, "IDoNotExist.csproj"));
            lib.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [TestMethod]
        public void WhenPassedReferenceDoesNotExistItShowsAnError_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path);
            var contentBefore = File.ReadAllText(appFile);

            var cmd = new DotnetCommand(Log, "reference", "add", "IDoNotExist.csproj", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindProjectOrDirectory, "IDoNotExist.csproj"));
            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        public void WhenPassedMultipleRefsAndOneOfthemDoesNotExistItCancelsWholeOperation()
        {
            var lib = NewLibWithFrameworks();
            var setup = Setup();

            var contentBefore = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath, "IDoNotExist.csproj");
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindProjectOrDirectory, "IDoNotExist.csproj"));
            lib.CsProjContent().Should().BeEquivalentTo(contentBefore);
        }

        [TestMethod]
        public void WhenPassedMultipleRefsAndOneOfthemDoesNotExistItCancelsWholeOperation_FileBasedApp()
        {
            var setup = Setup();
            var appFile = CreateFileBasedApp(setup.TestRoot);
            var contentBefore = File.ReadAllText(appFile);

            var cmd = new DotnetCommand(Log, "reference", "add", setup.ValidRefCsprojPath, "IDoNotExist.csproj", "--file", appFile)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute();

            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindProjectOrDirectory, "IDoNotExist.csproj"));
            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        public void WhenPassedReferenceIsUsingSlashesItNormalizesItToBackslashes()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjName, "reference")
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojPath.Replace('\\', '/'));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojRelPath.Replace('/', '\\')).Should().Be(1);
        }

        [TestMethod]
        public void WhenReferenceIsRelativeAndProjectIsNotInCurrentDirectoryReferencePathIsFixed()
        {
            var setup = Setup();
            var proj = new ProjDir(setup.LibDir);

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new DotnetCommand(Log, "add", setup.LibCsprojPath, "reference")
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojRelPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"..\ValidRef\ValidRef.csproj"));
            cmd.StdErr.Should().BeEmpty();
            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(setup.ValidRefCsprojRelToOtherProjPath.Replace('/', '\\')).Should().Be(1);
        }

        [TestMethod]
        public void WhenReferenceIsRelativeAndProjectIsNotInCurrentDirectoryReferencePathIsFixed_FileBasedApp()
        {
            var setup = Setup();
            var appDirectory = Path.Join(setup.TestRoot, "App");
            Directory.CreateDirectory(appDirectory);
            var appFile = CreateFileBasedApp(appDirectory);

            var cmd = new DotnetCommand(Log, "reference", "add", setup.ValidRefCsprojRelPath, "--file", appFile)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, "../ValidRef/ValidRef.csproj"));
            File.ReadAllText(appFile).Should().Be("""
                #:project ../ValidRef/ValidRef.csproj

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItCanAddReferenceWithConditionOnCompatibleFramework()
        {
            var setup = Setup();
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .Execute("-f", FrameworkNet451, net45lib.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"..\Net45Lib\Net45Lib.csproj"));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(net45lib.CsProjName, ConditionFrameworkNet451).Should().Be(1);
        }

        [TestMethod]
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
            cmd.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"..\Lib\Lib.csproj"));
            var csproj = net452netcoreapp10lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore + 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(lib.CsProjName).Should().Be(1);
        }

        [TestMethod]
        [DataRow("net45")]
        [DataRow("net40")]
        [DataRow(ToolsetInfo.CurrentTargetFramework)]
        [DataRow("nonexistingframeworkname")]
        public void WhenFrameworkSwitchIsNotMatchingAnyOfTargetedFrameworksItPrintsError(string framework)
        {
            var setup = Setup(framework);
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            var csProjContent = lib.CsProjContent();
            var cmd = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .Execute($"-f", framework, net45lib.CsProjPath);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(string.Format(CliStrings.ProjectDoesNotTargetFramework, setup.LibCsprojPath, framework));

            lib.CsProjContent().Should().BeEquivalentTo(csProjContent);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WhenIncompatibleFrameworkDetectedItPrintsError(bool useFrameworkArg)
        {
            var setup = Setup(useFrameworkArg.ToString());
            var lib = new ProjDir(setup.LibDir);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));

            List<string> args = new();
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

        [TestMethod]
        public void WhenIncompatibleFrameworkDetectedItPrintsError_FileBasedApp()
        {
            var setup = Setup();
            var appFile = CreateFileBasedApp(setup.TestRoot);
            var net45lib = new ProjDir(Path.Combine(setup.TestRoot, "Net45Lib"));
            var sourceBefore = File.ReadAllText(appFile);

            var cmd = new DotnetCommand(Log, "reference", "add", net45lib.CsProjPath, "--file", appFile)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute();

            cmd.Should().Fail();
            cmd.StdErr.Should().MatchRegex(ProjectNotCompatibleErrorMessageRegEx);
            cmd.StdErr.Should().MatchRegex($" - {ToolsetInfo.CurrentTargetFramework}");
            File.ReadAllText(appFile).Should().Be(sourceBefore);
        }

        [TestMethod]
        public void WhenDirectoryContainingProjectIsGivenReferenceIsAdded()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var result = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(Path.GetDirectoryName(setup.ValidRefCsprojPath) ?? string.Empty);

            result.Should().Pass();
            result.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, @"ValidRef\ValidRef.csproj"));
            result.StdErr.Should().BeEmpty();
        }

        [TestMethod]
        public void WhenDirectoryContainingProjectIsGivenReferenceIsAdded_FileBasedApp()
        {
            var setup = Setup();
            var appFile = CreateFileBasedApp(setup.TestRoot);

            var result = new DotnetCommand(Log, "reference", "add", Path.GetDirectoryName(setup.ValidRefCsprojPath) ?? string.Empty, "--file", appFile)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute();

            result.Should().Pass();
            result.StdOut.Should().Be(string.Format(CliStrings.ReferenceAddedToTheProject, "ValidRef"));
            result.StdErr.Should().BeEmpty();
            File.ReadAllText(appFile).Should().Contain("#:project ValidRef");
        }

        [TestMethod]
        public void WhenNoProjectIsSpecifiedItUsesCurrentDirectory()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            // Reproduces: dotnet reference add ../ValidRef/ValidRef.csproj
            // where the current directory contains a project (no --project argument needed)
            var result = new DotnetCommand(Log, "reference", "add")
                    .WithWorkingDirectory(lib.Path)
                    .Execute(setup.ValidRefCsprojPath);

            result.Should().Pass();
            result.StdErr.Should().BeEmpty();
        }

        [TestMethod]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "Empty";
            var result = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            result.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindAnyProjectInDirectory, reference));
        }

        [TestMethod]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "MoreThanOne";
            var result = new DotnetCommand(Log, "add", lib.CsProjPath, "reference")
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            result.StdErr.Should().Be(string.Format(CliStrings.MoreThanOneProjectInDirectory, reference));
        }
    }
}
