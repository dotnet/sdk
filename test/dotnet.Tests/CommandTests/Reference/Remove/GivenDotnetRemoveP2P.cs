// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.FileBasedPrograms;
using Msbuild.Tests.Utilities;

namespace Microsoft.DotNet.Cli.Remove.Reference.Tests
{
    [TestClass]
    public class GivenDotnetRemoveReference : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove a project-to-project reference from the project.

Usage:
  dotnet remove <PROJECT | FILE> reference <PROJECT_PATH>... [options]

Arguments:
  <PROJECT | FILE>  The project file or C# file-based app to operate on. If a file is not specified, the command will search the current directory for a project file. [default: {PathUtilities.EnsureTrailingSlash(defaultVal)}]
  <PROJECT_PATH>    The paths to the referenced projects to remove.

Options:
  -f, --framework <FRAMEWORK>    Remove the reference only when targeting a specific framework.
  -?, -h, --help                 Show command line help.";

        private Func<string, string> RemoveCommandHelpText = (defaultVal) => $@"Description:
      .NET Remove Command
    
    Usage:
      dotnet remove <PROJECT | FILE> [command] [options]
    
    Arguments:
      <PROJECT | FILE>  The project file or C# file-based app to operate on. If a file is not specified, the command will search the current directory for a project file. [default: {PathUtilities.EnsureTrailingSlash(defaultVal)}]
    
    Options:
      -?, -h, --help    Show command line help.
    
    Commands:
      package <PACKAGE_NAME>      Remove a NuGet package reference from the project.
      reference <PROJECT_PATH>    Remove a project-to-project reference from the project.";

        readonly string[] FrameworkNet451Args = new[] { "-f", "net451" };
        const string ConditionFrameworkNet451 = "== 'net451'";
        readonly string[] CurrentFramework = new[] { "-f", ToolsetInfo.CurrentTargetFramework };
        const string ConditionCurrentFramework = $"== '{ToolsetInfo.CurrentTargetFramework}'";
        static readonly string[] DefaultFrameworks = new string[] { ToolsetInfo.CurrentTargetFramework, "net451" };

        public GivenDotnetRemoveReference()
        {
        }

        private TestSetup Setup([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(Setup), string identifier = "")
        {
            return new TestSetup(
                TestAssetsManager.CopyTestAsset(TestSetup.ProjectName, callingMethod: callingMethod + nameof(GivenDotnetRemoveReference), identifier: identifier + callingMethod, testAssetSubdirectory: TestAssetSubdirectories.NonRestoredTestProjects)
                    .WithSource()
                    .Path);
        }

        private ProjDir NewDir([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            return new ProjDir(TestAssetsManager.CreateTestDirectory(testName: callingMethod, identifier: identifier).Path);
        }

        private ProjDir NewLib(string dir = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = nameof(NewDir), string identifier = "")
        {
            var projDir = dir == null ? NewDir(callingMethod: callingMethod, identifier: identifier) : new ProjDir(dir);

            try
            {
                string[] newArgs = new[] { "classlib", "-o", projDir.Path, "--no-restore" };
                new DotnetNewCommand(Log)
                    .WithVirtualHive()
                    .WithWorkingDirectory(projDir.Path)
                    .Execute(newArgs)
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

        private ProjDir GetLibRef(TestSetup setup)
        {
            return new ProjDir(setup.LibDir);
        }

        private ProjDir AddLibRef(TestSetup setup, ProjDir proj, params string[] additionalArgs)
        {
            var ret = GetLibRef(setup);
            new AddReferenceCommand(Log)
                .WithProject(proj.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(additionalArgs.Concat(new[] { ret.CsProjPath }))
                .Should().Pass();

            return ret;
        }

        private ProjDir AddValidRef(TestSetup setup, ProjDir proj, params string[] frameworkArgs)
        {
            var ret = new ProjDir(setup.ValidRefDir);
            new AddReferenceCommand(Log)
                .WithProject(proj.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(frameworkArgs.Concat(new[] { ret.CsProjPath }))
                .Should().Pass();

            return ret;
        }

        private static string CreateFileBasedApp(string directory, string content)
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
            var cmd = new RemoveReferenceCommand(Log).Execute(helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [TestMethod]
        public void ItRejectsProjectPathPassedToFileOption_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var projectFile = CreateMinimalProject(testInstance.Path, "App");

            new DotnetCommand(Log, "reference", "remove", "Lib/Lib.csproj", "--file", projectFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidFilePath, projectFile));
        }

        [TestMethod]
        public void ItRejectsProjectAndFileOptions_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                Console.WriteLine();
                """);
            var projectFile = CreateMinimalProject(testInstance.Path, "App");

            new DotnetCommand(Log, "reference", "remove", "Lib/Lib.csproj", "--project", projectFile, "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdErrContaining(string.Format(CliCommandStrings.CannotCombineOptions, "--file", "--project"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            List<string> args = new();
            args.Add("remove");
            if (commandName != null)
            {
                args.Add(commandName);
            }

            var cmd = new DotnetCommand(Log)
                .Execute(args);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CliStrings.RequiredCommandNotPassed);
        }

        [TestMethod]
        public void WhenTooManyArgumentsArePassedItPrintsError()
        {
            var cmd = new DotnetCommand(Log, "add", "one", "two", "three", "reference", "proj.csproj")
                    .Execute();
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().BeVisuallyEquivalentTo($@"{string.Format(CliStrings.UnrecognizedCommandOrArgument, "two")}
{string.Format(CliStrings.UnrecognizedCommandOrArgument, "three")}");
        }

        [TestMethod]
        [DataRow("idontexist.csproj")]
        [DataRow("ihave?inv@lid/char\\acters")]
        public void WhenNonExistingProjectIsPassedItPrintsError(string projName)
        {
            var setup = Setup(identifier: projName.GetHashCode().ToString());

            var cmd = new RemoveReferenceCommand(Log)
                    .WithProject(projName)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
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

            var cmd = new RemoveReferenceCommand(Log)
                    .WithProject(projName)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(setup.ValidRefCsprojPath);
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CliStrings.ProjectIsInvalid, projName));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        public void WhenMoreThanOneProjectExistsInTheDirectoryItPrintsError()
        {
            var setup = Setup();

            var workingDir = Path.Combine(setup.TestRoot, "MoreThanOne");
            var cmd = new RemoveReferenceCommand(Log)
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

            var cmd = new RemoveReferenceCommand(Log)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute($"\"{setup.ValidRefCsprojPath}\"");
            cmd.ExitCode.Should().NotBe(0);
            cmd.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindAnyProjectInDirectory, setup.TestRoot + Path.DirectorySeparatorChar));
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
        }

        [TestMethod]
        public void ItRemovesRefWithoutCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [TestMethod]
        public void ItRemovesRefWithoutCondAndPrintsStatus_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project Lib/Lib.csproj
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            CreateMinimalProject(testInstance.Path, "Other");

            new DotnetCommand(Log, "reference", "remove", "Lib/Lib.csproj", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ProjectReferenceRemoved, "Lib/Lib.csproj"));

            File.ReadAllText(appFile).Should().Be("""
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItPreservesMSBuildPropertyProjectDirectiveWhenRemovingReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project $(MSBuildThisFileDirectory)Lib/Lib.csproj
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            CreateMinimalProject(testInstance.Path, "Other");

            new DotnetCommand(Log, "reference", "remove", "Other/Other.csproj", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ProjectReferenceRemoved, "Other/Other.csproj"));

            File.ReadAllText(appFile).Should().Be("""
                #:project $(MSBuildThisFileDirectory)Lib/Lib.csproj

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItRemovesMSBuildPropertyProjectDirectiveWhenRemovingReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project $(MSBuildThisFileDirectory)Lib/Lib.csproj
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            CreateMinimalProject(testInstance.Path, "Other");

            new DotnetCommand(Log, "reference", "remove", "Lib/Lib.csproj", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(
                    CliStrings.ProjectReferenceRemoved,
                    "$(MSBuildThisFileDirectory)Lib/Lib.csproj"));

            File.ReadAllText(appFile).Should().Be("""
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItRemovesFileBasedAppReferenceDirective_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref Util.cs
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
            File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
                #:property OutputType=Library
                public class Util { }
                """);
            CreateMinimalProject(testInstance.Path, "Other");

            new DotnetCommand(Log, "reference", "remove", "Util.cs", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ProjectReferenceRemoved, "Util.cs"));

            File.ReadAllText(appFile).Should().Be($$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItRemovesMSBuildPropertyRefDirectiveWhenRemovingReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref $(MSBuildThisFileDirectory)Util.cs
                #:ref Other.cs

                Console.WriteLine();
                """);
            File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
                #:property OutputType=Library
                public class Util { }
                """);
            File.WriteAllText(Path.Join(testInstance.Path, "Other.cs"), """
                #:property OutputType=Library
                public class Other { }
                """);

            new DotnetCommand(Log, "reference", "remove", "Util.cs", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOutContaining(string.Format(CliStrings.ProjectReferenceRemoved, "$(MSBuildThisFileDirectory)Util.cs"));

            File.ReadAllText(appFile).Should().Be($$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref Other.cs

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItRemovesRefWithCondAndPrintsStatus()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib, FrameworkNet451Args);

            int condBefore = lib.CsProj().NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { libref.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(libref.Name, ConditionFrameworkNet451).Should().Be(0);
        }

        [TestMethod]
        public void WhenTwoDifferentRefsArePresentItDoesNotRemoveBoth()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib);
            var validref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenRefWithoutCondIsNotThereItPrintsMessage()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = GetLibRef(setup);

            string csprojContentBefore = lib.CsProjContent();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceCouldNotBeFound, libref.CsProjPath));
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [TestMethod]
        public void WhenRefWithoutCondIsNotThereItPrintsMessage_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project Other/Other.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Other");
            var contentBefore = File.ReadAllText(appFile);

            var cmd = new DotnetCommand(Log, "reference", "remove", "Lib/Lib.csproj", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceCouldNotBeFound, "Lib/Lib.csproj"));
            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        [DataRow("Missing")]
        [DataRow("missing")]
        public void ItRemovesProjectReferenceDirectiveWhenReferencedProjectDoesNotExist_FileBasedApp(string referenceArgument)
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project Missing

                Console.WriteLine();
                """);

            var cmd = new DotnetCommand(Log, "reference", "remove", referenceArgument, "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, "Missing"));
            File.ReadAllText(appFile).Should().Be("""
                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void ItPreservesFileBasedAppReferenceDirectiveWhenRemovingMissingProjectReference_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:project Missing
                #:ref MissingRef

                Console.WriteLine();
                """);

            var cmd = new DotnetCommand(Log, "reference", "remove", "Missing", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, "Missing"));
            File.ReadAllText(appFile).Should().Be($$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref MissingRef

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void WhenFileBasedAppReferenceWithoutExistingFileIsNotThereItPrintsMessage_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true

                Console.WriteLine();
                """);
            var contentBefore = File.ReadAllText(appFile);

            var cmd = new DotnetCommand(Log, "reference", "remove", "Missing.cs", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceCouldNotBeFound, "Missing.cs"));
            File.ReadAllText(appFile).Should().Be(contentBefore);
        }

        [TestMethod]
        [DataRow("Missing")]
        [DataRow("missing")]
        public void ItRemovesFileBasedAppReferenceDirectiveWhenReferencedFileDoesNotExist_FileBasedApp(string referenceArgument)
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, $$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true
                #:ref Missing

                Console.WriteLine();
                """);

            var cmd = new DotnetCommand(Log, "reference", "remove", referenceArgument, "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, "Missing"));
            File.ReadAllText(appFile).Should().Be($$"""
                #:property {{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}}=true

                Console.WriteLine();
                """);
        }

        [TestMethod]
        public void WhenRefWithCondIsNotThereItPrintsMessage()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = GetLibRef(setup);

            string csprojContentBefore = lib.CsProjContent();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { libref.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceCouldNotBeFound, libref.CsProjPath));
            lib.CsProjContent().Should().BeEquivalentTo(csprojContentBefore);
        }

        [TestMethod]
        public void WhenRefWithAndWithoutCondArePresentAndRemovingNoCondItDoesNotRemoveOther()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var librefCond = AddLibRef(setup, lib, FrameworkNet451Args);
            var librefNoCond = AddLibRef(setup, lib);

            var csprojBefore = lib.CsProj();
            int noCondBefore = csprojBefore.NumberOfItemGroupsWithoutCondition();
            int condBefore = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(librefNoCond.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(librefNoCond.Name).Should().Be(0);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCond.Name, ConditionFrameworkNet451).Should().Be(1);
        }

        [TestMethod]
        public void WhenRefWithAndWithoutCondArePresentAndRemovingCondItDoesNotRemoveOther()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var librefCond = AddLibRef(setup, lib, FrameworkNet451Args);
            var librefNoCond = AddLibRef(setup, lib);

            var csprojBefore = lib.CsProj();
            int noCondBefore = csprojBefore.NumberOfItemGroupsWithoutCondition();
            int condBefore = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { librefCond.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore);
            csproj.NumberOfProjectReferencesWithIncludeContaining(librefNoCond.Name).Should().Be(1);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCond.Name, ConditionFrameworkNet451).Should().Be(0);
        }

        [TestMethod]
        public void WhenRefWithDifferentCondIsPresentItDoesNotRemoveIt()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var librefCondNet451 = AddLibRef(setup, lib, FrameworkNet451Args);
            var librefCondNetCoreApp10 = AddLibRef(setup, lib, CurrentFramework);

            var csprojBefore = lib.CsProj();
            int condNet451Before = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451);
            int condNetCoreApp10Before = csprojBefore.NumberOfItemGroupsWithConditionContaining(ConditionCurrentFramework);
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(FrameworkNet451Args.Concat(new[] { librefCondNet451.CsProjPath }));
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithConditionContaining(ConditionFrameworkNet451).Should().Be(condNet451Before - 1);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCondNet451.Name, ConditionFrameworkNet451).Should().Be(0);

            csproj.NumberOfItemGroupsWithConditionContaining(ConditionCurrentFramework).Should().Be(condNetCoreApp10Before);
            csproj.NumberOfProjectReferencesWithIncludeAndConditionContaining(librefCondNetCoreApp10.Name, ConditionCurrentFramework).Should().Be(1);
        }

        [TestMethod]
        public void WhenDuplicateReferencesArePresentItRemovesThemAll()
        {
            var setup = Setup();
            var proj = new ProjDir(Path.Combine(setup.TestRoot, "WithDoubledRef"));
            var libref = GetLibRef(setup);

            string removedText = $@"{string.Format(CliStrings.ProjectReferenceRemoved, setup.LibCsprojRelPath)}
{string.Format(CliStrings.ProjectReferenceRemoved, setup.LibCsprojRelPath)}";

            int noCondBefore = proj.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(proj.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(removedText);

            var csproj = proj.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenDuplicateReferencesArePresentItRemovesThemAll_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project Lib/Lib.csproj
                #:project Lib/Lib.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            var outputText = $@"{string.Format(CliStrings.ProjectReferenceRemoved, "Lib/Lib.csproj")}
{string.Format(CliStrings.ProjectReferenceRemoved, "Lib/Lib.csproj")}";

            var cmd = new DotnetCommand(Log, "reference", "remove", Path.GetFullPath("Lib/Lib.csproj", testInstance.Path), "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            File.ReadAllText(appFile).Should().NotContain("#:project");
            File.ReadAllText(appFile).Should().Contain("Console.WriteLine();");
        }

        [TestMethod]
        public void WhenPassingRefWithRelPathItRemovesRefWithAbsolutePath()
        {
            var setup = Setup();
            var lib = GetLibRef(setup);
            var libref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(lib.Path)
                .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, setup.ValidRefCsprojRelToOtherProjPath));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenPassingRefWithRelPathToProjectItRemovesRefWithPathRelToProject()
        {
            var setup = Setup();
            var lib = GetLibRef(setup);
            var libref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojRelToOtherProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, setup.ValidRefCsprojRelToOtherProjPath));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenPassingRefWithAbsolutePathItRemovesRefWithRelPath()
        {
            var setup = Setup();
            var lib = GetLibRef(setup);
            var libref = AddValidRef(setup, lib);

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(setup.ValidRefCsprojPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, setup.ValidRefCsprojRelToOtherProjPath));
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenPassingMultipleReferencesItRemovesThemAll()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = AddLibRef(setup, lib);
            var validref = AddValidRef(setup, lib);

            string outputText = $@"{string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName))}
{string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine(setup.ValidRefCsprojRelPath))}";

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath, validref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(libref.Name).Should().Be(0);
            csproj.NumberOfProjectReferencesWithIncludeContaining(validref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenPassingMultipleReferencesItRemovesThemAll_FileBasedApp()
        {
            var testInstance = TestAssetsManager.CreateTestDirectory();
            var appFile = CreateFileBasedApp(testInstance.Path, """
                #:project Lib/Lib.csproj
                #:project ValidRef/ValidRef.csproj

                Console.WriteLine();
                """);
            CreateMinimalProject(testInstance.Path, "Lib");
            CreateMinimalProject(testInstance.Path, "ValidRef");
            var outputText = $@"{string.Format(CliStrings.ProjectReferenceRemoved, "Lib/Lib.csproj")}
{string.Format(CliStrings.ProjectReferenceRemoved, "ValidRef/ValidRef.csproj")}";

            var cmd = new DotnetCommand(Log, "reference", "remove", "Lib/Lib.csproj", "ValidRef/ValidRef.csproj", "--file", appFile)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();

            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            File.ReadAllText(appFile).Should().NotContain("#:project");
            File.ReadAllText(appFile).Should().Contain("Console.WriteLine();");
        }

        [TestMethod]
        public void WhenPassingMultipleReferencesAndOneOfThemDoesNotExistItRemovesOne()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(setup.TestRoot);
            var libref = GetLibRef(setup);
            var validref = AddValidRef(setup, lib);

            string outputText = $@"{string.Format(CliStrings.ProjectReferenceCouldNotBeFound, setup.LibCsprojPath)}
{string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine(setup.ValidRefCsprojRelPath))}";

            int noCondBefore = lib.CsProj().NumberOfItemGroupsWithoutCondition();
            var cmd = new RemoveReferenceCommand(Log)
                .WithProject(lib.CsProjPath)
                .WithWorkingDirectory(setup.TestRoot)
                .Execute(libref.CsProjPath, validref.CsProjPath);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentTo(outputText);
            var csproj = lib.CsProj();
            csproj.NumberOfItemGroupsWithoutCondition().Should().Be(noCondBefore - 1);
            csproj.NumberOfProjectReferencesWithIncludeContaining(validref.Name).Should().Be(0);
        }

        [TestMethod]
        public void WhenDirectoryContainingProjectIsGivenReferenceIsRemoved()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);
            var libref = AddLibRef(setup, lib);

            var result = new RemoveReferenceCommand(Log)
                    .WithProject(lib.CsProjPath)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(libref.CsProjPath);

            result.Should().Pass();
            result.StdOut.Should().Be(string.Format(CliStrings.ProjectReferenceRemoved, Path.Combine("Lib", setup.LibCsprojName)));
            result.StdErr.Should().BeEmpty();
        }

        [TestMethod]
        public void WhenDirectoryContainsNoProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "Empty";
            var result = new RemoveReferenceCommand(Log)
                    .WithProject(lib.CsProjPath)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            result.StdErr.Should().Be(string.Format(CliStrings.CouldNotFindAnyProjectInDirectory, Path.Combine(setup.TestRoot, reference)));
        }

        [TestMethod]
        public void WhenDirectoryContainsMultipleProjectsItCancelsWholeOperation()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);

            var reference = "MoreThanOne";
            var result = new RemoveReferenceCommand(Log)
                    .WithProject(lib.CsProjPath)
                    .WithWorkingDirectory(setup.TestRoot)
                    .Execute(reference);

            result.Should().Fail();
            result.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized("");
            result.StdErr.Should().Be(string.Format(CliStrings.MoreThanOneProjectInDirectory, Path.Combine(setup.TestRoot, reference)));
        }

        [TestMethod]
        public void WhenNoProjectIsSpecifiedItUsesCurrentDirectory()
        {
            var setup = Setup();
            var lib = NewLibWithFrameworks(dir: setup.TestRoot);
            var libref = AddLibRef(setup, lib);

            // Reproduces: dotnet reference remove ../Lib/Lib.csproj
            // where the current directory contains a project (no --project argument needed)
            var result = new DotnetCommand(Log, "reference", "remove")
                    .WithWorkingDirectory(lib.Path)
                    .Execute(libref.CsProjPath);

            result.Should().Pass();
            result.StdErr.Should().BeEmpty();
        }
    }
}
