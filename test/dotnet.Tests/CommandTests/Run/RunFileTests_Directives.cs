// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Text.Json;
using Basic.CompilerLog.Util;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests_Directives(ITestOutputHelper log) : RunFileTestBase(log)
{
    [Fact]
    public void Define_01()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #if MY_DEFINE
            Console.WriteLine("Test output");
            #endif
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-p:DefineConstants=MY_DEFINE")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Test output");
    }

    [Fact]
    public void Define_02()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #if !MY_DEFINE
            Console.WriteLine("Test output");
            #endif
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-p:DefineConstants=MY_DEFINE")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS5001:"); // Program does not contain a static 'Main' method suitable for an entry point
    }

    [Fact]
    public void PackageReference()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:package System.CommandLine@2.0.0-beta4.22272.1
            using System.CommandLine;

            var rootCommand = new RootCommand("Sample app for System.CommandLine");
            return await rootCommand.InvokeAsync(args);
            """);

        new DotnetCommand(Log, "run", "Program.cs", "--", "--help")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Description:
                  Sample app for System.CommandLine
                """);
    }

    [Fact]
    public void PackageReference_CentralVersion()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Packages.props"), """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:package System.CommandLine
            using System.CommandLine;

            var rootCommand = new RootCommand("Sample app for System.CommandLine");
            return await rootCommand.InvokeAsync(args);
            """);

        new DotnetCommand(Log, "run", "Program.cs", "--", "--help")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Description:
                  Sample app for System.CommandLine
                """);
    }

    //  https://github.com/dotnet/sdk/issues/49665
    [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)] // https://github.com/dotnet/sdk/issues/48990
    public void SdkReference()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:sdk Microsoft.NET.Sdk
            #:sdk Aspire.AppHost.Sdk@9.2.1
            #:package Aspire.Hosting.AppHost@9.2.1

            var builder = DistributedApplication.CreateBuilder(args);
            builder.Build().Run();
            """);

        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();
    }

    [Fact] // https://github.com/dotnet/sdk/issues/49797
    public void SdkReference_VersionedSdkFirst()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:sdk Microsoft.NET.Sdk@9.0.0
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();
    }

    [Theory]
    [InlineData("../Lib/Lib.csproj")]
    [InlineData("../Lib")]
    [InlineData(@"..\Lib\Lib.csproj")]
    [InlineData(@"..\Lib")]
    [InlineData("$(MSBuildProjectDirectory)/../$(LibProjectName)")]
    [InlineData(@"$(MSBuildProjectDirectory)/../Lib\$(LibProjectName).csproj")]
    public void ProjectReference(string arg)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libDir = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(libDir, "Lib.cs"), """
            namespace Lib;
            public class LibClass
            {
                public static string GetMessage() => "Hello from Lib";
            }
            """);

        var appDir = Path.Join(testInstance.Path, "App");
        Directory.CreateDirectory(appDir);

        File.WriteAllText(Path.Join(appDir, "Program.cs"), $"""
            #:project {arg}
            #:property LibProjectName=Lib
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        var expectedOutput = "Hello from Lib";

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Running from a different working directory shouldn't affect handling of the relative project paths.
        new DotnetCommand(Log, "run", "App/Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("app")]
    public void ProjectReference_Errors(string? subdir)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var relativeFilePath = Path.Join(subdir, "Program.cs");
        var filePath = Path.Join(testInstance.Path, relativeFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, """
            #:project wrong.csproj
            """);

        // Project file does not exist.
        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(filePath, 1, FileBasedProgramsResources.InvalidProjectDirective,
                string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, Path.Join(testInstance.Path, subdir, "wrong.csproj"))));

        File.WriteAllText(filePath, """
            #:project dir/
            """);

        // Project directory does not exist.
        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(filePath, 1, FileBasedProgramsResources.InvalidProjectDirective,
                string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, Path.Join(testInstance.Path, subdir, "dir/"))));

        Directory.CreateDirectory(Path.Join(testInstance.Path, subdir, "dir"));

        // Directory exists but has no project file.
        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(filePath, 1, FileBasedProgramsResources.InvalidProjectDirective,
                string.Format(FileBasedProgramsResources.CouldNotFindAnyProjectInDirectory, Path.Join(testInstance.Path, subdir, "dir/"))));

        File.WriteAllText(Path.Join(testInstance.Path, subdir, "dir", "proj1.csproj"), "<Project />");
        File.WriteAllText(Path.Join(testInstance.Path, subdir, "dir", "proj2.csproj"), "<Project />");

        // Directory exists but has multiple project files.
        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(filePath, 1, FileBasedProgramsResources.InvalidProjectDirective,
                string.Format(FileBasedProgramsResources.MoreThanOneProjectInDirectory, Path.Join(testInstance.Path, subdir, "dir/"))));

        // Malformed MSBuild variable syntax.
        File.WriteAllText(filePath, """
            #:project $(Test
            """);

        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(filePath, 1, FileBasedProgramsResources.InvalidProjectDirective,
                string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, Path.Join(testInstance.Path, subdir, "$(Test"))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("app")]
    public void ProjectReference_Duplicate(string? subdir)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var relativeFilePath = Path.Join(subdir, "Program.cs");
        var filePath = Path.Join(testInstance.Path, relativeFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        Directory.CreateDirectory(Path.Join(testInstance.Path, subdir, "dir"));
        File.WriteAllText(Path.Join(testInstance.Path, subdir, "dir", "proj1.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(filePath, """
            #:project dir/
            #:project dir/
            Console.WriteLine("Hello");
            """);

        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(filePath, 2, FileBasedProgramsResources.DuplicateDirective, "#:project dir/"));

        File.WriteAllText(filePath, """
            #:project dir/
            #:project dir/proj1.csproj
            Console.WriteLine("Hello");
            """);

        // https://github.com/dotnet/sdk/issues/51139: we should detect the duplicate project reference
        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello");

        File.WriteAllText(filePath, """
            #:project dir/
            #:project $(MSBuildProjectDirectory)/dir/
            Console.WriteLine("Hello");
            """);

        // https://github.com/dotnet/sdk/issues/51139: we should detect the duplicate project reference
        new DotnetCommand(Log, "run", relativeFilePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello");
    }

    [Theory, CombinatorialData]
    public void IncludeDirective(
        [CombinatorialValues("Util.cs", "**/*.cs", "**/*.$(MyProp1)")] string includePattern,
        [CombinatorialValues("", "#:exclude Program.$(MyProp1)")] string additionalDirectives)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
                <ExperimentalFileBasedProgramEnableExcludeDirective>true</ExperimentalFileBasedProgramEnableExcludeDirective>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:include {includePattern}
            {additionalDirectives}
            #:property MyProp1=cs
            {s_programDependingOnUtil}
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, String from Util");
    }

    [Fact]
    public void IncludeDirective_WorkingDirectory()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
              </PropertyGroup>
            </Project>
            """);

        var srcDir = Path.Join(testInstance.Path, "src");
        Directory.CreateDirectory(srcDir);

        var a = """
            Console.WriteLine(B.M());
            """;

        File.WriteAllText(Path.Join(srcDir, "A.cs"), $"""
            #:include B.cs
            {a}
            """);

        var b = """
            static class B { public static string M() => "Hello from B"; }
            """;

        File.WriteAllText(Path.Join(srcDir, "B.cs"), b);

        var expectedOutput = "Hello from B";

        new DotnetCommand(Log, "run", "src/A.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert to a project.
        new DotnetCommand(Log, "project", "convert", "src/A.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .Should().HaveSubtree("""
                Directory.Build.props
                src/
                src/A.cs
                src/A/
                src/A/A.cs
                src/A/A.csproj
                src/A/B.cs
                src/B.cs
                """)
            .And.HaveFileContent("src/A/A.cs", a)
            .And.HaveFileContent("src/A/B.cs", b)
            .And.HaveFileContentPattern("src/A/A.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>A-*</UserSecretsId>
                  </PropertyGroup>

                </Project>

                """);

        // Run the converted project.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "src/A"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [Fact]
    public void IncludeDirective_Transitive()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        Directory.CreateDirectory(Path.Join(testInstance.Path, "dir1/dir2"));
        Directory.CreateDirectory(Path.Join(testInstance.Path, "dir3"));

        File.WriteAllText(Path.Join(testInstance.Path, "dir1/Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
                <ExperimentalFileBasedProgramEnableTransitiveDirectives>true</ExperimentalFileBasedProgramEnableTransitiveDirectives>
              </PropertyGroup>
            </Project>
            """);

        var a = """
            B.M();
            """;

        File.WriteAllText(Path.Join(testInstance.Path, "dir1/A.cs"), $"""
            #:include dir2/B.cs
            {a}
            """);

        var b = """
            static class B { public static void M() { C.M(); } }
            """;

        File.WriteAllText(Path.Join(testInstance.Path, "dir1/dir2/B.cs"), $"""
            #:include ../../dir3/$(P1).cs
            #:property P1=C
            {b}
            """);

        var c = """
            static class C { public static void M() { D.M(); } }
            """;

        File.WriteAllText(Path.Join(testInstance.Path, "dir3/C.cs"), $"""
            #:include ../$(P1).cs
            {c}
            """);

        var d = """
            static class D
            {
                public static void M()
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    using var stream = asm.GetManifestResourceStream($"{asm.GetName().Name}.Resources.resources")!;
                    using var reader = new System.Resources.ResourceReader(stream);
                    Console.WriteLine(reader.Cast<System.Collections.DictionaryEntry>().Single());
                }
            }
            """;

        File.WriteAllText(Path.Join(testInstance.Path, "C.cs"), $"""
            #:include Resources.resx
            {d}
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);

        var expectedOutput = "[MyString, TestValue]";

        new DotnetCommand(Log, "run", "A.cs")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "dir1"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        // Convert to a project.
        new DotnetCommand(Log, "project", "convert", "A.cs")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "dir1"))
            .Execute()
            .Should().Pass();

        new DirectoryInfo(Path.Join(testInstance.Path, "dir1/A"))
            .Should().HaveSubtree("""
                A.cs
                A.csproj
                C.cs
                C_2.cs
                Resources.resx
                dir2/
                dir2/B.cs
                """)
            .And.HaveFileContent("A.cs", a)
            .And.HaveFileContent("dir2/B.cs", b)
            .And.HaveFileContent("C.cs", c)
            .And.HaveFileContent("C_2.cs", d)
            .And.HaveFileContent("Resources.resx", s_resx)
            .And.HaveFileContentPattern("A.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                    <PackAsTool>true</PackAsTool>
                    <UserSecretsId>A-*</UserSecretsId>
                    <P1>C</P1>
                  </PropertyGroup>

                </Project>

                """);

        // Run the converted project.
        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "dir1/A"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);
    }

    [Fact]
    public void IncludeDirective_FileNotFound()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
              </PropertyGroup>
            </Project>
            """);

        var programPath = Path.Join(testInstance.Path, "A.cs");

        File.WriteAllText(programPath, """
            #:include B.cs
            Console.WriteLine("Hello");
            """);

        new DotnetCommand(Log, "run", "A.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(DirectiveError(programPath, 1, Resources.IncludedFileNotFound, Path.Join(testInstance.Path, "B.cs")));
    }

    /// <summary>
    /// Combination of <see cref="UpToDate"/> optimization and <c>#:include</c> directive.
    /// </summary>
    [Theory]
    [InlineData("*")]
    [InlineData("$(_Star)")]
    [InlineData("Util?")]
    public void IncludeDirective_UpToDate_Glob(string glob)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
              </PropertyGroup>
            </Project>
            """);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, $"""
            #:include {glob}.cs
            #:property _Star=*
            {s_programDependingOnUtil}
            """);

        var utilPath = Path.Join(testInstance.Path, "Util1.cs");
        var utilCode = s_util;
        File.WriteAllText(utilPath, utilCode);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var expectedOutput = "Hello, String from Util";

        Build(testInstance, BuildLevel.All, expectedOutput: expectedOutput);

        Build(testInstance, BuildLevel.All, expectedOutput: expectedOutput);

        utilCode = utilCode.Replace("String from Util", "v2");
        File.WriteAllText(utilPath, utilCode);

        Build(testInstance, BuildLevel.All, expectedOutput: "Hello, v2");

        utilCode = utilCode.Replace("v2", "v3");
        File.WriteAllText(utilPath, utilCode);

        Build(testInstance, BuildLevel.All, expectedOutput: "Hello, v3");

        var util2Path = Path.Join(testInstance.Path, "Util2.cs");
        File.WriteAllText(util2Path, """
            using System.Runtime.CompilerServices;

            file class C
            {
                [ModuleInitializer]
                internal static void Initialize()
                {
                    Console.WriteLine("Hello from Util2");
                }
            }
            """);

        Build(testInstance, BuildLevel.All, expectedOutput: """
            Hello from Util2
            Hello, v3
            """);
    }

    /// <summary>
    /// Combination of <see cref="UpToDate"/> optimization and <c>#:include</c> directive.
    /// </summary>
    [Fact]
    public void IncludeDirective_UpToDate_NoGlob()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
              </PropertyGroup>
            </Project>
            """);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, $"""
            #:include Util.cs
            {s_programDependingOnUtil}
            """);

        var utilPath = Path.Join(testInstance.Path, "Util.cs");
        var utilCode = s_util;
        File.WriteAllText(utilPath, utilCode);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var expectedOutput = "Hello, String from Util";

        Build(testInstance, BuildLevel.All, expectedOutput: expectedOutput);

        Build(testInstance, BuildLevel.None, expectedOutput: expectedOutput);

        utilCode = utilCode.Replace("String from Util", "v2");
        File.WriteAllText(utilPath, utilCode);

        Build(testInstance, BuildLevel.All, expectedOutput: "Hello, v2");

        utilCode = utilCode.Replace("v2", "v3");
        File.WriteAllText(utilPath, utilCode);

        Build(testInstance, BuildLevel.All, expectedOutput: "Hello, v3");

        var util2Path = Path.Join(testInstance.Path, "Util2.cs");
        File.WriteAllText(util2Path, """
            using System.Runtime.CompilerServices;

            file class C
            {
                [ModuleInitializer]
                internal static void Initialize()
                {
                    Console.WriteLine("Hello from Util2");
                }
            }
            """);

        Build(testInstance, BuildLevel.None, expectedOutput: "Hello, v3");

        Build(testInstance, BuildLevel.All, args: ["--no-cache"], expectedOutput: "Hello, v3");
    }

    /// <summary>
    /// Combination of <see cref="UpToDate_ProjectReferences"/> test and <c>#:include</c> directive.
    /// </summary>
    [Fact]
    public void IncludeDirective_UpToDate_ProjectReference()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
                <ExperimentalFileBasedProgramEnableTransitiveDirectives>true</ExperimentalFileBasedProgramEnableTransitiveDirectives>
              </PropertyGroup>
            </Project>
            """);

        var libDir = Path.Join(testInstance.Path, "Lib");
        Directory.CreateDirectory(libDir);

        File.WriteAllText(Path.Join(libDir, "Lib.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var libPath = Path.Join(libDir, "Lib.cs");
        var libCode = """
            namespace Lib;
            public class LibClass
            {
                public static string GetMessage() => "Lib(v1)";
            }
            """;
        File.WriteAllText(libPath, libCode);

        var appDir = Path.Join(testInstance.Path, "App");
        Directory.CreateDirectory(appDir);

        var utilPath = Path.Join(appDir, "Util.cs");
        var utilCode = """
            #:project ../Lib
            class UtilClass
            {
                public static string GetMessage() => "Util(v1) " + Lib.LibClass.GetMessage();
            }
            """;
        File.WriteAllText(utilPath, utilCode);

        var programPath = Path.Join(appDir, "Program.cs");
        var programCode = """
            #:include Util.cs
            Console.WriteLine("Program(v1) " + UtilClass.GetMessage());
            """;
        File.WriteAllText(programPath, programCode);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var expectedOutput = "Program(v1) Util(v1) Lib(v1)";

        Build(testInstance, BuildLevel.All, expectedOutput: expectedOutput, workDir: appDir);

        Build(testInstance, BuildLevel.All, expectedOutput: expectedOutput, workDir: appDir);

        libCode = libCode.Replace("v1", "v2");
        File.WriteAllText(libPath, libCode);

        expectedOutput = "Program(v1) Util(v1) Lib(v2)";

        Build(testInstance, BuildLevel.All, expectedOutput: expectedOutput, workDir: appDir);
    }

    [Fact]
    public void IncludeDirective_FeatureFlags()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, $"""
            #:include *.cs
            {s_programDependingOnUtil}
            """);

        var utilPath = Path.Join(testInstance.Path, "Util.cs");
        File.WriteAllText(utilPath, $"""
            #:exclude Other.cs
            {s_util}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(programPath, 1, Resources.ExperimentalFeatureDisabled, CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableIncludeDirective)}

                {CliCommandStrings.RunCommandException}
                """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable(CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableIncludeDirective, "true")
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(utilPath, 1, Resources.ExperimentalFeatureDisabled, CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableExcludeDirective)}

                {CliCommandStrings.RunCommandException}
                """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable(CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableIncludeDirective, "true")
            .WithEnvironmentVariable(CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableExcludeDirective, "true")
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(utilPath, 1, Resources.ExperimentalFeatureDisabled, CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableTransitiveDirectives)}

                {CliCommandStrings.RunCommandException}
                """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable(CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableIncludeDirective, "true")
            .WithEnvironmentVariable(CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableExcludeDirective, "true")
            .WithEnvironmentVariable(CSharpDirective.IncludeOrExclude.ExperimentalFileBasedProgramEnableTransitiveDirectives, "true")
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, String from Util");
    }

    [Fact]
    public void IncludeDirective_CustomMapping()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
                <ExperimentalFileBasedProgramEnableItemMapping>true</ExperimentalFileBasedProgramEnableItemMapping>
              </PropertyGroup>
            </Project>
            """);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, $"""
            #:property FileBasedProgramsItemMapping=.json=Content
            #:include *.cs
            {s_programDependingOnUtil}
            """);

        var utilPath = Path.Join(testInstance.Path, "Util.cs");
        File.WriteAllText(utilPath, s_util);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(programPath, 2, FileBasedProgramsResources.IncludeOrExcludeDirectiveUnknownFileType, "#:include", ".json")}

                {CliCommandStrings.RunCommandException}
                """);

        File.WriteAllText(programPath, $"""
            #:property FileBasedProgramsItemMapping=.cs=Content
            #:include *.cs
            {s_programDependingOnUtil}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Util' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");

        File.WriteAllText(programPath, $"""
            #:property FileBasedProgramsItemMapping=.cs=Compile
            #:include *.cs
            {s_programDependingOnUtil}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, String from Util");
    }

    [Fact]
    public void IncludeDirective_CustomMapping_ParseErrors()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
                <ExperimentalFileBasedProgramEnableItemMapping>true</ExperimentalFileBasedProgramEnableItemMapping>
              </PropertyGroup>
            </Project>
            """);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            #:property FileBasedProgramsItemMapping=x
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS5001: Program does not contain a static 'Main' method suitable for an entry point
            .And.HaveStdOutContaining("error CS5001");

        File.WriteAllText(programPath, """
            #:property FileBasedProgramsItemMapping=x
            #:include *.*
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(programPath, 1, FileBasedProgramsResources.InvalidIncludeExcludeMappingEntry, "x")}

                {CliCommandStrings.RunCommandException}
                """);

        File.WriteAllText(programPath, """
            #:property FileBasedProgramsItemMapping=.=X;y
            #:include *.*
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(programPath, 1, FileBasedProgramsResources.InvalidIncludeExcludeMappingExtension, ".", ".=X")}

                {CliCommandStrings.RunCommandException}
                """);

        File.WriteAllText(programPath, """
            #:property FileBasedProgramsItemMapping=.cs=;y
            #:include *.*
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(programPath, 1, FileBasedProgramsResources.InvalidIncludeExcludeMappingItemType, "", ".cs=")}

                {CliCommandStrings.RunCommandException}
                """);

        File.WriteAllText(programPath, """
            #:property FileBasedProgramsItemMapping=.x=X;y
            #:include *.*
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr($"""
                {DirectiveError(programPath, 1, FileBasedProgramsResources.InvalidIncludeExcludeMappingEntry, "y")}

                {CliCommandStrings.RunCommandException}
                """);
    }

    /// <summary>
    /// Demonstrates that consumers (e.g., IDE) can use the API to create an approximate virtual project without needing to know the full mapping.
    /// </summary>
    [Fact]
    public void IncludeDirective_CustomMapping_Api()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = """
            #:include B.cs
            #:include C.proto
            Console.WriteLine();
            """;

        var builder = new VirtualProjectBuilder(
            entryPointFileFullPath: programPath,
            targetFramework: VirtualProjectBuildingCommand.TargetFramework,
            sourceText: SourceText.From(code, Encoding.UTF8));

        var directives = FileLevelDirectiveHelpers.FindDirectives(
            builder.EntryPointSourceFile,
            reportAllErrors: true,
            VirtualProjectBuildingCommand.ThrowingReporter);

        ImmutableArray<(string Extension, string ItemType)> mapping = [(".cs", "Compile")];

        var evaluatedBuilder = ImmutableArray.CreateBuilder<CSharpDirective>(directives.Length);

        foreach (var directive in directives)
        {
            if (directive is CSharpDirective.IncludeOrExclude includeOrExcludeDirective)
            {
                var evaluated = includeOrExcludeDirective.WithDeterminedItemType(ErrorReporters.IgnoringReporter, mapping);
                evaluatedBuilder.Add(evaluated);
            }
            else
            {
                evaluatedBuilder.Add(directive);
            }
        }

        var evaluatedDirectives = evaluatedBuilder.DrainToImmutable();

        var projectWriter = new System.IO.StringWriter();
        VirtualProjectBuilder.WriteProjectFile(
            projectWriter,
            evaluatedDirectives,
            VirtualProjectBuilder.GetDefaultProperties(VirtualProjectBuildingCommand.TargetFrameworkVersion),
            isVirtualProject: true,
            entryPointFilePath: programPath,
            artifactsPath: builder.ArtifactsPath);

        var actualProject = projectWriter.ToString();

        Log.WriteLine(actualProject);

        actualProject.Should().Contain("""<Compile Include="B.cs" />""");

        actualProject.Should().NotContain(".proto");
    }

    [Fact]
    public void IncludeDirective_DefaultMapping_InSync()
    {
        var parsed = CSharpDirective.IncludeOrExclude.ParseMapping(CSharpDirective.IncludeOrExclude.DefaultMappingString,
            sourceFile: default,
            VirtualProjectBuildingCommand.ThrowingReporter);
        parsed.Should().BeEquivalentTo(CSharpDirective.IncludeOrExclude.DefaultMapping);
    }

    [Theory] // https://github.com/dotnet/aspnetcore/issues/63440
    [InlineData(true, null)]
    [InlineData(false, null)]
    [InlineData(true, "test-id")]
    [InlineData(false, "test-id")]
    public void UserSecrets(bool useIdArg, string? userSecretsId)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        string code = $"""
            #:package Microsoft.Extensions.Configuration.UserSecrets@{CSharpCompilerCommand.RuntimeVersion}
            {(userSecretsId is null ? "" : $"#:property UserSecretsId={userSecretsId}")}

            using Microsoft.Extensions.Configuration;

            IConfigurationRoot config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            Console.WriteLine("v1");
            Console.WriteLine(config.GetDebugView());
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        if (useIdArg)
        {
            if (userSecretsId == null)
            {
                var result = new DotnetCommand(Log, "build", "-getProperty:UserSecretsId", "Program.cs")
                    .WithWorkingDirectory(testInstance.Path)
                    .Execute();
                result.Should().Pass();
                userSecretsId = result.StdOut!.Trim();
            }

            new DotnetCommand(Log, "user-secrets", "set", "MySecret", "MyValue", "--id", userSecretsId)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }
        else
        {
            new DotnetCommand(Log, "user-secrets", "set", "MySecret", "MyValue", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        Build(testInstance, BuildLevel.All, expectedOutput: """
            v1
            MySecret=MyValue (JsonConfigurationProvider for 'secrets.json' (Optional))
            """);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: """
            v2
            MySecret=MyValue (JsonConfigurationProvider for 'secrets.json' (Optional))
            """);
    }

    /// <summary>
    /// Verifies that msbuild-based runs use CSC args equivalent to csc-only runs.
    /// Can regenerate CSC arguments template in <see cref="CSharpCompilerCommand"/>.
    /// </summary>
    [Fact]
    public void CscArguments()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        const string programName = "TestProgram";
        const string fileName = $"{programName}.cs";
        string entryPointPath = Path.Join(testInstance.Path, fileName);
        File.WriteAllText(entryPointPath, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(entryPointPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        // Build using MSBuild.
        new DotnetCommand(Log, "run", fileName, "-bl", "--no-cache")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"Hello from {programName}");

        // Find the csc args used by the build.
        var msbuildCall = FindCompilerCall(Path.Join(testInstance.Path, "msbuild.binlog"));
        var msbuildCallArgs = msbuildCall.GetArguments();
        var msbuildCallArgsString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(msbuildCallArgs);

        // Generate argument template code.
        string sdkPath = NormalizePath(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest);
        string dotNetRootPath = NormalizePath(SdkTestContext.Current.ToolsetUnderTest.DotNetRoot);
        string nuGetCachePath = NormalizePath(SdkTestContext.Current.NuGetCachePath!);
        string artifactsDirNormalized = NormalizePath(artifactsDir);
        string objPath = $"{artifactsDirNormalized}/obj/debug";
        string entryPointPathNormalized = NormalizePath(entryPointPath);
        var msbuildArgsToVerify = new List<string>();
        var nuGetPackageFilePaths = new List<string>();
        bool referenceSpreadInserted = false;
        bool analyzerSpreadInserted = false;
        const string NetCoreAppRefPackPath = "packs/Microsoft.NETCore.App.Ref/";
        var code = new StringBuilder();
        code.AppendLine($$"""
            // Licensed to the .NET Foundation under one or more agreements.
            // The .NET Foundation licenses this file to you under the MIT license.

            using System.Text.Json;

            namespace Microsoft.DotNet.Cli.Commands.Run;

            // Generated by test `{{nameof(RunFileTests)}}.{{nameof(CscArguments)}}`.
            partial class CSharpCompilerCommand
            {
                private IEnumerable<string> GetCscArguments(
                    string objDir,
                    string binDir)
                {
                    return
                    [
            """);
        foreach (var arg in msbuildCallArgs)
        {
            // This option needs to be passed on the command line, not in an RSP file.
            if (arg is "/noconfig")
            {
                continue;
            }

            // We don't need to generate a ref assembly.
            if (arg.StartsWith("/refout:", StringComparison.Ordinal))
            {
                continue;
            }

            // There should be no source link arguments.
            if (arg.StartsWith("/sourcelink:", StringComparison.Ordinal))
            {
                Assert.Fail($"Unexpected source link argument: {arg}");
            }

            // PreferredUILang is normally not set by default but can be in builds, so ignore it.
            if (arg.StartsWith("/preferreduilang:", StringComparison.Ordinal))
            {
                continue;
            }

            bool needsInterpolation = false;
            bool fromNuGetPackage = false;

            // Normalize slashes in paths.
            string rewritten = NormalizePathArg(arg);

            // Remove quotes.
            rewritten = RemoveQuotes(rewritten);

            string msbuildArgToVerify = rewritten;

            // Use variable SDK path.
            if (rewritten.Contains(sdkPath, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(sdkPath, "{SdkPath}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable .NET root path.
            if (rewritten.Contains(dotNetRootPath, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(dotNetRootPath, "{DotNetRootPath}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable NuGet cache path.
            if (rewritten.Contains(nuGetCachePath, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(nuGetCachePath, "{NuGetCachePath}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
                fromNuGetPackage = true;
            }

            // Use variable intermediate dir path.
            if (rewritten.Contains(objPath, StringComparison.OrdinalIgnoreCase))
            {
                // We want to emit the resulting DLL directly into the bin folder.
                bool isOut = arg.StartsWith("/out", StringComparison.Ordinal);
                string replacement = isOut ? "{binDir}" : "{objDir}";

                if (isOut)
                {
                    msbuildArgToVerify = msbuildArgToVerify.Replace("/obj/", "/bin/", StringComparison.OrdinalIgnoreCase);
                }

                rewritten = rewritten.Replace(objPath, replacement, StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable file name.
            if (rewritten.Contains(entryPointPathNormalized, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(entryPointPathNormalized, "{" + nameof(CSharpCompilerCommand.EntryPointFileFullPath) + "}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable program name.
            if (rewritten.Contains(programName, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(programName, "{FileNameWithoutExtension}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable runtime version.
            if (rewritten.Contains(CSharpCompilerCommand.RuntimeVersion, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(CSharpCompilerCommand.RuntimeVersion, "{" + nameof(CSharpCompilerCommand.RuntimeVersion) + "}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable target framework version.
            if (rewritten.Contains(CSharpCompilerCommand.TargetFrameworkVersion, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(CSharpCompilerCommand.TargetFrameworkVersion, "{" + nameof(CSharpCompilerCommand.TargetFrameworkVersion) + "}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Ignore `/analyzerconfig` which is not variable (so it comes from the machine or sdk repo).
            if (!needsInterpolation && arg.StartsWith("/analyzerconfig", StringComparison.Ordinal))
            {
                continue;
            }

            // Use GetFrameworkReferenceArguments() for framework references instead of hard-coding them.
            if (arg.StartsWith("/reference:", StringComparison.Ordinal))
            {
                if (!referenceSpreadInserted)
                {
                    code.AppendLine("""
                                    .. GetFrameworkReferenceArguments(),
                        """);
                    referenceSpreadInserted = true;
                }

                msbuildArgsToVerify.Add(msbuildArgToVerify);
                continue;
            }

            // Use GetFrameworkAnalyzerArguments() for targeting-pack analyzers instead of hard-coding them.
            if (arg.StartsWith("/analyzer:", StringComparison.Ordinal)
                && rewritten.Contains(NetCoreAppRefPackPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!analyzerSpreadInserted)
                {
                    code.AppendLine("""
                                    .. GetFrameworkAnalyzerArguments(),
                        """);
                    analyzerSpreadInserted = true;
                }

                msbuildArgsToVerify.Add(msbuildArgToVerify);
                continue;
            }

            string prefix = needsInterpolation ? "$" : string.Empty;

            code.AppendLine($"""
                            {prefix}"{rewritten}",
                """);

            msbuildArgsToVerify.Add(msbuildArgToVerify);

            if (fromNuGetPackage)
            {
                nuGetPackageFilePaths.Add(CSharpCompilerCommand.IsPathOption(rewritten, out int colonIndex)
                    ? rewritten.Substring(colonIndex + 1)
                    : rewritten);
            }
        }
        code.AppendLine("""
                    ];
                }

                /// <summary>
                /// Files that come from referenced NuGet packages (e.g., analyzers for NativeAOT) need to be checked specially (if they don't exist, MSBuild needs to run).
                /// </summary>
                public static IEnumerable<string> GetPathsOfCscInputsFromNuGetCache()
                {
                    return
                    [
            """);
        foreach (var nuGetPackageFilePath in nuGetPackageFilePaths)
        {
            code.AppendLine($"""
                            $"{nuGetPackageFilePath}",
                """);
        }
        code.AppendLine("""
                    ];
                }
            """);

        // Generate file content templates.
        var baseDirectory = TestPathUtility.ResolveTempPrefixLink(Path.GetDirectoryName(entryPointPath)!);
        var replacements = new List<(string, string)>
        {
            (TestPathUtility.ResolveTempPrefixLink(entryPointPath), nameof(CSharpCompilerCommand.EntryPointFileFullPath)),
            (baseDirectory + Path.DirectorySeparatorChar, nameof(CSharpCompilerCommand.BaseDirectoryWithTrailingSeparator)),
            (baseDirectory, nameof(CSharpCompilerCommand.BaseDirectory)),
            (programName, nameof(CSharpCompilerCommand.FileNameWithoutExtension)),
            (CSharpCompilerCommand.TargetFrameworkVersion, nameof(CSharpCompilerCommand.TargetFrameworkVersion)),
            (CSharpCompilerCommand.TargetFramework, nameof(CSharpCompilerCommand.TargetFramework)),
            (CSharpCompilerCommand.DefaultRuntimeVersion, nameof(CSharpCompilerCommand.DefaultRuntimeVersion)),
        };
        var emittedFiles = Directory.EnumerateFiles(artifactsDir, "*", SearchOption.AllDirectories).Order();
        foreach (var emittedFile in emittedFiles)
        {
            var emittedFileName = Path.GetFileName(emittedFile);
            var generatedMethodName = GetGeneratedMethodName(emittedFileName);
            if (generatedMethodName is null)
            {
                Log.WriteLine($"Skipping unrecognized file '{emittedFile}'.");
                continue;
            }

            var emittedFileContent = File.ReadAllText(emittedFile);

            string interpolatedString = emittedFileContent;
            string interpolationPrefix;

            if (emittedFileName.EndsWith(".json", StringComparison.Ordinal))
            {
                interpolationPrefix = "$$";
                foreach (var (key, value) in replacements)
                {
                    interpolatedString = interpolatedString.Replace(JsonSerializer.Serialize(key), "{{JsonSerializer.Serialize(" + value + ", CSharpCompilerCommandJsonSerializerContext.Default.String)}}");
                }
            }
            else
            {
                interpolationPrefix = "$";
                foreach (var (key, value) in replacements)
                {
                    interpolatedString = interpolatedString.Replace(key, "{" + value + "}");
                }
            }

            if (interpolatedString == emittedFileContent)
            {
                interpolationPrefix = "";
            }

            code.AppendLine($$""""

                    private string Get{{generatedMethodName}}Content()
                    {
                        return {{interpolationPrefix}}"""
                {{interpolatedString}}
                """;
                    }
                """");
        }

        code.AppendLine("""
            }
            """);

        // Save the code.
        var codeFolder = new DirectoryInfo(Path.Join(
            SdkTestContext.Current.ToolsetUnderTest.RepoRoot,
            "src", "Cli", "dotnet", "Commands", "Run"));
        var nonGeneratedFile = codeFolder.File("CSharpCompilerCommand.cs");
        if (!nonGeneratedFile.Exists)
        {
            Log.WriteLine($"Skipping code generation because file does not exist: {nonGeneratedFile.FullName}");
        }
        else
        {
            var codeFilePath = codeFolder.File("CSharpCompilerCommand.Generated.cs");
            var existingText = codeFilePath.Exists ? File.ReadAllText(codeFilePath.FullName) : string.Empty;
            var newText = code.ToString();
            if (existingText != newText)
            {
                Log.WriteLine($"{codeFilePath.FullName} needs to be updated:");
                Log.WriteLine(newText);
                if (Env.GetEnvironmentVariableAsBool("CI"))
                {
                    throw new InvalidOperationException($"Not updating file in CI: {codeFilePath.FullName}");
                }
                else
                {
                    File.WriteAllText(codeFilePath.FullName, newText);
                    throw new InvalidOperationException($"File outdated, commit the changes: {codeFilePath.FullName}");
                }
            }
        }

        // Build using CSC.
        Directory.Delete(artifactsDir, recursive: true);
        new DotnetCommand(Log, "run", fileName, "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {CliCommandStrings.NoBinaryLogBecauseRunningJustCsc}
                Hello from {programName}
                """);

        // Read args from csc.rsp file.
        var rspFilePath = Path.Join(artifactsDir, "csc.rsp");
        var cscOnlyCallArgs = File.ReadAllLines(rspFilePath);
        var cscOnlyCallArgsString = string.Join(' ', cscOnlyCallArgs);

        // Check that csc args between MSBuild run and CSC-only run are equivalent.
        var normalizedCscOnlyArgs = cscOnlyCallArgs
            .Select(static a => NormalizePathArg(RemoveQuotes(a)))
            .ToList();
        Log.WriteLine("CSC-only args:");
        Log.WriteLine(string.Join(Environment.NewLine, normalizedCscOnlyArgs));
        Log.WriteLine("MSBuild args:");
        Log.WriteLine(string.Join(Environment.NewLine, msbuildArgsToVerify));

        // References and targeting-pack analyzers may be in a different order (FrameworkList.xml vs. MSBuild),
        // so compare them as sets. All other args must be in the same order.
        var cscOnlyRefArgs = normalizedCscOnlyArgs.Where(static a => a.StartsWith("/reference:", StringComparison.Ordinal)).ToList();
        var cscOnlyAnalyzerArgs = normalizedCscOnlyArgs.Where(a => a.StartsWith("/analyzer:", StringComparison.Ordinal) && a.Contains(NetCoreAppRefPackPath, StringComparison.OrdinalIgnoreCase)).ToList();
        var cscOnlyOtherArgs = normalizedCscOnlyArgs.Where(a => !a.StartsWith("/reference:", StringComparison.Ordinal) && !(a.StartsWith("/analyzer:", StringComparison.Ordinal) && a.Contains(NetCoreAppRefPackPath, StringComparison.OrdinalIgnoreCase))).ToList();
        var msbuildRefArgs = msbuildArgsToVerify.Where(static a => a.StartsWith("/reference:", StringComparison.Ordinal)).ToList();
        var msbuildAnalyzerArgs = msbuildArgsToVerify.Where(a => a.StartsWith("/analyzer:", StringComparison.Ordinal) && a.Contains(NetCoreAppRefPackPath, StringComparison.OrdinalIgnoreCase)).ToList();
        var msbuildOtherArgs = msbuildArgsToVerify.Where(a => !a.StartsWith("/reference:", StringComparison.Ordinal) && !(a.StartsWith("/analyzer:", StringComparison.Ordinal) && a.Contains(NetCoreAppRefPackPath, StringComparison.OrdinalIgnoreCase))).ToList();
        cscOnlyRefArgs.Should().NotBeEmpty(
            "framework references should be resolved from FrameworkList.xml");
        cscOnlyRefArgs.Should().BeEquivalentTo(msbuildRefArgs,
            "the generated file might be outdated, run this test locally to regenerate it");
        cscOnlyAnalyzerArgs.Should().NotBeEmpty(
            "framework analyzers should be resolved from FrameworkList.xml");
        cscOnlyAnalyzerArgs.Should().BeEquivalentTo(msbuildAnalyzerArgs,
            "the generated file might be outdated, run this test locally to regenerate it");
        cscOnlyOtherArgs.Should().Equal(msbuildOtherArgs,
            "the generated file might be outdated, run this test locally to regenerate it");

        static CompilerCall FindCompilerCall(string binaryLogPath)
        {
            using var reader = BinaryLogReader.Create(binaryLogPath);
            return reader.ReadAllCompilerCalls().Should().ContainSingle().Subject;
        }

        static string NormalizePathArg(string arg)
        {
            return CSharpCompilerCommand.IsPathOption(arg, out int colonIndex)
                ? string.Concat(arg.AsSpan(0, colonIndex + 1), NormalizePath(arg.Substring(colonIndex + 1)))
                : NormalizePath(arg);
        }

        static string NormalizePath(string path)
        {
            return PathUtility.GetPathWithForwardSlashes(TestPathUtility.ResolveTempPrefixLink(path));
        }

        static string RemoveQuotes(string arg)
        {
            return arg.Replace("\"", string.Empty);
        }

        static string? GetGeneratedMethodName(string fileName)
        {
            return fileName switch
            {
                $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}.AssemblyAttributes.cs" => "AssemblyAttributes",
                $"{programName}.GlobalUsings.g.cs" => "GlobalUsings",
                $"{programName}.AssemblyInfo.cs" => "AssemblyInfo",
                $"{programName}.GeneratedMSBuildEditorConfig.editorconfig" => "GeneratedMSBuildEditorConfig",
                $"{programName}{FileNameSuffixes.RuntimeConfigJson}" => "RuntimeConfig",
                _ => null,
            };
        }
    }

    /// <summary>
    /// Verifies that csc-only runs emit auxiliary files equivalent to msbuild-based runs.
    /// </summary>
    [Theory]
    [InlineData("Program.cs")]
    [InlineData("test.cs")]
    [InlineData("noext")]
    public void CscVsMSBuild(string fileName)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        string entryPointPath = Path.Join(testInstance.Path, fileName);
        File.WriteAllText(entryPointPath, $"""
            #!/test
            {s_program}
            """);

        string programName = Path.GetFileNameWithoutExtension(fileName);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(entryPointPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);
        var artifactsBackupDir = Path.ChangeExtension(artifactsDir, ".bak");
        if (Directory.Exists(artifactsBackupDir)) Directory.Delete(artifactsBackupDir, recursive: true);

        // Build using CSC.
        new DotnetCommand(Log, "run", fileName, "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {CliCommandStrings.NoBinaryLogBecauseRunningJustCsc}
                Hello from {programName}
                """);

        // Backup the artifacts directory.
        Directory.Move(artifactsDir, artifactsBackupDir);

        // Build using MSBuild.
        new DotnetCommand(Log, "run", fileName, "-bl", "--no-cache")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"Hello from {programName}");

        // Check that files generated by MSBuild and CSC-only runs are equivalent.
        var cscOnlyFiles = Directory.EnumerateFiles(artifactsBackupDir, "*", SearchOption.AllDirectories)
            .Where(f =>
                Path.GetDirectoryName(f) != artifactsBackupDir && // exclude top-level marker files
                Path.GetFileName(f) != programName && // binary on unix
                Path.GetExtension(f) is not (".dll" or ".exe" or ".pdb")); // other binaries
        bool hasErrors = false;
        foreach (var cscOnlyFile in cscOnlyFiles)
        {
            var relativePath = Path.GetRelativePath(relativeTo: artifactsBackupDir, path: cscOnlyFile);
            var msbuildFile = Path.Join(artifactsDir, relativePath);

            if (!File.Exists(msbuildFile))
            {
                throw new InvalidOperationException($"File exists in CSC-only run but not in MSBuild run: {cscOnlyFile}");
            }

            var cscOnlyFileText = File.ReadAllText(cscOnlyFile);
            var msbuildFileText = File.ReadAllText(msbuildFile);
            if (cscOnlyFileText.ReplaceLineEndings() != msbuildFileText.ReplaceLineEndings())
            {
                Log.WriteLine($"File differs between MSBuild and CSC-only runs (if this is expected, run test '{nameof(CscArguments)}' locally to re-generate the template): {cscOnlyFile}");
                const int limit = 3_000;
                if (cscOnlyFileText.Length < limit && msbuildFileText.Length < limit)
                {
                    Log.WriteLine("MSBuild file content:");
                    Log.WriteLine(msbuildFileText);
                    Log.WriteLine("CSC-only file content:");
                    Log.WriteLine(cscOnlyFileText);
                }
                else
                {
                    Log.WriteLine($"MSBuild file size: {msbuildFileText.Length} chars");
                    Log.WriteLine($"CSC-only file size: {cscOnlyFileText.Length} chars");
                }
                hasErrors = true;
            }
        }
        hasErrors.Should().BeFalse("some file contents do not match, see the test output for details");
    }

}
