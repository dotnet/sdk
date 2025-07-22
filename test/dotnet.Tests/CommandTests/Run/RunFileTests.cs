// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests(ITestOutputHelper log) : SdkTest(log)
{
    private static readonly string s_program = """
        if (args.Length > 0)
        {
            Console.WriteLine("echo args:" + string.Join(";", args));
        }
        Console.WriteLine("Hello from " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
        #if !DEBUG
        Console.WriteLine("Release config");
        #endif
        #if CUSTOM_DEFINE
        Console.WriteLine("Custom define");
        #endif
        """;

    private static readonly string s_programDependingOnUtil = """
        if (args.Length > 0)
        {
            Console.WriteLine("echo args:" + string.Join(";", args));
        }
        Console.WriteLine("Hello, " + Util.GetMessage());
        """;

    private static readonly string s_util = """
        static class Util
        {
            public static string GetMessage()
            {
                return "String from Util";
            }
        }
        """;

    private static readonly string s_consoleProject = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;

    private static readonly string s_launchSettings = """
        {
            "profiles": {
                "TestProfile1": {
                    "commandName": "Project",
                    "environmentVariables": {
                        "Message": "TestProfileMessage1"
                    }
                },
                "TestProfile2": {
                    "commandName": "Project",
                    "environmentVariables": {
                        "Message": "TestProfileMessage2"
                    }
                }
            }
        }
        """;

    private static bool HasCaseInsensitiveFileSystem
    {
        get
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }

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
        var testInstance = _testAssetsManager.CreateTestDirectory();

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
                    RunCommandParser.ProjectOption.Name));
        }
    }

    /// <summary>
    /// <c>dotnet file.cs</c> is equivalent to <c>dotnet run file.cs</c>.
    /// </summary>
    [Fact]
    public void FilePath_WithoutRun()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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

        new DotnetCommand(Log, "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                Release config
                """);

        new DotnetCommand(Log, "Program.cs", "-c", "Debug")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                """);
    }

    /// <summary>
    /// Casing of the argument is used for the output binary name.
    /// </summary>
    [Fact]
    public void FilePath_DifferentCasing()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
                    RunCommandParser.ProjectOption.Name));
        }
    }

    /// <summary>
    /// <c>dotnet run folder/file.cs</c> succeeds without a project file.
    /// </summary>
    [Fact]
    public void FilePath_OutsideWorkDir()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
    [InlineData("build", "MSB1003")]
    // dotnet watch: Could not find a MSBuild project file in '...'. Specify which project to use with the --project option.
    [InlineData("watch", "--project")]
    public void Precedence_BuiltInCommand(string cmd, string error)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, cmd), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello 1");
            """);
        File.WriteAllText(Path.Join(testInstance.Path, $"dotnet-{cmd}"), """
            #!/usr/bin/env dotnet
            Console.WriteLine("hello 2");
            """);

        // dotnet build -> built-in command
        new DotnetCommand(Log, cmd)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(error);

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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        new DotnetCommand(Log, "run", "-")
            .WithStandardInput("""
                Console.WriteLine("Hello from stdin");
                Console.WriteLine("Read: " + (Console.ReadLine() ?? "null"));
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from stdin
                Read: null
                """);
    }

    [Fact]
    public void ReadFromStdin_NoBuild()
    {
        new DotnetCommand(Log, "run", "-", "--no-build")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidOptionForStdin, RunCommandParser.NoBuildOption.Name));
    }

    /// <summary>
    /// <c>dotnet run -- -</c> should NOT read the C# file from stdin,
    /// the hyphen should be considred an app argument instead since it's after <c>--</c>.
    /// </summary>
    [Fact]
    public void ReadFromStdin_AfterDoubleDash()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        new DotnetCommand(Log, "run", "--", "-")
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardInput("""Console.WriteLine("stdin code");""")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.RunCommandExceptionNoProjects, testInstance.Path, RunCommandParser.ProjectOption.Name));
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        path ??= testInstance.Path;

        new DotnetCommand(Log, "run", path)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                RunCommandParser.ProjectOption.Name));
    }

    /// <summary>
    /// <c>dotnet run app.csproj</c> fails if app.csproj does not exist.
    /// </summary>
    [Fact]
    public void ProjectPath_DoesNotExist()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "./App.csproj")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                RunCommandParser.ProjectOption.Name));
    }

    /// <summary>
    /// <c>dotnet run app.csproj</c> where app.csproj exists
    /// runs the project and passes 'app.csproj' as an argument.
    /// </summary>
    [Fact]
    public void ProjectPath_Exists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, fileName), s_program);

        new DotnetCommand(Log, "run", fileName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                RunCommandParser.ProjectOption.Name));
    }

    [Fact]
    public void MultipleEntryPoints()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                RunCommandParser.ProjectOption.Name));
    }

    /// <summary>
    /// Cannot run a non-entry-point file.
    /// </summary>
    [Fact]
    public void ClassLibrary_EntryPointFileExists()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), s_util);

        new DotnetCommand(Log, "run", "NonExistentFile.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                RunCommandParser.ProjectOption.Name));
    }

    /// <summary>
    /// Other files in the folder are not part of the compilation.
    /// </summary>
    [Fact]
    public void MultipleFiles_RunEntryPoint()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
            // warning CS2002: Source file 'Program.cs' specified multiple times
            .And.HaveStdOutContaining("warning CS2002")
            .And.HaveStdOutContaining("Hello, String from Util");
    }

    /// <summary>
    /// <c>dotnet run util.cs</c> fails if <c>util.cs</c> is not the entry-point.
    /// </summary>
    [Fact]
    public void MultipleFiles_RunLibraryFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
                RunCommandParser.ProjectOption.Name));
    }

    /// <summary>
    /// Main method is supported just like top-level statements.
    /// </summary>
    [Fact]
    public void MainMethod()
    {
        var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
        File.Delete(Path.Join(testInstance.Path, "MSBuildTestApp.csproj"));

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello World!");
    }

    /// <summary>
    /// Empty file does not contain entry point, so that's an error.
    /// </summary>
    [Fact]
    public void EmptyFile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), string.Empty);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS5001:"); // Program does not contain a static 'Main' method suitable for an entry point
    }

    /// <summary>
    /// Implicit build files have an effect.
    /// </summary>
    [Fact]
    public void DirectoryBuildProps()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <PropertyGroup>
                    <AssemblyName>TestName</AssemblyName>
                </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from TestName");
    }

    /// <summary>
    /// Command-line arguments should be passed through.
    /// </summary>
    [Theory]
    [InlineData("other;args", "other;args")]
    [InlineData("--;other;args", "other;args")]
    [InlineData("--appArg", "--appArg")]
    [InlineData("-c;Debug;--xyz", "--xyz")]
    public void Arguments_PassThrough(string input, string output)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, ["run", "Program.cs", .. input.Split(';')])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                echo args:{output}
                Hello from Program
                """);
    }

    /// <summary>
    /// <c>dotnet run --unknown-arg file.cs</c> fallbacks to normal <c>dotnet run</c> behavior.
    /// </summary>
    [Fact]
    public void Arguments_Unrecognized()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, ["run", "--arg", "Program.cs"])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                RunCommandParser.ProjectOption.Name));
    }

    /// <summary>
    /// <c>dotnet run --some-known-arg file.cs</c> is supported.
    /// </summary>
    [Theory, CombinatorialData]
    public void Arguments_Recognized(bool beforeFile)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        string[] args = beforeFile
            ? ["run", "-c", "Release", "Program.cs", "more", "args"]
            : ["run", "Program.cs", "-c", "Release", "more", "args"];

        new DotnetCommand(Log, args)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:more;args
                Hello from Program
                Release config
                """);
    }

    /// <summary>
    /// <c>dotnet run --bl file.cs</c> produces a binary log.
    /// </summary>
    [Theory, CombinatorialData]
    public void BinaryLog_Run(bool beforeFile)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        string[] args = beforeFile
            ? ["run", "-bl", "Program.cs"]
            : ["run", "Program.cs", "-bl"];

        new DotnetCommand(Log, args)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly)
            .Select(f => f.Name)
            .Should().BeEquivalentTo(["msbuild.binlog", "msbuild-dotnet-run.binlog"]);
    }

    [Theory, CombinatorialData]
    public void BinaryLog_Build([CombinatorialValues("restore", "build")] string command, bool beforeFile)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        string[] args = beforeFile
            ? [command, "-bl", "Program.cs"]
            : [command, "Program.cs", "-bl"];

        new DotnetCommand(Log, args)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly)
            .Select(f => f.Name)
            .Should().BeEquivalentTo(["msbuild.binlog"]);
    }

    [Theory]
    [InlineData("-bl")]
    [InlineData("-BL")]
    [InlineData("-bl:msbuild.binlog")]
    [InlineData("/bl")]
    [InlineData("/bl:msbuild.binlog")]
    [InlineData("--binaryLogger")]
    [InlineData("--binaryLogger:msbuild.binlog")]
    [InlineData("-bl:another.binlog")]
    public void BinaryLog_ArgumentForms(string arg)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", arg)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        var fileName = arg.Split(':', 2) is [_, { Length: > 0 } value] ? Path.GetFileNameWithoutExtension(value) : "msbuild";

        new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly)
            .Select(f => f.Name)
            .Should().BeEquivalentTo([$"{fileName}.binlog", $"{fileName}-dotnet-run.binlog"]);
    }

    [Fact]
    public void BinaryLog_Multiple()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", "-bl:one.binlog", "two.binlog", "/bl:three.binlog")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:two.binlog
                Hello from Program
                """);

        new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly)
            .Select(f => f.Name)
            .Should().BeEquivalentTo(["three.binlog", "three-dotnet-run.binlog"]);
    }

    [Fact]
    public void BinaryLog_WrongExtension()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", "-bl:test.test")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining("test.test"); // Invalid binary logger parameter(s): "test.test"

        new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly)
            .Select(f => f.Name)
            .Should().BeEmpty();
    }

    /// <summary>
    /// <c>dotnet run file.cs</c> should not produce a binary log.
    /// </summary>
    [Fact]
    public void BinaryLog_NotSpecified()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly)
            .Select(f => f.Name)
            .Should().BeEmpty();
    }

    /// <summary>
    /// Binary logs from our in-memory projects should have evaluation data.
    /// </summary>
    [Fact]
    public void BinaryLog_EvaluationData()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        string binaryLogPath = Path.Join(testInstance.Path, "msbuild.binlog");
        new FileInfo(binaryLogPath).Should().Exist();

        var records = BinaryLog.ReadRecords(binaryLogPath).ToList();
        records.Any(static r => r.Args is ProjectEvaluationStartedEventArgs).Should().BeTrue();
        records.Any(static r => r.Args is ProjectEvaluationFinishedEventArgs).Should().BeTrue();
    }

    /// <summary>
    /// Default projects include embedded resources by default.
    /// </summary>
    [Fact]
    public void EmbeddedResource()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        string code = """
            using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Program.Resources.resources");

            if (stream is null)
            {
                Console.WriteLine("Resource not found");
                return;
            }

            using var reader = new System.Resources.ResourceReader(stream);
            Console.WriteLine(reader.Cast<System.Collections.DictionaryEntry>().Single());
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), code);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), """
            <root>
              <data name="MyString">
                <value>TestValue</value>
              </data>
            </root>
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                [MyString, TestValue]
                """);

        // This behavior can be overridden.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:property EnableDefaultEmbeddedResourceItems=false
            {code}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Resource not found
                """);
    }

    [Fact]
    public void NoRestore_01()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
    public void NoBuild_01()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
    public void Publish()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
    }

    [Fact]
    public void PublishWithCustomTarget()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

        var artifactsDir = new DirectoryInfo(VirtualProjectBuildingCommand.GetArtifactsPath(programFile));
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
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

    [Fact]
    public void LaunchProfile()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program + """

            Console.WriteLine($"Message: '{Environment.GetEnvironmentVariable("Message")}'");
            """);
        Directory.CreateDirectory(Path.Join(testInstance.Path, "Properties"));
        File.WriteAllText(Path.Join(testInstance.Path, "Properties", "launchSettings.json"), s_launchSettings);

        new DotnetCommand(Log, "run", "--no-launch-profile", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Program
                Message: ''
                """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Hello from Program
                Message: 'TestProfileMessage1'
                """);

        new DotnetCommand(Log, "run", "-lp", "TestProfile2", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                Hello from Program
                Message: 'TestProfileMessage2'
                """);
    }

    [Fact]
    public void Define_01()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
    public void ProjectReference(string arg)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

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
            Console.WriteLine(Lib.LibClass.GetMessage());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Lib");
    }

    [Fact]
    public void ProjectReference_Errors()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:project wrong.csproj
            """);

        // Project file does not exist.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidProjectDirective,
                $"{Path.Join(testInstance.Path, "Program.cs")}:1",
                string.Format(CliStrings.CouldNotFindProjectOrDirectory, Path.Join(testInstance.Path, "wrong.csproj"))));

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:project dir/
            """);

        // Project directory does not exist.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidProjectDirective,
                $"{Path.Join(testInstance.Path, "Program.cs")}:1",
                string.Format(CliStrings.CouldNotFindProjectOrDirectory, Path.Join(testInstance.Path, "dir/"))));

        Directory.CreateDirectory(Path.Join(testInstance.Path, "dir"));

        // Directory exists but has no project file.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidProjectDirective,
                $"{Path.Join(testInstance.Path, "Program.cs")}:1",
                string.Format(CliStrings.CouldNotFindAnyProjectInDirectory, Path.Join(testInstance.Path, "dir/"))));

        File.WriteAllText(Path.Join(testInstance.Path, "dir", "proj1.csproj"), "<Project />");
        File.WriteAllText(Path.Join(testInstance.Path, "dir", "proj2.csproj"), "<Project />");

        // Directory exists but has multiple project files.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidProjectDirective,
                $"{Path.Join(testInstance.Path, "Program.cs")}:1",
                string.Format(CliStrings.MoreThanOneProjectInDirectory, Path.Join(testInstance.Path, "dir/"))));
    }

    [Fact]
    public void UpToDate()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        Build(expectedUpToDate: false);

        Build(expectedUpToDate: true);

        Build(expectedUpToDate: true);

        // Change the source file (a rebuild is necessary).
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program + " ");

        Build(expectedUpToDate: false);

        Build(expectedUpToDate: true);

        // Change an unrelated source file (no rebuild necessary).
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "test");

        Build(expectedUpToDate: true);

        // Add an implicit build file (a rebuild is necessary).
        string buildPropsFile = Path.Join(testInstance.Path, "Directory.Build.props");
        File.WriteAllText(buildPropsFile, """
            <Project>
                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);CUSTOM_DEFINE</DefineConstants>
                </PropertyGroup>
            </Project>
            """);

        Build(expectedUpToDate: false, expectedOutput: """
            Hello from Program
            Custom define
            """);

        Build(expectedUpToDate: true, expectedOutput: """
            Hello from Program
            Custom define
            """);

        // Change the implicit build file (a rebuild is necessary).
        string importedFile = Path.Join(testInstance.Path, "Settings.props");
        File.WriteAllText(importedFile, """
            <Project>
            </Project>
            """);
        File.WriteAllText(buildPropsFile, """
            <Project>
                <Import Project="Settings.props" />
            </Project>
            """);

        Build(expectedUpToDate: false);

        // Change the imported build file (this is not recognized).
        File.WriteAllText(importedFile, """
            <Project>
                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);CUSTOM_DEFINE</DefineConstants>
                </PropertyGroup>
            </Project>
            """);

        Build(expectedUpToDate: true);

        // Force rebuild.
        Build(expectedUpToDate: false, args: ["--no-cache"], expectedOutput: """
            Hello from Program
            Custom define
            """);

        // Remove an implicit build file (a rebuild is necessary).
        File.Delete(buildPropsFile);
        Build(expectedUpToDate: false);

        // Force rebuild.
        Build(expectedUpToDate: false, args: ["--no-cache"]);

        Build(expectedUpToDate: true);

        // Pass argument (no rebuild necessary).
        Build(expectedUpToDate: true, args: ["--", "test-arg"], expectedOutput: """
            echo args:test-arg
            Hello from Program
            """);

        // Change config (a rebuild is necessary).
        Build(expectedUpToDate: false, args: ["-c", "Release"], expectedOutput: """
            Hello from Program
            Release config
            """);

        // Keep changed config (no rebuild necessary).
        Build(expectedUpToDate: true, args: ["-c", "Release"], expectedOutput: """
            Hello from Program
            Release config
            """);

        // Change config back (a rebuild is necessary).
        Build(expectedUpToDate: false);

        // Build with a failure.
        new DotnetCommand(Log, ["run", "Program.cs", "-p:LangVersion=Invalid"])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS1617"); // Invalid option 'Invalid' for /langversion.

        // A rebuild is necessary since the last build failed.
        Build(expectedUpToDate: false);

        void Build(bool expectedUpToDate, ReadOnlySpan<string> args = default, string expectedOutput = "Hello from Program")
        {
            new DotnetCommand(Log, ["run", "Program.cs", "-bl", .. args])
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOut(expectedUpToDate
                    ? $"""
                        {CliCommandStrings.NoBinaryLogBecauseUpToDate}
                        {expectedOutput}
                        """
                    : expectedOutput);

            var binlogs = new DirectoryInfo(testInstance.Path)
                .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly);

            binlogs.Select(f => f.Name)
                .Should().BeEquivalentTo(
                    expectedUpToDate
                        ? ["msbuild-dotnet-run.binlog"]
                        : ["msbuild.binlog", "msbuild-dotnet-run.binlog"]);

            foreach (var binlog in binlogs)
            {
                binlog.Delete();
            }
        }
    }

    [Fact]
    public void UpToDate_InvalidOptions()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", "--no-cache", "--no-build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidOptionCombination, RunCommandParser.NoCacheOption.Name, RunCommandParser.NoBuildOption.Name));
    }

    private static string ToJson(string s) => JsonSerializer.Serialize(s);

    /// <summary>
    /// Simplifies using interpolated raw strings with nested JSON,
    /// e.g, in <c>$$"""{x:{y:1}}"""</c>, the <c>}}</c> would result in an error.
    /// </summary>
    private const string nop = "";

    [Fact]
    public void Api()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            #!/program
            #:sdk Microsoft.NET.Sdk
            #:sdk Aspire.Hosting.Sdk@9.1.0
            #:property TargetFramework=net11.0
            #:package System.CommandLine@2.0.0-beta4.22272.1
            #:property LangVersion=preview
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <!-- We need to explicitly import Sdk props/targets so we can override the targets below. -->
                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                      <Import Project="Sdk.props" Sdk="Aspire.Hosting.Sdk" Version="9.1.0" />

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                      </PropertyGroup>

                      <PropertyGroup>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                      </PropertyGroup>

                      <PropertyGroup>
                        <TargetFramework>net11.0</TargetFramework>
                        <LangVersion>preview</LangVersion>
                      </PropertyGroup>

                      <PropertyGroup>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
                      </ItemGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
                      <Import Project="Sdk.targets" Sdk="Aspire.Hosting.Sdk" Version="9.1.0" />

                    {VirtualProjectBuildingCommand.TargetOverrides}

                    </Project>

                    """)}},"Diagnostics":[]}
                """);
    }

    [Fact]
    public void Api_Diagnostic_01()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            Console.WriteLine();
            #:property LangVersion=preview
            """);

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <!-- We need to explicitly import Sdk props/targets so we can override the targets below. -->
                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                      </PropertyGroup>

                      <PropertyGroup>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                      </PropertyGroup>

                      <PropertyGroup>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

                    {VirtualProjectBuildingCommand.TargetOverrides}

                    </Project>

                    """)}},"Diagnostics":
                [{"Location":{
                "Path":{{ToJson(programPath)}},
                "Span":{"Start":{"Line":1,"Character":0},"End":{"Line":1,"Character":30}{{nop}}}{{nop}}},
                "Message":{{ToJson(string.Format(CliCommandStrings.CannotConvertDirective, $"{programPath}:2"))}}}]}
                """.ReplaceLineEndings(""));
    }

    [Fact]
    public void Api_Diagnostic_02()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            #:unknown directive
            Console.WriteLine();
            """);

        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetProject","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":"/artifacts"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($$"""
                {"$type":"Project","Version":1,"Content":{{ToJson($"""
                    <Project>

                      <PropertyGroup>
                        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        <ArtifactsPath>/artifacts</ArtifactsPath>
                        <PublishDir>artifacts/$(MSBuildProjectName)</PublishDir>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <!-- We need to explicitly import Sdk props/targets so we can override the targets below. -->
                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                      </PropertyGroup>

                      <PropertyGroup>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                      </PropertyGroup>

                      <PropertyGroup>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Include="{programPath}" />
                      </ItemGroup>

                      <ItemGroup>
                        <RuntimeHostConfigurationOption Include="EntryPointFilePath" Value="{programPath}" />
                        <RuntimeHostConfigurationOption Include="EntryPointFileDirectoryPath" Value="{testInstance.Path}" />
                      </ItemGroup>

                      <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />

                    {VirtualProjectBuildingCommand.TargetOverrides}

                    </Project>

                    """)}},"Diagnostics":
                [{"Location":{
                "Path":{{ToJson(programPath)}},
                "Span":{"Start":{"Line":0,"Character":0},"End":{"Line":1,"Character":0}{{nop}}}{{nop}}},
                "Message":{{ToJson(string.Format(CliCommandStrings.UnrecognizedDirective, "unknown", $"{programPath}:1"))}}}]}
                """.ReplaceLineEndings(""));
    }

    [Fact]
    public void Api_Error()
    {
        new DotnetCommand(Log, "run-api")
            .WithStandardInput("""
                {"$type":"Unknown1"}
                {"$type":"Unknown2"}
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("""
                {"$type":"Error","Version":1,"Message":
                """)
            .And.HaveStdOutContaining("Unknown1")
            .And.HaveStdOutContaining("Unknown2");
    }

    [Fact]
    public void Api_RunCommand()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, """
            Console.WriteLine();
            """);

        string artifactsPath = OperatingSystem.IsWindows() ? @"C:\artifacts" : "/artifacts";
        string executablePath = OperatingSystem.IsWindows() ? @"C:\artifacts\bin\debug\Program.exe" : "/artifacts/bin/debug/Program";
        new DotnetCommand(Log, "run-api")
            .WithStandardInput($$"""
                {"$type":"GetRunCommand","EntryPointFileFullPath":{{ToJson(programPath)}},"ArtifactsPath":{{ToJson(artifactsPath)}}}
                """)
            .Execute()
            .Should().Pass()
            // DOTNET_ROOT environment variable is platform dependent so we don't verify it fully for simplicity
            .And.HaveStdOutContaining($$"""
                {"$type":"RunCommand","Version":1,"ExecutablePath":{{ToJson(executablePath)}},"CommandLineArguments":"","WorkingDirectory":"","EnvironmentVariables":{"DOTNET_ROOT
                """);
    }

    [Fact]
    public void EntryPointFilePath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, """"
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"""EntryPointFilePath: {entryPointFilePath}""");
            """");

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {filePath}");
    }

    [Fact]
    public void EntryPointFileDirectoryPath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """"
            var entryPointFileDirectoryPath = AppContext.GetData("EntryPointFileDirectoryPath") as string;
            Console.WriteLine($"""EntryPointFileDirectoryPath: {entryPointFileDirectoryPath}""");
            """");

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFileDirectoryPath: {testInstance.Path}");
    }

    [Fact]
    public void EntryPointFilePath_WithRelativePath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var fileName = "Program.cs";
        File.WriteAllText(Path.Join(testInstance.Path, fileName), """
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
            """);

        var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Join(testInstance.Path, fileName));
        new DotnetCommand(Log, "run", relativePath)
            .WithWorkingDirectory(Directory.GetCurrentDirectory())
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {Path.GetFullPath(relativePath)}");
    }

    [Fact]
    public void EntryPointFilePath_WithSpacesInPath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var dirWithSpaces = Path.Join(testInstance.Path, "dir with spaces");
        Directory.CreateDirectory(dirWithSpaces);
        var filePath = Path.Join(dirWithSpaces, "Program.cs");
        File.WriteAllText(filePath, """
        var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
        Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
        """);

        new DotnetCommand(Log, "run", filePath)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {filePath}");
    }

    [Fact]
    public void EntryPointFileDirectoryPath_WithDotSlash()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var fileName = "Program.cs";
        File.WriteAllText(Path.Join(testInstance.Path, fileName), """
        var entryPointFileDirectoryPath = AppContext.GetData("EntryPointFileDirectoryPath") as string;
        Console.WriteLine($"EntryPointFileDirectoryPath: {entryPointFileDirectoryPath}");
        """);

        new DotnetCommand(Log, "run", $"./{fileName}")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFileDirectoryPath: {testInstance.Path}");
    }

    [Fact]
    public void EntryPointFilePath_WithUnicodeCharacters()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var unicodeFileName = "Программа.cs";
        var filePath = Path.Join(testInstance.Path, unicodeFileName);
        File.WriteAllText(filePath, """
        var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
        Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
        """);

        new DotnetCommand(Log, "run", unicodeFileName)
            .WithWorkingDirectory(testInstance.Path)
            .WithStandardOutputEncoding(Encoding.UTF8)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {filePath}");
    }
}
