// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
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

}
