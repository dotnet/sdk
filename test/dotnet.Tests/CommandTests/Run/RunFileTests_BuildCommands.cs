// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests_BuildCommands(ITestOutputHelper log) : RunFileTestBase(log)
{

    [Fact]
    public void Restore_NonExistentPackage()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, """
            #:package Microsoft.ThisPackageDoesNotExist@1.0.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("Program.cs.csproj : error NU1101");
    }

    [Fact]
    public void NoRestore_01()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        // It is an error when never restored before.
        new DotnetCommand(Log, "run", "--no-restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("NETSDK1004"); // error NETSDK1004: Assets file '...\obj\project.assets.json' not found. Run a NuGet package restore to generate this file.

        // Run restore.
        new DotnetCommand(Log, "restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // --no-restore works.
        new DotnetCommand(Log, "run", "--no-restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");
    }

    [Fact]
    public void NoRestore_02()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        // It is an error when never restored before.
        new DotnetCommand(Log, "build", "--no-restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("NETSDK1004"); // error NETSDK1004: Assets file '...\obj\project.assets.json' not found. Run a NuGet package restore to generate this file.

        // Run restore.
        new DotnetCommand(Log, "restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // --no-restore works.
        new DotnetCommand(Log, "build", "--no-restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "--no-build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");
    }

    [Fact]
    public void Restore_StaticGraph_Implicit()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <PropertyGroup>
                    <RestoreUseStaticGraphEvaluation>true</RestoreUseStaticGraphEvaluation>
                </PropertyGroup>
            </Project>
            """);
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, "Console.WriteLine();");

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();
    }

    [Fact]
    public void Restore_StaticGraph_Explicit()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, """
            #:property RestoreUseStaticGraphEvaluation=true
            Console.WriteLine();
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr(DirectiveError(programFile, 1, FileBasedProgramsResources.StaticGraphRestoreNotSupported));
    }

    [Fact]
    public void NoBuild_01()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        // It is an error when never built before.
        new DotnetCommand(Log, "run", "--no-build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("An error occurred trying to start process");

        // Now build it.
        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // Changing the program has no effect when it is not built.
        File.WriteAllText(programFile, """Console.WriteLine("Changed");""");
        new DotnetCommand(Log, "run", "--no-build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        // The change has an effect when built again.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Changed");
    }

    [Fact]
    public void NoBuild_02()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        // It is an error when never built before.
        new DotnetCommand(Log, "run", "--no-build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("An error occurred trying to start process");

        // Now build it.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        // Changing the program has no effect when it is not built.
        File.WriteAllText(programFile, """Console.WriteLine("Changed");""");
        new DotnetCommand(Log, "run", "--no-build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        // The change has an effect when built again.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Changed");
    }

    [Fact]
    public void Build_Library()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "lib.cs");
        File.WriteAllText(programFile, """
            #:property OutputType=Library
            class C;
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "lib.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "lib.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr(string.Format(CliCommandStrings.RunCommandExceptionUnableToRun,
                VirtualProjectBuilder.GetVirtualProjectPath(programFile),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "Library"));
    }

    [Fact]
    public void Build_Library_MultiTarget()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "lib.cs");
        File.WriteAllText(programFile, $"""
            #:property OutputType=Library
            #:property PublishAot=false
            #:property LangVersion=preview
            #:property TargetFramework=
            #:property TargetFrameworks=netstandard2.0;{ToolsetInfo.CurrentTargetFramework}
            class C;
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "lib.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "lib.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute("--no-interactive")
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));

        new DotnetCommand(Log, "run", "lib.cs", "--framework", ToolsetInfo.CurrentTargetFramework)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr(string.Format(CliCommandStrings.RunCommandExceptionUnableToRun,
                VirtualProjectBuilder.GetVirtualProjectPath(programFile),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "Library"));
    }

    [Fact]
    public void Build_Module()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "module.cs");
        File.WriteAllText(programFile, """
            #:property OutputType=Module
            #:property ProduceReferenceAssembly=false
            class C;
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "module.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "module.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr(string.Format(CliCommandStrings.RunCommandExceptionUnableToRun,
                VirtualProjectBuilder.GetVirtualProjectPath(programFile),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "Module"));
    }

    [Fact]
    public void Build_WinExe()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "winexe.cs");
        File.WriteAllText(programFile, """
            #:property OutputType=WinExe
            Console.WriteLine("Hello WinExe");
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "winexe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "winexe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello WinExe");
    }

    [Fact]
    public void Build_Exe()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "exe.cs");
        File.WriteAllText(programFile, """
            #:property OutputType=Exe
            Console.WriteLine("Hello Exe");
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "exe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "exe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello Exe");
    }

    [Fact]
    public void Build_Exe_MultiTarget()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "exe.cs");
        File.WriteAllText(programFile, $"""
            #:property OutputType=Exe
            #:property PublishAot=false
            #:property LangVersion=preview
            #:property TargetFramework=
            #:property TargetFrameworks=netstandard2.0;{ToolsetInfo.CurrentTargetFramework}
            Console.WriteLine("Hello Exe");
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "exe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "exe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework"));

        new DotnetCommand(Log, "run", "exe.cs", "--framework", ToolsetInfo.CurrentTargetFramework)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello Exe");
    }

    [Fact]
    public void Build_AppContainerExe()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "appcontainerexe.cs");
        File.WriteAllText(programFile, """
            #:property OutputType=AppContainerExe
            Console.WriteLine("Hello AppContainerExe");
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "appcontainerexe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run", "appcontainerexe.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr(string.Format(CliCommandStrings.RunCommandExceptionUnableToRun,
                VirtualProjectBuilder.GetVirtualProjectPath(programFile),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "AppContainerExe"));
    }

    [Fact]
    public void Publish()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var publishDir = Path.Join(testInstance.Path, "artifacts");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(publishDir).Sub("Program")
            .Should().Exist()
            .And.NotHaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly); // no deps.json file for AOT-published app

        new RunExeCommand(Log, Path.Join(publishDir, "Program", $"Program{Constants.ExeSuffix}"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                Release config
                """);
    }

    [Fact]
    public void PublishWithCustomTarget()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var publishDir = Path.Join(testInstance.Path, "artifacts");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "Program.cs", "-t", "ComputeContainerConfig", "-p", "PublishAot=false", "--use-current-runtime")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        var appBinaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Program.exe" : "Program";
        new DirectoryInfo(publishDir).Sub("Program")
            .Should().Exist()
            .And.HaveFiles([
                appBinaryName,
                "Program.deps.json",
                "Program.runtimeconfig.json"
            ]);
    }

    [Fact]
    public void Publish_WithJson()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, """
            #:sdk Microsoft.NET.Sdk.Web
            Console.WriteLine(File.ReadAllText("config.json"));
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "config.json"), """
            { "MyKey": "MyValue" }
            """);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var publishDir = Path.Join(testInstance.Path, "artifacts");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(publishDir).Sub("Program")
            .Should().Exist()
            .And.NotHaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly) // no deps.json file for AOT-published app
            .And.HaveFile("config.json"); // the JSON is included as content and hence copied
    }

    [Fact]
    public void Publish_Options()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var publishDir = Path.Join(testInstance.Path, "artifacts");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "Program.cs", "-c", "Debug", "-p:PublishAot=false", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(publishDir).Sub("Program")
            .Should().Exist()
            .And.HaveFile("Program.deps.json");

        new DirectoryInfo(testInstance.Path).File("msbuild.binlog").Should().Exist();
    }

    [Fact]
    public void Publish_PublishDir_IncludesFileName()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "MyCustomProgram.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var publishDir = Path.Join(testInstance.Path, "artifacts");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "MyCustomProgram.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(publishDir).Sub("MyCustomProgram")
            .Should().Exist()
            .And.NotHaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly); // no deps.json file for AOT-published app
    }

    [Fact]
    public void Publish_PublishDir_CommandLine()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var customPublishDir = Path.Join(testInstance.Path, "custom-publish");
        if (Directory.Exists(customPublishDir)) Directory.Delete(customPublishDir, recursive: true);

        new DotnetCommand(Log, "publish", "Program.cs", $"/p:PublishDir={customPublishDir}")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(customPublishDir)
            .Should().Exist()
            .And.NotHaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly); // no deps.json file for AOT-published app
    }

    [Fact]
    public void Publish_PublishDir_PropertyDirective()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        var publishDir = Path.Join(testInstance.Path, "directive-publish");
        File.WriteAllText(programFile, $"""
            #:property PublishDir={publishDir}
            {s_program}
            """);

        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(publishDir)
            .Should().Exist()
            .And.NotHaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly); // no deps.json file for AOT-published app
    }

    [Fact]
    public void Publish_In_SubDir()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var subDir = Directory.CreateDirectory(Path.Combine(testInstance.Path, "subdir"));

        var programFile = Path.Join(subDir.FullName, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var publishDir = Path.Join(subDir.FullName, "artifacts");
        if (Directory.Exists(publishDir)) Directory.Delete(publishDir, recursive: true);

        new DotnetCommand(Log, "publish", "./subdir/Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path).Sub("subdir").Sub("artifacts").Sub("Program")
            .Should().Exist()
            .And.NotHaveFilesMatching("*.deps.json", SearchOption.TopDirectoryOnly); // no deps.json file for AOT-published app
    }

    [Fact]
    public void Pack()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "MyFileBasedTool.cs");
        File.WriteAllText(programFile, """
            Console.WriteLine($"Hello; EntryPointFilePath set? {AppContext.GetData("EntryPointFilePath") is string}");
            #if !DEBUG
            Console.WriteLine("Release config");
            #endif
            """);

        // Run unpacked.
        new DotnetCommand(Log, "run", "MyFileBasedTool.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello; EntryPointFilePath set? True");

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var outputDir = Path.Join(testInstance.Path, "artifacts");
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);

        // Pack.
        new DotnetCommand(Log, "pack", "MyFileBasedTool.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        var packageDir = new DirectoryInfo(outputDir).Sub("MyFileBasedTool");
        packageDir.File("MyFileBasedTool.1.0.0.nupkg").Should().Exist();
        new DirectoryInfo(artifactsDir).Sub("package").Should().NotExist();

        // Run the packed tool.
        new DotnetCommand(Log, "tool", "exec", "MyFileBasedTool", "--yes", "--add-source", packageDir.FullName)
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Hello; EntryPointFilePath set? False
                Release config
                """);
    }

    [Fact]
    public void Pack_CustomPath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "MyFileBasedTool.cs");
        File.WriteAllText(programFile, """
            #:property PackageOutputPath=custom
            Console.WriteLine($"Hello; EntryPointFilePath set? {AppContext.GetData("EntryPointFilePath") is string}");
            """);

        // Run unpacked.
        new DotnetCommand(Log, "run", "MyFileBasedTool.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello; EntryPointFilePath set? True");

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var outputDir = Path.Join(testInstance.Path, "custom");
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);

        // Pack.
        new DotnetCommand(Log, "pack", "MyFileBasedTool.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(outputDir).File("MyFileBasedTool.1.0.0.nupkg").Should().Exist();
        new DirectoryInfo(artifactsDir).Sub("package").Should().NotExist();

        // Run the packed tool.
        new DotnetCommand(Log, "tool", "exec", "MyFileBasedTool", "--yes", "--add-source", outputDir)
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("Hello; EntryPointFilePath set? False");
    }

    [Fact]
    public void Clean()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        var artifactsDir = new DirectoryInfo(VirtualProjectBuilder.GetArtifactsPath(programFile));
        artifactsDir.Should().HaveFiles(["build-start.cache", "build-success.cache"]);

        var dllFile = artifactsDir.File("bin/debug/Program.dll");
        dllFile.Should().Exist();

        new DotnetCommand(Log, "clean", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        artifactsDir.EnumerateFiles().Should().BeEmpty();

        dllFile.Refresh();
        dllFile.Should().NotExist();
    }

    [PlatformSpecificFact(TestPlatforms.AnyUnix), UnsupportedOSPlatform("windows")]
    public void ArtifactsDirectory_Permissions()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(artifactsDir).UnixFileMode
            .Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, artifactsDir);

        // Re-create directory with incorrect permissions.
        Directory.Delete(artifactsDir, recursive: true);
        Directory.CreateDirectory(artifactsDir, UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute);
        var actualMode = new DirectoryInfo(artifactsDir).UnixFileMode
            .Should().NotBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, artifactsDir).And.Subject;

        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("build-start.cache"); // Unhandled exception: Access to the path '.../build-start.cache' is denied.

        // Build shouldn't have changed the permissions.
        new DirectoryInfo(artifactsDir).UnixFileMode
            .Should().Be(actualMode, artifactsDir);
    }

    [Theory, CombinatorialData]
    public void LaunchProfile(
        bool cscOnly,
        [CombinatorialValues("Properties/launchSettings.json", "Program.run.json")] string relativePath)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: cscOnly ? OutOfTreeBaseDirectory : null);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program + """

            Console.WriteLine($"Message: '{Environment.GetEnvironmentVariable("Message")}'");
            """);
        var fullPath = Path.Join(testInstance.Path, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, s_launchSettings);

        var prefix = cscOnly
            ? CliCommandStrings.NoBinaryLogBecauseRunningJustCsc + Environment.NewLine
            : string.Empty;

        new DotnetCommand(Log, "run", "-bl", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining(prefix + """
                Hello from Program
                Message: 'TestProfileMessage1'
                """);

        prefix = CliCommandStrings.NoBinaryLogBecauseUpToDate + Environment.NewLine;

        new DotnetCommand(Log, "run", "-bl", "--no-launch-profile", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(prefix + """
                Hello from Program
                Message: ''
                """);

        new DotnetCommand(Log, "run", "-bl", "-lp", "TestProfile2", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining(prefix + """
                Hello from Program
                Message: 'TestProfileMessage2'
                """);
    }

    /// <summary>
    /// <c>Properties/launchSettings.json</c> takes precedence over <c>Program.run.json</c>.
    /// </summary>
    [Fact]
    public void LaunchProfile_Precedence()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program + """

            Console.WriteLine($"Message: '{Environment.GetEnvironmentVariable("Message")}'");
            """);
        Directory.CreateDirectory(Path.Join(testInstance.Path, "Properties"));
        string launchSettings = Path.Join(testInstance.Path, "Properties", "launchSettings.json");
        File.WriteAllText(launchSettings, s_launchSettings.Replace("TestProfileMessage", "PropertiesLaunchSettingsJson"));
        string runJson = Path.Join(testInstance.Path, "Program.run.json");
        File.WriteAllText(runJson, s_launchSettings.Replace("TestProfileMessage", "ProgramRunJson"));

        new DotnetCommand(Log, "run", "--no-launch-profile", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                Message: ''
                """);

        // quiet runs here so that launch-profile usage messages don't impact test assertions
        new DotnetCommand(Log, "run", "-v", "q", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {string.Format(CliCommandStrings.RunCommandWarningRunJsonNotUsed, runJson, launchSettings)}
                Hello from Program
                Message: 'PropertiesLaunchSettingsJson1'
                """);

        new DotnetCommand(Log, "run", "-v", "q", "-lp", "TestProfile2", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {string.Format(CliCommandStrings.RunCommandWarningRunJsonNotUsed, runJson, launchSettings)}
                Hello from Program
                Message: 'PropertiesLaunchSettingsJson2'
                """);
    }

    /// <summary>
    /// Each file-based app in a folder can have separate launch profile.
    /// </summary>
    [Fact]
    public void LaunchProfile_Multiple()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var source = s_program + """

            Console.WriteLine($"Message: '{Environment.GetEnvironmentVariable("Message")}'");
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "First.cs"), source);
        File.WriteAllText(Path.Join(testInstance.Path, "First.run.json"), s_launchSettings.Replace("TestProfileMessage", "First"));
        File.WriteAllText(Path.Join(testInstance.Path, "Second.cs"), source);
        File.WriteAllText(Path.Join(testInstance.Path, "Second.run.json"), s_launchSettings.Replace("TestProfileMessage", "Second"));

        // do these runs with quiet verbosity so that default run output doesn't impact the tests
        new DotnetCommand(Log, "run", "-v", "q", "First.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from First
                Message: 'First1'
                """);

        new DotnetCommand(Log, "run", "-v", "q", "Second.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Second
                Message: 'Second1'
                """);
    }
}
