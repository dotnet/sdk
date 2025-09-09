// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Text.Json;
using Basic.CompilerLog.Util;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests(ITestOutputHelper log) : SdkTest(log)
{
    private static readonly string s_program = /* lang=C#-Test */ """
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

    private static readonly string s_programDependingOnUtil = /* lang=C#-Test */ """
        if (args.Length > 0)
        {
            Console.WriteLine("echo args:" + string.Join(";", args));
        }
        Console.WriteLine("Hello, " + Util.GetMessage());
        """;

    private static readonly string s_util = /* lang=C#-Test */ """
        static class Util
        {
            public static string GetMessage()
            {
                return "String from Util";
            }
        }
        """;

    private static readonly string s_programReadingEmbeddedResource = /* lang=C#-Test */ """
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault();

        if (resourceName is null)
        {
            Console.WriteLine("Resource not found");
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new System.Resources.ResourceReader(stream);
        Console.WriteLine(reader.Cast<System.Collections.DictionaryEntry>().Single());
        """;

    private static readonly string s_resx = """
        <root>
          <data name="MyString">
            <value>TestValue</value>
          </data>
        </root>
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

    /// <summary>
    /// Used when we need an out-of-tree base test directory to avoid having implicit build files
    /// like Directory.Build.props in scope and negating the optimizations we want to test.
    /// </summary>
    private static string OutOfTreeBaseDirectory => field ??= PrepareOutOfTreeBaseDirectory();

    private static bool HasCaseInsensitiveFileSystem
    {
        get
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }

    /// <inheritdoc cref="OutOfTreeBaseDirectory"/>
    private static string PrepareOutOfTreeBaseDirectory()
    {
        string outOfTreeBaseDirectory = TestPathUtility.ResolveTempPrefixLink(Path.Join(Path.GetTempPath(), "dotnetSdkTests"));
        Directory.CreateDirectory(outOfTreeBaseDirectory);

        // Create NuGet.config in our out-of-tree base directory.
        var sourceNuGetConfig = Path.Join(TestContext.Current.TestExecutionDirectory, "NuGet.config");
        var targetNuGetConfig = Path.Join(outOfTreeBaseDirectory, "NuGet.config");
        File.Copy(sourceNuGetConfig, targetNuGetConfig, overwrite: true);

        // Check there are no implicit build files that would prevent testing optimizations.
        VirtualProjectBuildingCommand.CollectImplicitBuildFiles(new DirectoryInfo(outOfTreeBaseDirectory), [], out var exampleMSBuildFile);
        exampleMSBuildFile.Should().BeNull(because: "there should not be any implicit build files in the temp directory or its parents " +
            "so we can test optimizations that would be disabled with implicit build files present");

        return outOfTreeBaseDirectory;
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

    [Fact]
    public void ReadFromStdin_LaunchProfile()
    {
        new DotnetCommand(Log, "run", "-", "--launch-profile=test")
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.InvalidOptionForStdin, RunCommandParser.LaunchProfileOption.Name));
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

    [Fact]
    public void ProjectInCurrentDirectory_NoRunVerb()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
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
        var testInstance = _testAssetsManager.CreateTestDirectory();
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

    [Fact]
    public void ComputeRunArguments_Success()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.targets"), """
            <Project>
              <Target Name="_SetCustomRunArgs" BeforeTargets="ComputeRunArguments">
                <PropertyGroup>
                  <RunArguments>$(RunArguments) extended</RunArguments>
                </PropertyGroup>
              </Target>
            </Project>
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:extended
                Hello from Program
                """);
    }

    [Fact]
    public void ComputeRunArguments_Failure()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.targets"), """
            <Project>
              <Target Name="_SetCustomRunArgs" BeforeTargets="ComputeRunArguments">
                <Error Code="MYAPP001" Text="Custom error" />
              </Target>
            </Project>
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("""
                MYAPP001: Custom error
                """)
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);
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
            .Should().BeEquivalentTo(["msbuild.binlog"]);
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
            .Should().BeEquivalentTo([$"{fileName}.binlog"]);
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
            .Should().BeEquivalentTo(["three.binlog"]);
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

        // There should be at least two - one for restore, one for build.
        // But the restore targets might re-evaluate the project via inner MSBuild task invocations.
        records.Count(static r => r.Args is ProjectEvaluationStartedEventArgs).Should().BeGreaterThanOrEqualTo(2);
        records.Count(static r => r.Args is ProjectEvaluationFinishedEventArgs).Should().BeGreaterThanOrEqualTo(2);
    }

    [Theory, CombinatorialData]
    public void TerminalLogger(bool on)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        var result = new DotnetCommand(Log, "run", "Program.cs", "--no-cache")
            .WithWorkingDirectory(testInstance.Path)
            .WithEnvironmentVariable("MSBUILDTERMINALLOGGER", on ? "on" : "off")
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("Hello from Program");

        const string terminalLoggerSubstring = "\x1b";
        if (on)
        {
            result.And.HaveStdOutContaining(terminalLoggerSubstring);
        }
        else
        {
            result.And.NotHaveStdOutContaining(terminalLoggerSubstring);
        }
    }

    [Fact]
    public void Verbosity_Run()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        new DotnetCommand(Log, "run", "Program.cs", "--no-cache")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            // no additional build messages
            .And.HaveStdOut("Hello from Program")
            .And.NotHaveStdOutContaining("Program.dll")
            .And.NotHaveStdErr();
    }

    [Fact] // https://github.com/dotnet/sdk/issues/50227
    public void Verbosity_Build()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programFile, s_program);

        new DotnetCommand(Log, "build", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            // should print path to the built DLL
            .And.HaveStdOutContaining("Program.dll");
    }

    [Fact]
    public void Verbosity_CompilationDiagnostics()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            string x = null;
            Console.WriteLine("ran" + x);
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            // warning CS8600: Converting null literal or possible null value to non-nullable type.
            .And.HaveStdOutContaining("warning CS8600")
            .And.HaveStdOutContaining("ran");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.Write
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS1002: ; expected
            .And.HaveStdOutContaining("error CS1002")
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);
    }

    /// <summary>
    /// Default projects include embedded resources by default.
    /// </summary>
    [Fact]
    public void EmbeddedResource()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_programReadingEmbeddedResource);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);

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
            {s_programReadingEmbeddedResource}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Resource not found
                """);
    }

    /// <summary>
    /// Scripts in repo root should not include <c>.resx</c> files.
    /// Part of <see href="https://github.com/dotnet/sdk/issues/49826"/>.
    /// </summary>
    [Theory, CombinatorialData]
    public void EmbeddedResource_AlongsideProj([CombinatorialValues("sln", "slnx", "csproj", "vbproj", "shproj", "proj")] string ext)
    {
        bool considered = ext is "sln" or "slnx" or "csproj";

        var testInstance = _testAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_programReadingEmbeddedResource);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);
        File.WriteAllText(Path.Join(testInstance.Path, $"repo.{ext}"), "");

        new DotnetCommand(Log, "run", "--file", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(considered ? "Resource not found" : "[MyString, TestValue]");
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
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "restore", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErr(string.Format(CliCommandStrings.StaticGraphRestoreNotSupported, $"{programFile}:1"));
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
    public void Pack()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "MyFileBasedTool.cs");
        File.WriteAllText(programFile, """
            #:property PackAsTool=true
            Console.WriteLine($"Hello; EntryPointFilePath set? {AppContext.GetData("EntryPointFilePath") is string}");
            """);

        // Run unpacked.
        new DotnetCommand(Log, "run", "MyFileBasedTool.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello; EntryPointFilePath set? True");

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("Hello; EntryPointFilePath set? False");
    }

    [Fact]
    public void Pack_CustomPath()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "MyFileBasedTool.cs");
        File.WriteAllText(programFile, """
            #:property PackAsTool=true
            #:property PackageOutputPath=custom
            Console.WriteLine($"Hello; EntryPointFilePath set? {AppContext.GetData("EntryPointFilePath") is string}");
            """);

        // Run unpacked.
        new DotnetCommand(Log, "run", "MyFileBasedTool.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello; EntryPointFilePath set? True");

        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(programFile);
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

        // quiet runs here so that launch-profile useage messages don't impact test assertions
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

        new DotnetCommand(Log, "run", "-v", "q",  "Second.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Hello from Second
                Message: 'Second1'
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

    /// <summary>
    /// Verifies that msbuild-based runs use CSC args equivalent to csc-only runs.
    /// Can regenerate CSC arguments template in <see cref="CSharpCompilerCommand"/>.
    /// </summary>
    [Fact]
    public void CscArguments()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        const string programName = "TestProgram";
        const string fileName = $"{programName}.cs";
        string entryPointPath = Path.Join(testInstance.Path, fileName);
        File.WriteAllText(entryPointPath, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(entryPointPath);
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
        string sdkPath = NormalizePath(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest);
        string dotNetRootPath = NormalizePath(TestContext.Current.ToolsetUnderTest.DotNetRoot);
        string nuGetCachePath = NormalizePath(TestContext.Current.NuGetCachePath!);
        string artifactsDirNormalized = NormalizePath(artifactsDir);
        string objPath = $"{artifactsDirNormalized}/obj/debug";
        string entryPointPathNormalized = NormalizePath(entryPointPath);
        var msbuildArgsToVerify = new List<string>();
        var nuGetPackageFilePaths = new List<string>();
        var code = new StringBuilder();
        code.AppendLine($$"""
            // Licensed to the .NET Foundation under one or more agreements.
            // The .NET Foundation licenses this file to you under the MIT license.

            namespace Microsoft.DotNet.Cli.Commands.Run;

            // Generated by test `{{nameof(RunFileTests)}}.{{nameof(CscArguments)}}`.
            partial class CSharpCompilerCommand
            {
                private IEnumerable<string> GetCscArguments(
                    string fileNameWithoutExtension,
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
                rewritten = rewritten.Replace(programName, "{fileNameWithoutExtension}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Use variable runtime version.
            if (rewritten.Contains(CSharpCompilerCommand.RuntimeVersion, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(CSharpCompilerCommand.RuntimeVersion, "{" + nameof(CSharpCompilerCommand.RuntimeVersion) + "}", StringComparison.OrdinalIgnoreCase);
                needsInterpolation = true;
            }

            // Ignore `/analyzerconfig` which is not variable (so it comes from the machine or sdk repo).
            if (!needsInterpolation && arg.StartsWith("/analyzerconfig", StringComparison.Ordinal))
            {
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
            }
            """);

        // Save the code.
        var codeFolder = new DirectoryInfo(Path.Join(
            TestContext.Current.ToolsetUnderTest.RepoRoot,
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
            .Select(static a => NormalizePathArg(RemoveQuotes(a)));
        Log.WriteLine("CSC-only args:");
        Log.WriteLine(string.Join(Environment.NewLine, normalizedCscOnlyArgs));
        Log.WriteLine("MSBuild args:");
        Log.WriteLine(string.Join(Environment.NewLine, msbuildArgsToVerify));
        normalizedCscOnlyArgs.Should().Equal(msbuildArgsToVerify,
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
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        string entryPointPath = Path.Join(testInstance.Path, fileName);
        File.WriteAllText(entryPointPath, $"""
            #!/test
            {s_program}
            """);

        string programName = Path.GetFileNameWithoutExtension(fileName);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(entryPointPath);
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
                Log.WriteLine($"File differs between MSBuild and CSC-only runs (if this is expected, find the template in '{nameof(CSharpCompilerCommand)}.cs' and update it): {cscOnlyFile}");
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

    [Fact]
    public void UpToDate()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("Hello v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "Hello v1");

        Build(testInstance, BuildLevel.None, expectedOutput: "Hello v1");

        Build(testInstance, BuildLevel.None, expectedOutput: "Hello v1");

        // Change the source file (a rebuild is necessary).
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        Build(testInstance, BuildLevel.Csc);

        Build(testInstance, BuildLevel.None);

        // Change an unrelated source file (no rebuild necessary).
        File.WriteAllText(Path.Join(testInstance.Path, "Program2.cs"), "test");

        Build(testInstance, BuildLevel.None);

        // Add an implicit build file (a rebuild is necessary).
        string buildPropsFile = Path.Join(testInstance.Path, "Directory.Build.props");
        File.WriteAllText(buildPropsFile, """
            <Project>
                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);CUSTOM_DEFINE</DefineConstants>
                </PropertyGroup>
            </Project>
            """);

        Build(testInstance, BuildLevel.All, expectedOutput: """
            Hello from Program
            Custom define
            """);

        Build(testInstance, BuildLevel.None, expectedOutput: """
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

        Build(testInstance, BuildLevel.All);

        // Change the imported build file (this is not recognized).
        File.WriteAllText(importedFile, """
            <Project>
                <PropertyGroup>
                    <DefineConstants>$(DefineConstants);CUSTOM_DEFINE</DefineConstants>
                </PropertyGroup>
            </Project>
            """);

        Build(testInstance, BuildLevel.None);

        // Force rebuild.
        Build(testInstance, BuildLevel.All, args: ["--no-cache"], expectedOutput: """
            Hello from Program
            Custom define
            """);

        // Remove an implicit build file (a rebuild is necessary).
        File.Delete(buildPropsFile);
        Build(testInstance, BuildLevel.Csc);

        // Force rebuild.
        Build(testInstance, BuildLevel.All, args: ["--no-cache"]);

        Build(testInstance, BuildLevel.None);

        // Pass argument (no rebuild necessary).
        Build(testInstance, BuildLevel.None, args: ["--", "test-arg"], expectedOutput: """
            echo args:test-arg
            Hello from Program
            """);

        // Change config (a rebuild is necessary).
        Build(testInstance, BuildLevel.All, args: ["-c", "Release"], expectedOutput: """
            Hello from Program
            Release config
            """);

        // Keep changed config (no rebuild necessary).
        Build(testInstance, BuildLevel.None, args: ["-c", "Release"], expectedOutput: """
            Hello from Program
            Release config
            """);

        // Change config back (a rebuild is necessary).
        Build(testInstance, BuildLevel.Csc);

        // Build with a failure.
        new DotnetCommand(Log, ["run", "Program.cs", "-p:LangVersion=Invalid"])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS1617"); // Invalid option 'Invalid' for /langversion.

        // A rebuild is necessary since the last build failed.
        Build(testInstance, BuildLevel.Csc);
    }

    private void Build(TestDirectory testInstance, BuildLevel level, ReadOnlySpan<string> args = default, string expectedOutput = "Hello from Program")
    {
        string prefix = level switch
        {
            BuildLevel.None => CliCommandStrings.NoBinaryLogBecauseUpToDate + Environment.NewLine,
            BuildLevel.Csc => CliCommandStrings.NoBinaryLogBecauseRunningJustCsc + Environment.NewLine,
            BuildLevel.All => string.Empty,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(level)),
        };

        new DotnetCommand(Log, ["run", "Program.cs", "-bl", .. args])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(prefix + expectedOutput);

        var binlogs = new DirectoryInfo(testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly);

        binlogs.Select(f => f.Name)
            .Should().BeEquivalentTo(
                level switch
                {
                    BuildLevel.None or BuildLevel.Csc => [],
                    BuildLevel.All => ["msbuild.binlog"],
                    _ => throw new ArgumentOutOfRangeException(paramName: nameof(level), message: level.ToString()),
                });

        foreach (var binlog in binlogs)
        {
            binlog.Delete();
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
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.CannotCombineOptions, RunCommandParser.NoCacheOption.Name, RunCommandParser.NoBuildOption.Name));
    }

    [Fact]
    public void CscOnly()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v1");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v2");
            #if !DEBUG
            Console.WriteLine("Release config");
            #endif
            """);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2");

        // Customizing a property forces MSBuild to be used.
        Build(testInstance, BuildLevel.All, args: ["-c", "Release"], expectedOutput: """
            v2
            Release config
            """);
    }

    [Fact]
    public void CscOnly_CompilationDiagnostics()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            string x = null;
            Console.WriteLine("ran" + x);
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            // warning CS8600: Converting null literal or possible null value to non-nullable type.
            .And.HaveStdOutContaining("warning CS8600")
            .And.HaveStdOutContaining("ran");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.Write
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            // error CS1002: ; expected
            .And.HaveStdOutContaining("error CS1002")
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);
    }

    /// <summary>
    /// Checks that the <c>DOTNET_ROOT</c> env var is set the same in csc mode as in msbuild mode.
    /// </summary>
    [Fact]
    public void CscOnly_DotNetRoot()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            foreach (var entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
                .Cast<System.Collections.DictionaryEntry>()
                .Where(e => ((string)e.Key).StartsWith("DOTNET_ROOT")))
            {
                Console.WriteLine($"{entry.Key}={entry.Value}");
            }
            """);

        var expectedDotNetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;

        var cscResult = new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        cscResult.Should().Pass()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            .And.HaveStdOutContaining("DOTNET_ROOT")
            .And.HaveStdOutContaining($"={expectedDotNetRoot}");

        // Add an implicit build file to force use of msbuild instead of csc.
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), "<Project />");

        var msbuildResult = new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        msbuildResult.Should().Pass()
            .And.NotHaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            .And.HaveStdOutContaining("DOTNET_ROOT")
            .And.HaveStdOutContaining($"={expectedDotNetRoot}");

        // The set of DOTNET_ROOT env vars should be the same in both cases.
        var cscVars = cscResult.StdOut!
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("DOTNET_ROOT"));
        var msbuildVars = msbuildResult.StdOut!
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith("DOTNET_ROOT"));
        cscVars.Should().BeEquivalentTo(msbuildVars);
    }

    /// <summary>
    /// In CSC-only mode, the SDK needs to manually create intermediate files
    /// like GlobalUsings.g.cs which are normally generated by MSBuild targets.
    /// This tests the SDK recreates the files when they are outdated.
    /// </summary>
    [Fact]
    public void CscOnly_IntermediateFiles()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Expression<Func<int>> e = () => 1 + 1;
            Console.WriteLine(e);
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), "<Project />");

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS0246: The type or namespace name 'Expression<>' could not be found
            .And.HaveStdOutContaining("error CS0246");

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <ItemGroup>
                    <Using Include="System.Linq.Expressions" />
                </ItemGroup>
            </Project>
            """);

        Build(testInstance, BuildLevel.All, expectedOutput: "() => 2");

        File.Delete(Path.Join(testInstance.Path, "Directory.Build.props"));

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            // error CS0246: The type or namespace name 'Expression<>' could not be found
            .And.HaveStdOutContaining("error CS0246");
    }

    /// <summary>
    /// If a file from a NuGet package (which would be used by CSC-only build) does not exist, full MSBuild should be used instead.
    /// </summary>
    [Fact]
    public void CscOnly_NotRestored()
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        new DotnetCommand(Log, "run", "Program.cs", "-bl", "--no-restore")
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error NETSDK1004: Assets file '...\obj\project.assets.json' not found. Run a NuGet package restore to generate this file.
            .And.HaveStdOutContaining("NETSDK1004");

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v2");
            """);

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithEnvironmentVariable("NUGET_PACKAGES", Path.Join(testInstance.Path, "packages"))
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {CliCommandStrings.NoBinaryLogBecauseRunningJustCsc}
                v2
                """);
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
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                      <Import Project="Sdk.props" Sdk="Aspire.Hosting.Sdk" Version="9.1.0" />

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <TargetFramework>net11.0</TargetFramework>
                        <LangVersion>preview</LangVersion>
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
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
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
                        <PackageOutputPath>artifacts/$(MSBuildProjectName)</PackageOutputPath>
                        <FileBasedProgram>true</FileBasedProgram>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
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
            // The command outputs only _custom_ environment variables (not inherited ones),
            // so make sure we don't pass DOTNET_ROOT_* so we can assert that it is set by the run command.
            .WithEnvironmentVariable("DOTNET_ROOT", string.Empty)
            .WithEnvironmentVariable($"DOTNET_ROOT_{RuntimeInformation.OSArchitecture.ToString().ToUpperInvariant()}", string.Empty)
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

    [Theory, CombinatorialData]
    public void EntryPointFilePath(bool cscOnly)
    {
        var testInstance = _testAssetsManager.CreateTestDirectory(baseDirectory: cscOnly ? OutOfTreeBaseDirectory : null);
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, """"
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"""EntryPointFilePath: {entryPointFilePath}""");
            """");

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuildingCommand.GetArtifactsPath(filePath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var prefix = cscOnly
            ? CliCommandStrings.NoBinaryLogBecauseRunningJustCsc + Environment.NewLine
            : string.Empty;

        new DotnetCommand(Log, "run", "-bl", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(prefix + $"EntryPointFilePath: {filePath}");
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
