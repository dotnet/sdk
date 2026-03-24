// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.FileBasedPrograms;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests_General(ITestOutputHelper log) : RunFileTestBase(log)
{
    /// <summary>
    /// <c>dotnet run file.cs</c> succeeds without a project file.
    /// </summary>
    [Theory]
    [InlineData(null, false)] // will be replaced with an absolute path
    [InlineData("Program.cs", false)]
    [InlineData("./Program.cs", false)]
    [InlineData("Program.CS", true)]
    public void FilePath(string? path, bool differentCasing)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var programPath = Path.Join(testInstance.Path, "Program.cs");

        File.WriteAllText(programPath, s_program);

        path ??= programPath;

        var result = new DotnetCommand(Log, "run", path)
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        if (!differentCasing || HasCaseInsensitiveFileSystem)
        {
            result.Should().Pass()
                .And.HaveStdOut("Hello from Program");
        }
        else
        {
            result.Should().Fail()
                .And.HaveStdErrContaining(string.Format(
                    CliCommandStrings.RunCommandExceptionNoProjects,
                    testInstance.Path,
                    "--project"));
        }
    }

    /// <summary>
    /// <c>dotnet file.cs</c> is equivalent to <c>dotnet run file.cs</c>.
    /// </summary>
    [Fact]
    public void FilePath_WithoutRun()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:property Configuration=Release
            {s_program}
            """);

        string expectedOutput = """
            Hello from Program
            Release config
            """;

        new DotnetCommand(Log, "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "./Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, $".{Path.DirectorySeparatorChar}Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, Path.Join(testInstance.Path, "Program.cs"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        new DotnetCommand(Log, "Program.cs", "-c", "Debug")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                """);

        new DotnetCommand(Log, "Program.cs", "arg1", "arg2")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:arg1;arg2
                Hello from Program
                Release config
                """);

        new DotnetCommand(Log, "Program.cs", "build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:build
                Hello from Program
                Release config
                """);

        new DotnetCommand(Log, "Program.cs", "arg1", "arg2")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:arg1;arg2
                Hello from Program
                Release config
                """);

        // https://github.com/dotnet/sdk/issues/52108
        new DotnetCommand(Log, "Program.cs", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:Program.cs
                Hello from Program
                Release config
                """);
    }

    /// <summary>
    /// Casing of the argument is used for the output binary name.
    /// </summary>
    [Fact]
    public void FilePath_DifferentCasing()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        var result = new DotnetCommand(Log, "run", "program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        if (HasCaseInsensitiveFileSystem)
        {
            result.Should().Pass()
                .And.HaveStdOut("Hello from program");
        }
        else
        {
            result.Should().Fail()
                .And.HaveStdErrContaining(string.Format(
                    CliCommandStrings.RunCommandExceptionNoProjects,
                    testInstance.Path,
                    "--project"));
        }
    }

    /// <summary>
    /// <c>dotnet run folder/file.cs</c> succeeds without a project file.
    /// </summary>
    [Fact]
    public void FilePath_OutsideWorkDir()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        var dirName = Path.GetFileName(testInstance.Path);

        new DotnetCommand(Log, "run", $"{dirName}/Program.cs")
            .WithWorkingDirectory(Path.GetDirectoryName(testInstance.Path)!)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");
    }

    /// <summary>
    /// <c>dotnet run --project file.cs</c> fails.
    /// </summary>
    [Fact]
    public void FilePath_AsProjectArgument()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--project", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);
    }

    /// <summary>
    /// Even if there is a file-based app <c>./build</c>, <c>dotnet build</c> should not execute that.
    /// </summary>
    [Theory]
    // error MSB1003: Specify a project or solution file. The current working directory does not contain a project or solution file.
    [InlineData("build", "MSB1003", false)]
    // dotnet watch: Could not find a MSBuild project file in '...'. Specify which project to use with the --project option.
    [InlineData("watch", "--project", true)]
    public void Precedence_BuiltInCommand(string cmd, string error, bool errorInStdErr)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, cmd), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello 1");
            """);
        File.WriteAllText(Path.Join(testInstance.Path, $"dotnet-{cmd}"), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello 2");
            """);

        // dotnet build -> built-in command
        var failure = new DotnetCommand(Log, cmd)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail();

        if (errorInStdErr)
        {
            failure.And.HaveStdErrContaining(error);
        }
        else
        {
            failure.And.HaveStdOutContaining(error);
        }

        // dotnet ./build -> file-based app
        new DotnetCommand(Log, $"./{cmd}")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOut("hello 1");

        // dotnet run build -> file-based app
        new DotnetCommand(Log, "run", cmd)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello 1");
    }

    /// <summary>
    /// Even if there is a file-based app <c>./test.dll</c>, <c>dotnet test.dll</c> should not execute that.
    /// </summary>
    [Theory]
    [InlineData("test.dll")]
    [InlineData("./test.dll")]
    public void Precedence_Dll(string arg)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "test.dll"), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello world");
            """);

        // dotnet [./]test.dll -> exec the dll
        new DotnetCommand(Log, arg)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // A fatal error was encountered. The library 'hostpolicy.dll' required to execute the application was not found in ...
            .And.HaveStdErrContaining("hostpolicy");

        // dotnet run [./]test.dll -> file-based app
        new DotnetCommand(Log, "run", arg)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello world");
    }

    //  https://github.com/dotnet/sdk/issues/49665
    //  Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
    [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
    public void Precedence_NuGetTool()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "complog"), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello world");
            """);

        new DotnetCommand(Log, "new", "tool-manifest")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "tool", "install", "complog@0.7.0")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        // dotnet complog -> NuGet tool
        new DotnetCommand(Log, "complog")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("complog");

        // dotnet ./complog -> file-based app
        new DotnetCommand(Log, "./complog")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello world");

        // dotnet run complog -> file-based app
        new DotnetCommand(Log, "run", "complog")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello world");
    }

    /// <summary>
    /// <c>dotnet run -</c> reads the C# code from stdin.
    /// </summary>
    [Fact]
    public void ReadFromStdin()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        new DotnetCommand(Log, "run", "-")
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardInput("""
                Console.WriteLine("Hello from stdin");
                Console.WriteLine("Read: " + (Console.ReadLine() ?? "null"));
                Console.WriteLine("Working directory: " + Environment.CurrentDirectory);
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                Hello from stdin
                Read: null
                Working directory: {testInstance.Path}
                """);
    }

    /// <summary>
    /// <c>Directory.Build.props</c> doesn't have any effect on <c>dotnet run -</c>.
    /// </summary>
    [Fact]
    public void ReadFromStdin_BuildProps()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ImplicitUsings>disable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "run", "-")
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardInput("""
                Console.WriteLine("Hello from stdin");
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from stdin");

        new DotnetCommand(Log, "run", "-")
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardInput("""
                #:property ImplicitUsings=disable
                Console.WriteLine("Hello from stdin");
                """)
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Console' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");
    }

    /// <summary>
    /// <c>Directory.Build.props</c> doesn't have any effect on <c>dotnet run -</c>.
    /// </summary>
    [Fact]
    public void ReadFromStdin_ProjectReference()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var libDir = Path.Join(testInstance.Path, "lib");
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

        var appDir = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDir);

        new DotnetCommand(Log, "run", "-")
            .WithWorkingDirectory(appDir)
            .WithStandardInput($"""
                #:project $(MSBuildStartupDirectory)/../lib
                Console.WriteLine(Lib.LibClass.GetMessage());
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Lib");

        // Relative paths are resolved from the isolated temp directory, hence they don't work.

        var errorParts = DirectiveError("app.cs", 1, FileBasedProgramsResources.InvalidProjectDirective,
            string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, "{}")).Split("{}");
        errorParts.Should().HaveCount(2);

        new DotnetCommand(Log, "run", "-")
            .WithWorkingDirectory(appDir)
            .WithStandardInput($"""
                #:project ../lib
                Console.WriteLine(Lib.LibClass.GetMessage());
                """)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(errorParts[0])
            .And.HaveStdErrContaining(errorParts[1]);
    }

    [Fact]
    public void ReadFromStdin_NoBuild()
    {
        new DotnetCommand(Log, "run", "-", "--no-build")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidOptionForStdin, "--no-build"));
    }

    [Fact]
    public void ReadFromStdin_LaunchProfile()
    {
        new DotnetCommand(Log, "run", "-", "--launch-profile=test")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidOptionForStdin, "--launch-profile"));
    }

    /// <summary>
    /// <c>dotnet run -- -</c> should NOT read the C# file from stdin,
    /// the hyphen should be considred an app argument instead since it's after <c>--</c>.
    /// </summary>
    [Fact]
    public void ReadFromStdin_AfterDoubleDash()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        new DotnetCommand(Log, "run", "--", "-")
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardInput("""Console.WriteLine("stdin code");""")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionNoProjects, testInstance.Path, "--project"));
    }

    /// <summary>
    /// <c>dotnet run folder</c> without a project file is not supported.
    /// </summary>
    [Theory]
    [InlineData(null)] // will be replaced with an absolute path
    [InlineData(".")]
    [InlineData("../MSBuildTestApp")]
    [InlineData("../MSBuildTestApp/")]
    public void FolderPath(string? path)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        path ??= testInstance.Path;

        new DotnetCommand(Log, "run", path)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                "--project"));
    }

    /// <summary>
    /// <c>dotnet run app.csproj</c> fails if app.csproj does not exist.
    /// </summary>
    [Fact]
    public void ProjectPath_DoesNotExist()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "./App.csproj")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                "--project"));
    }

    /// <summary>
    /// <c>dotnet run app.csproj</c> where app.csproj exists
    /// runs the project and passes 'app.csproj' as an argument.
    /// </summary>
    [Fact]
    public void ProjectPath_Exists()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        File.WriteAllText(Path.Join(testInstance.Path, "App.csproj"), s_consoleProject);

        new DotnetCommand(Log, "run", "./App.csproj")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                echo args:./App.csproj
                Hello from App
                """);
    }

    [Fact]
    public void ProjectInCurrentDirectory_NoRunVerb()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "file"));
        File.WriteAllText(Path.Join(testInstance.Path, "file", "Program.cs"), s_program);
        Directory.CreateDirectory(Path.Join(testInstance.Path, "proj"));
        File.WriteAllText(Path.Join(testInstance.Path, "proj", "App.csproj"), s_consoleProject);

        new DotnetCommand(Log, "../file/Program.cs")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "proj"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Hello from Program
                """);
    }

    [Fact]
    public void ProjectInCurrentDirectory_FileOption()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        Directory.CreateDirectory(Path.Join(testInstance.Path, "file"));
        File.WriteAllText(Path.Join(testInstance.Path, "file", "Program.cs"), s_program);
        Directory.CreateDirectory(Path.Join(testInstance.Path, "proj"));
        File.WriteAllText(Path.Join(testInstance.Path, "proj", "App.csproj"), s_consoleProject);

        new DotnetCommand(Log, "run", "--file", "../file/Program.cs")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "proj"))
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Hello from Program
                """);
    }

    /// <summary>
    /// When a file is not a .cs file, we probe the first characters of the file for <c>#!</c>, and
    /// execute as a single file program if we find them.
    /// </summary>
    [Theory]
    [InlineData("Program")]
    [InlineData("Program.csx")]
    [InlineData("Program.vb")]
    public void NonCsFileExtensionWithShebang(string fileName)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, fileName), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello world");
            """);

        new DotnetCommand(Log, "run", fileName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("hello world");
    }

    /// <summary>
    /// When a file is not a .cs file, we probe the first characters of the file for <c>#!</c>, and
    /// fall back to normal <c>dotnet run</c> behavior if we don't find them.
    /// </summary>
    [Theory]
    [InlineData("Program")]
    [InlineData("Program.csx")]
    [InlineData("Program.vb")]
    public void NonCsFileExtensionWithNoShebang(string fileName)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, fileName), s_program);

        new DotnetCommand(Log, "run", fileName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                "--project"));
    }

    [Fact]
    public void MultipleEntryPoints()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        new DotnetCommand(Log, "run", "Program2.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program2");
    }

    /// <summary>
    /// When the entry-point file does not exist, fallback to normal <c>dotnet run</c> behavior.
    /// </summary>
    [Fact]
    public void NoCode()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                "--project"));
    }

    /// <summary>
    /// Cannot run a non-entry-point file.
    /// </summary>
    [Fact]
    public void ClassLibrary_EntryPointFileExists()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);

        new DotnetCommand(Log, "run", "Util.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS5001:"); // Program does not contain a static 'Main' method suitable for an entry point
    }

    /// <summary>
    /// When the entry-point file does not exist, fallback to normal <c>dotnet run</c> behavior.
    /// </summary>
    [Fact]
    public void ClassLibrary_EntryPointFileDoesNotExist()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);

        new DotnetCommand(Log, "run", "NonExistentFile.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                "--project"));
    }

    /// <summary>
    /// Other files in the folder are not part of the compilation.
    /// See <see href="https://github.com/dotnet/sdk/issues/51785"/>.
    /// </summary>
    [Fact]
    public void MultipleFiles_RunEntryPoint()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_programDependingOnUtil);
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS0103"); // The name 'Util' does not exist in the current context

        // This can be overridden.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:property EnableDefaultCompileItems=true
            {s_programDependingOnUtil}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, String from Util");
    }

    /// <summary>
    /// Setting EnableDefaultCompileItems=true via Directory.Build.props should not cause CS2002 warning.
    /// See <see href="https://github.com/dotnet/sdk/issues/51785"/>.
    /// </summary>
    [Fact]
    public void MultipleFiles_EnableDefaultCompileItemsViaDirectoryBuildProps()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_programDependingOnUtil);
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <PropertyGroup>
                    <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
                </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, String from Util");
    }

    /// <summary>
    /// Directives in other files are considered even if those files are included via manual MSBuild rather than <c>#:include</c>.
    /// </summary>
    [Fact]
    public void MultipleFiles_DirectivesInOtherFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "A.cs"), """
            Console.WriteLine(B.M());
            #if !DEBUG
            Console.WriteLine("Release config");
            #endif
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "B.cs"), """
            #:property Configuration=Release
            public static class B
            {
                public static string M() => "String from Util";
            }
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableTransitiveDirectives>true</ExperimentalFileBasedProgramEnableTransitiveDirectives>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="B.cs" />
              </ItemGroup>
            </Project>
            """);

        new DotnetCommand(Log, "run", "A.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                String from Util
                Release config
                """);
    }

    /// <summary>
    /// <c>dotnet run util.cs</c> fails if <c>util.cs</c> is not the entry-point.
    /// </summary>
    [Fact]
    public void MultipleFiles_RunLibraryFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_programDependingOnUtil);
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);

        new DotnetCommand(Log, "run", "Util.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS5001:"); // Program does not contain a static 'Main' method suitable for an entry point
    }

    /// <summary>
    /// If there are nested project files like
    /// <code>
    ///  app/file.cs
    ///  app/nested/x.csproj
    ///  app/nested/another.cs
    /// </code>
    /// executing <c>dotnet run app/file.cs</c> will include the nested <c>.cs</c> file in the compilation.
    /// Hence we could consider reporting an error in this situation.
    /// However, the same problem exists for normal builds with explicit project files
    /// and usually the build fails because there are multiple entry points or other clashes.
    /// </summary>
    [Fact]
    public void NestedProjectFiles()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        Directory.CreateDirectory(Path.Join(testInstance.Path, "nested"));
        File.WriteAllText(Path.Join(testInstance.Path, "nested", "App.csproj"), s_consoleProject);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");
    }

    /// <summary>
    /// <c>dotnet run folder/app.csproj</c> -> the argument is not recognized as an entry-point file
    /// (it does not have <c>.cs</c> file extension), so this fallbacks to normal <c>dotnet run</c> behavior.
    /// </summary>
    [Fact]
    public void RunNestedProjectFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        File.WriteAllText(Path.Join(testInstance.Path, "App.csproj"), s_consoleProject);

        var dirName = Path.GetFileName(testInstance.Path);

        var workDir = Path.GetDirectoryName(testInstance.Path)!;

        new DotnetCommand(Log, "run", $"{dirName}/App.csproj")
            .WithWorkingDirectory(workDir)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                workDir,
                "--project"));
    }
}
