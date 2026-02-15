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
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

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
        var sourceNuGetConfig = Path.Join(SdkTestContext.Current.TestExecutionDirectory, "NuGet.config");
        var targetNuGetConfig = Path.Join(outOfTreeBaseDirectory, "NuGet.config");
        File.Copy(sourceNuGetConfig, targetNuGetConfig, overwrite: true);

        // Check there are no implicit build files that would prevent testing optimizations.
        VirtualProjectBuildingCommand.CollectImplicitBuildFiles(new DirectoryInfo(outOfTreeBaseDirectory), [], out var exampleMSBuildFile);
        exampleMSBuildFile.Should().BeNull(because: "there should not be any implicit build files in the temp directory or its parents " +
            "so we can test optimizations that would be disabled with implicit build files present");

        return outOfTreeBaseDirectory;
    }

    internal static string DirectiveError(string path, int line, string messageFormat, params ReadOnlySpan<object> args)
    {
        return $"{path}({line}): {FileBasedProgramsResources.DirectiveError}: {string.Format(messageFormat, args)}";
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

        new DotnetNewCommand(Log, "tool-manifest")
            .WithVirtualHive()
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

    /// <summary>
    /// Main method is supported just like top-level statements.
    /// </summary>
    [Fact]
    public void MainMethod()
    {
        var testInstance = TestAssetsManager.CopyTestAsset("MSBuildTestApp").WithSource();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), string.Empty);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining("error CS5001:"); // Program does not contain a static 'Main' method suitable for an entry point
    }

    /// <summary>
    /// See <see href="https://github.com/dotnet/sdk/issues/51778"/>.
    /// </summary>
    [Theory, CombinatorialData]
    public void WorkingDirectory(bool cscOnly)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: cscOnly ? OutOfTreeBaseDirectory : null);
        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = """
            Console.WriteLine("v1");
            Console.WriteLine(Environment.CurrentDirectory);
            Console.WriteLine(Directory.GetCurrentDirectory());
            Console.WriteLine(new DirectoryInfo(".").FullName);
            Console.WriteLine(AppContext.GetData("EntryPointFileDirectoryPath"));
            """;

        File.WriteAllText(programPath, code);

        var workDir = TestPathUtility.ResolveTempPrefixLink(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance,
            expectedLevel: cscOnly ? BuildLevel.Csc : BuildLevel.All,
            programFileName: programPath,
            workDir: workDir,
            expectedOutput: GetExpectedOutput("v1", workDir));

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance,
            expectedLevel: BuildLevel.Csc,
            programFileName: programPath,
            workDir: workDir,
            expectedOutput: GetExpectedOutput("v2", workDir));

        string GetExpectedOutput(string version, string workDir) => $"""
            {version}
            {workDir}
            {workDir}
            {workDir}
            {Path.GetDirectoryName(programPath)}
            """;
    }

    /// <summary>
    /// Combination of <see cref="WorkingDirectory"/> and <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void WorkingDirectory_CscOnly_AfterMSBuild()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = """
            #:property Configuration=Release
            Console.WriteLine("v1");
            Console.WriteLine(Environment.CurrentDirectory);
            Console.WriteLine(Directory.GetCurrentDirectory());
            Console.WriteLine(new DirectoryInfo(".").FullName);
            Console.WriteLine(AppContext.GetData("EntryPointFileDirectoryPath"));
            """;

        File.WriteAllText(programPath, code);

        var workDir = TestPathUtility.ResolveTempPrefixLink(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance,
            expectedLevel: BuildLevel.All,
            programFileName: programPath,
            workDir: workDir,
            expectedOutput: GetExpectedOutput("v1", workDir));

        Build(testInstance,
            expectedLevel: BuildLevel.None,
            programFileName: programPath,
            workDir: workDir,
            expectedOutput: GetExpectedOutput("v1", workDir));

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance,
            expectedLevel: BuildLevel.Csc,
            programFileName: programPath,
            workDir: workDir,
            expectedOutput: GetExpectedOutput("v2", workDir));

        // Can be overridden with a #:property.
        var workDir2 = Path.Join(testInstance.Path, "dir2");
        Directory.CreateDirectory(workDir2);
        code = $"""
            #:property RunWorkingDirectory={workDir2}
            {code}
            """;
        File.WriteAllText(programPath, code);

        Build(testInstance,
            expectedLevel: BuildLevel.All,
            programFileName: programPath,
            workDir: workDir,
            expectedOutput: GetExpectedOutput("v2", workDir2));

        string GetExpectedOutput(string version, string workDir) => $"""
            {version}
            {workDir}
            {workDir}
            {workDir}
            {Path.GetDirectoryName(programPath)}
            """;
    }

    /// <summary>
    /// Implicit build files have an effect.
    /// </summary>
    [Fact]
    public void DirectoryBuildProps()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
    /// Implicit build files are taken from the folder of the symbolic link itself, not its target.
    /// This is equivalent to the behavior of symlinked project files.
    /// See <see href="https://github.com/dotnet/sdk/pull/52064#issuecomment-3628958688"/>.
    /// </summary>
    [Fact]
    public void DirectoryBuildProps_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var dir1 = Path.Join(testInstance.Path, "dir1");
        Directory.CreateDirectory(dir1);

        var originalPath = Path.Join(dir1, "original.cs");
        File.WriteAllText(originalPath, s_program);

        File.WriteAllText(Path.Join(dir1, "Directory.Build.props"), """
            <Project>
                <PropertyGroup>
                    <AssemblyName>OriginalAssemblyName</AssemblyName>
                </PropertyGroup>
            </Project>
            """);

        var dir2 = Path.Join(testInstance.Path, "dir2");
        Directory.CreateDirectory(dir2);

        var programFileName = "linked.cs";
        var programPath = Path.Join(dir2, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        File.WriteAllText(Path.Join(dir2, "Directory.Build.props"), """
            <Project>
                <PropertyGroup>
                    <AssemblyName>LinkedAssemblyName</AssemblyName>
                </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "run", programFileName)
            .WithWorkingDirectory(dir2)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from LinkedAssemblyName");

        // Removing the Directory.Build.props should be detected by up-to-date check.
        File.Delete(Path.Join(dir2, "Directory.Build.props"));

        new DotnetCommand(Log, "run", programFileName)
            .WithWorkingDirectory(dir2)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from linked");
    }

    /// <summary>
    /// Overriding default (implicit) properties of file-based apps via implicit build files.
    /// </summary>
    [Fact]
    public void DefaultProps_DirectoryBuildProps()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("Hi");
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ImplicitUsings>disable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Console' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");

        // Converting to a project should not change the behavior.

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithWorkingDirectory(Path.Join(testInstance.Path, "Program"))
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Console' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");
    }

    /// <summary>
    /// Overriding default (implicit) properties of file-based apps from custom SDKs.
    /// </summary>
    [Fact]
    public void DefaultProps_CustomSdk()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var sdkDir = Path.Join(testInstance.Path, "MySdk");
        Directory.CreateDirectory(sdkDir);
        File.WriteAllText(Path.Join(sdkDir, "Sdk.props"), """
            <Project>
              <PropertyGroup>
                <ImplicitUsings>disable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Join(sdkDir, "Sdk.targets"), """
            <Project />
            """);
        File.WriteAllText(Path.Join(sdkDir, "MySdk.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                <PackageType>MSBuildSdk</PackageType>
                <IncludeBuildOutput>false</IncludeBuildOutput>
              </PropertyGroup>
              <ItemGroup>
                <None Include="Sdk.*" Pack="true" PackagePath="Sdk" />
              </ItemGroup>
            </Project>
            """);

        new DotnetCommand(Log, "pack")
            .WithWorkingDirectory(sdkDir)
            .Execute()
            .Should().Pass();

        var appDir = Path.Join(testInstance.Path, "app");
        Directory.CreateDirectory(appDir);
        File.WriteAllText(Path.Join(appDir, "NuGet.config"), $"""
            <configuration>
              <packageSources>
                <add key="local" value="{Path.Join(sdkDir, "bin", "Release")}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);
        File.WriteAllText(Path.Join(appDir, "Program.cs"), """
            #:sdk Microsoft.NET.Sdk
            #:sdk MySdk@1.0.0
            Console.WriteLine("Hi");
            """);

        // Use custom package cache to avoid reuse of the custom SDK packed by previous test runs.
        var packagesDir = Path.Join(testInstance.Path, ".packages");

        new DotnetCommand(Log, "run", "Program.cs")
            .WithEnvironmentVariable("NUGET_PACKAGES", packagesDir)
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Console' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");

        // Converting to a project should not change the behavior.

        new DotnetCommand(Log, "project", "convert", "Program.cs")
            .WithEnvironmentVariable("NUGET_PACKAGES", packagesDir)
            .WithWorkingDirectory(appDir)
            .Execute()
            .Should().Pass();

        new DotnetCommand(Log, "run")
            .WithEnvironmentVariable("NUGET_PACKAGES", packagesDir)
            .WithWorkingDirectory(Path.Join(appDir, "Program"))
            .Execute()
            .Should().Fail()
            // error CS0103: The name 'Console' does not exist in the current context
            .And.HaveStdOutContaining("error CS0103");
    }

    [Fact]
    public void ComputeRunArguments_Success()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, ["run", "--arg", "Program.cs"])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(
                CliCommandStrings.RunCommandExceptionNoProjects,
                testInstance.Path,
                "--project"));
    }

    /// <summary>
    /// <c>dotnet run --some-known-arg file.cs</c> is supported.
    /// </summary>
    [Theory, CombinatorialData]
    public void Arguments_Recognized(bool beforeFile)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        string[] args = beforeFile
            ? ["-bl", "Program.cs"]
            : ["Program.cs", "-bl"];

        new DotnetCommand(Log, ["run", "--no-cache", .. args])
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", arg)
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "-bl:one.binlog", "two.binlog", "/bl:three.binlog")
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "-bl")
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();

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
    /// File-based projects using the default SDK do not include embedded resources by default.
    /// </summary>
    [Fact]
    public void EmbeddedResource()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_programReadingEmbeddedResource);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);

        // By default, with the default SDK, embedded resources are not included.
        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                Resource not found
                """);

        // This behavior can be overridden to enable embedded resources.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:property EnableDefaultEmbeddedResourceItems=true
            {s_programReadingEmbeddedResource}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                [MyString, TestValue]
                """);

        // When using a non-default SDK, embedded resources are included by default.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:sdk Microsoft.NET.Sdk.Web
            {s_programReadingEmbeddedResource}
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                [MyString, TestValue]
                """);

        // When using the default SDK explicitly, embedded resources are not included.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:sdk Microsoft.NET.Sdk
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

        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:property EnableDefaultEmbeddedResourceItems=true
            {s_programReadingEmbeddedResource}
            """);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);
        File.WriteAllText(Path.Join(testInstance.Path, $"repo.{ext}"), "");

        // Up-to-date check currently doesn't support default items, so we need to pass --no-cache
        // otherwise other runs of this test theory might cause outdated results.
        new DotnetCommand(Log, "run", "--no-cache", "--file", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(considered ? "Resource not found" : "[MyString, TestValue]");
    }

    [Fact]
    public void NoRestore_01()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
                Path.ChangeExtension(programFile, ".csproj"),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "Library"));
    }

    [Fact]
    public void Build_Library_MultiTarget()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "lib.cs");
        File.WriteAllText(programFile, $"""
            #:property OutputType=Library
            #:property PublishAot=false
            #:property LangVersion=preview
            #:property TargetFrameworks=netstandard2.0;{ToolsetInfo.CurrentTargetFramework}
            class C;
            """);

        // https://github.com/dotnet/sdk/issues/51077: cannot set this via `#:property` directive.
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <TargetFramework></TargetFramework>
              </PropertyGroup>
            </Project>
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
                Path.ChangeExtension(programFile, ".csproj"),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "Library"));
    }

    [Fact]
    public void Build_Module()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
                Path.ChangeExtension(programFile, ".csproj"),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "Module"));
    }

    [Fact]
    public void Build_WinExe()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var programFile = Path.Join(testInstance.Path, "exe.cs");
        File.WriteAllText(programFile, $"""
            #:property OutputType=Exe
            #:property PublishAot=false
            #:property LangVersion=preview
            #:property TargetFrameworks=netstandard2.0;{ToolsetInfo.CurrentTargetFramework}
            Console.WriteLine("Hello Exe");
            """);

        // https://github.com/dotnet/sdk/issues/51077: cannot set this via `#:property` directive.
        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <TargetFramework></TargetFramework>
              </PropertyGroup>
            </Project>
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
                Path.ChangeExtension(programFile, ".csproj"),
                ToolsetInfo.CurrentTargetFrameworkVersion,
                "AppContainerExe"));
    }

    [Fact]
    public void Publish()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("Hello; EntryPointFilePath set? False");
    }

    [Fact]
    public void Clean()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: cscOnly ? OutOfTreeBaseDirectory : null);
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var binaryLogPath = Path.Join(testInstance.Path, "msbuild.binlog");
        var msbuildCall = FindCompilerCall(binaryLogPath);
        var msbuildCallArgs = msbuildCall.GetArguments();
        var msbuildCallArgsString = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(msbuildCallArgs);

        // Generate argument template code.
        string sdkPath = NormalizePath(SdkTestContext.Current.ToolsetUnderTest.SdkFolderUnderTest);
        string dotNetRootPath = NormalizePath(SdkTestContext.Current.ToolsetUnderTest.DotNetRoot);
        string nuGetCachePath = NormalizePath(SdkTestContext.Current.NuGetCachePath!);
        string artifactsDirNormalized = NormalizePath(artifactsDir);
        string objPath = $"{artifactsDirNormalized}/obj/debug";
        string entryPointPathNormalized = NormalizePath(entryPointPath);
        string runtimeVersion = FindRuntimeVersion(binaryLogPath);
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
            if (rewritten.Contains(runtimeVersion, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = rewritten.Replace(runtimeVersion, "{" + nameof(CSharpCompilerCommand.RuntimeVersion) + "}", StringComparison.OrdinalIgnoreCase);
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

        static string FindRuntimeVersion(string binaryLogPath)
        {
            var records = BinaryLog.ReadRecords(binaryLogPath).ToList();
            foreach (var r in records)
            {
                if (r.Args is ProjectEvaluationFinishedEventArgs args)
                {
                    Assert.NotNull(args.Properties);
                    foreach (KeyValuePair<string, string> entry in args.Properties)
                    {
                        if (entry.Key == "PkgMicrosoft_NET_ILLink_Tasks")
                        {
                            return Path.GetFileName(entry.Value);
                        }
                    }
                }
            }

            Assert.Fail();
            return null;
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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("Hello v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
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

    private void Build(
        TestDirectory testInstance,
        BuildLevel expectedLevel,
        ReadOnlySpan<string> args = default,
        string expectedOutput = "Hello from Program",
        string programFileName = "Program.cs",
        string? workDir = null,
        Func<TestCommand, TestCommand>? customizeCommand = null)
    {
        string prefix = expectedLevel switch
        {
            BuildLevel.None => CliCommandStrings.NoBinaryLogBecauseUpToDate + Environment.NewLine,
            BuildLevel.Csc => CliCommandStrings.NoBinaryLogBecauseRunningJustCsc + Environment.NewLine,
            BuildLevel.All => string.Empty,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(expectedLevel)),
        };

        var command = new DotnetCommand(Log, ["run", programFileName, "-bl", .. args])
            .WithWorkingDirectory(workDir ?? testInstance.Path);

        if (customizeCommand != null)
        {
            command = customizeCommand(command);
        }

        command.Execute()
            .Should().Pass()
            .And.HaveStdOut(prefix + expectedOutput);

        var binlogs = new DirectoryInfo(workDir ?? testInstance.Path)
            .EnumerateFiles("*.binlog", SearchOption.TopDirectoryOnly);

        binlogs.Select(f => f.Name)
            .Should().BeEquivalentTo(
                expectedLevel switch
                {
                    BuildLevel.None or BuildLevel.Csc => [],
                    BuildLevel.All => ["msbuild.binlog"],
                    _ => throw new ArgumentOutOfRangeException(paramName: nameof(expectedLevel), message: expectedLevel.ToString()),
                });

        foreach (var binlog in binlogs)
        {
            binlog.Delete();
        }
    }

    [Fact]
    public void UpToDate_InvalidOptions()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "Program.cs", "--no-cache", "--no-build")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdErrContaining(string.Format(CliCommandStrings.CannotCombineOptions, "--no-cache", "--no-build"));
    }

    /// <summary>
    /// <see cref="UpToDate"/> optimization should see through symlinks.
    /// See <see href="https://github.com/dotnet/sdk/issues/52063"/>.
    /// </summary>
    [Fact]
    public void UpToDate_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var programFileName = "linked";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", programFileName: programFileName);

        Build(testInstance, BuildLevel.None, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// Similar to <see cref="UpToDate_SymbolicLink"/> but with a chain of symlinks.
    /// </summary>
    [Fact]
    public void UpToDate_SymbolicLink2()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var intermediateFileName = "linked1";
        var intermediatePath = Path.Join(testInstance.Path, intermediateFileName);

        File.CreateSymbolicLink(path: intermediatePath, pathToTarget: originalPath);

        var programFileName = "linked2";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: intermediatePath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", programFileName: programFileName);

        Build(testInstance, BuildLevel.None, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// <see cref="UpToDate"/> optimization currently does not support <c>#:project</c> references and hence is disabled if those are present.
    /// See <see href="https://github.com/dotnet/sdk/issues/52057"/>.
    /// </summary>
    [Fact]
    public void UpToDate_ProjectReferences()
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

        var libPath = Path.Join(libDir, "Lib.cs");
        var libCode = """
            namespace Lib;
            public class LibClass
            {
                public static string GetMessage() => "Hello from Lib v1";
            }
            """;
        File.WriteAllText(libPath, libCode);

        var appDir = Path.Join(testInstance.Path, "App");
        Directory.CreateDirectory(appDir);

        var code = """
            #:project ../Lib
            Console.WriteLine("v1 " + Lib.LibClass.GetMessage());
            """;

        var programPath = Path.Join(appDir, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var programFileName = "App/Program.cs";

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v1", programFileName: programFileName);

        // We cannot detect changes in referenced projects, so we always rebuild.
        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v1", programFileName: programFileName);

        libCode = libCode.Replace("v1", "v2");
        File.WriteAllText(libPath, libCode);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v2", programFileName: programFileName);
    }

    /// <summary>
    /// Up-to-date checks and optimizations currently don't support other included files.
    /// </summary>
    [Theory, CombinatorialData] // https://github.com/dotnet/sdk/issues/50912
    public void UpToDate_DefaultItems(bool optOut)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var code = $"""
            {(optOut ? "#:property FileBasedProgramCanSkipMSBuild=false" : "")}
            #:property EnableDefaultEmbeddedResourceItems=true
            {s_programReadingEmbeddedResource}
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), code);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);

        Build(testInstance, BuildLevel.All, expectedOutput: "[MyString, TestValue]");

        // Update the RESX file.
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx.Replace("TestValue", "UpdatedValue"));

        Build(testInstance, optOut ? BuildLevel.All : BuildLevel.None, expectedOutput: optOut ? "[MyString, UpdatedValue]" : "[MyString, TestValue]"); // note: outdated output (build skipped)

        // Update the C# file.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), "//v2\n" + code);

        Build(testInstance, optOut ? BuildLevel.All : BuildLevel.Csc, expectedOutput: optOut ? "[MyString, UpdatedValue]" : "[MyString, TestValue]"); // note: outdated output (only CSC used)

        Build(testInstance, BuildLevel.All, ["--no-cache"], expectedOutput: "[MyString, UpdatedValue]");
    }

    /// <summary>
    /// Combination of <see cref="UpToDate_DefaultItems"/> with <see cref="CscOnly_AfterMSBuild"/> optimization.
    /// </summary>
    /// <remarks>
    /// Note: we cannot test <see cref="CscOnly"/> because that optimization doesn't support neither <c>#:property</c> nor <c>#:sdk</c> which we need to enable default items.
    /// </remarks>
    [Theory, CombinatorialData] // https://github.com/dotnet/sdk/issues/50912
    public void UpToDate_DefaultItems_CscOnly_AfterMSBuild(bool optOut)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var code = $"""
            #:property Configuration=Release
            {(optOut ? "#:property FileBasedProgramCanSkipMSBuild=false" : "")}
            #:property EnableDefaultEmbeddedResourceItems=true
            {s_programReadingEmbeddedResource}
            """;
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), code);
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx);

        Build(testInstance, BuildLevel.All, expectedOutput: "[MyString, TestValue]");

        // Update the C# file.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), "//v2\n" + code);

        Build(testInstance, optOut ? BuildLevel.All : BuildLevel.Csc, expectedOutput: optOut ? "[MyString, TestValue]" : "[MyString, TestValue]");

        // Update the RESX file.
        File.WriteAllText(Path.Join(testInstance.Path, "Resources.resx"), s_resx.Replace("TestValue", "UpdatedValue"));

        Build(testInstance, optOut ? BuildLevel.All : BuildLevel.None, expectedOutput: optOut ? "[MyString, UpdatedValue]" : "[MyString, TestValue]");

        // Update the C# file.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), "//v3\n" + code);

        Build(testInstance, optOut ? BuildLevel.All : BuildLevel.Csc, expectedOutput: optOut ? "[MyString, UpdatedValue]" : "[MyString, TestValue]");

        Build(testInstance, BuildLevel.All, ["--no-cache"], expectedOutput: "[MyString, UpdatedValue]");
    }

    [Fact]
    public void CscOnly()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            foreach (var entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process)
                .Cast<System.Collections.DictionaryEntry>()
                .Where(e => ((string)e.Key).StartsWith("DOTNET_ROOT")))
            {
                Console.WriteLine($"{entry.Key}={entry.Value}");
            }
            """);

        var expectedDotNetRoot = SdkTestContext.Current.ToolsetUnderTest.DotNetRoot;

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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Expression<Func<int>> e = () => 1 + 1;
            Console.WriteLine(e);
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(Path.Join(testInstance.Path, "Program.cs"));
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

    [Fact]
    public void CscOnly_SpacesInPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var programFileName = "Program with spaces.cs";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.WriteAllText(programPath, """
            Console.WriteLine("v1");
            """);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v1", programFileName: programFileName);
    }

    [Fact] // https://github.com/dotnet/sdk/issues/50778
    public void CscOnly_Args()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, s_program);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, args: ["test", "args"], expectedOutput: """
            echo args:test;args
            Hello from Program
            """);
    }

    /// <summary>
    /// Combination of <see cref="UpToDate_SymbolicLink"/> and <see cref="CscOnly"/>.
    /// </summary>
    [Fact]
    public void CscOnly_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var programFileName = "linked";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v1", programFileName: programFileName);

        Build(testInstance, BuildLevel.None, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// Tests an optimization which remembers CSC args from prior MSBuild runs and can skip subsequent MSBuild invocations and call CSC directly.
    /// This optimization kicks in when the file has some <c>#:</c> directives (then the simpler "hard-coded CSC args" optimization cannot be used).
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property Configuration=Release
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release");

        Build(testInstance, BuildLevel.None, expectedOutput: "v1 Release");

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2 Release");

        code = code.Replace("v2", "v3");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v3 Release");

        // Customizing a property forces MSBuild to be used.
        code = code.Replace("Configuration=Release", "Configuration=Debug");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.All, expectedOutput: "v3 ");

        // This MSBuild will skip CoreBuild but we still need to preserve CSC args so the next build can be CSC-only.
        Build(testInstance, BuildLevel.All, ["--no-cache"], expectedOutput: "v3 ");

        code = code.Replace("v3", "v4");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v4 ");

        // Customizing a property on the command-line forces MSBuild to be used.
        Build(testInstance, BuildLevel.All, args: ["-c", "Release"], expectedOutput: "v4 Release");

        Build(testInstance, BuildLevel.All, expectedOutput: "v4 ");
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_SpacesInPath()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property Configuration=Release
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programFileName = "Program with spaces.cs";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2 Release", programFileName: programFileName);
    }

    /// <summary>
    /// Testing optimization <see cref="CscOnly_AfterMSBuild"/>.
    /// When compilation fails, the obj dll should not be copied to bin directory.
    /// This prevents spurious errors if the dll file was not even produced by roslyn due to compilation errors.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_CompilationFailure_NoCopyToBin()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        // First, create a valid program and build it successfully
        var programPath = Path.Join(testInstance.Path, "Program.cs");
        var code = """
            #:property PublishAot=false
            Console.WriteLine("version 1");
            """;
        File.WriteAllText(programPath, code);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "version 1");

        // Verify that the dlls were created
        var objDll = Path.Join(artifactsDir, "obj", "debug", "Program.dll");
        new FileInfo(objDll).Should().Exist();
        var binDll = Path.Join(artifactsDir, "bin", "debug", "Program.dll");
        new FileInfo(binDll).Should().Exist();

        // Delete the dlls
        File.Delete(objDll);
        File.Delete(binDll);

        // Write invalid code that causes compilation to fail
        code = code + "\n#error my custom error";
        File.WriteAllText(programPath, code);

        // Try to build the invalid code
        new DotnetCommand(Log, "run", "-bl", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Fail()
            .And.HaveStdOutContaining(CliCommandStrings.NoBinaryLogBecauseRunningJustCsc)
            .And.HaveStdOutContaining("my custom error")
            .And.HaveStdErrContaining(CliCommandStrings.RunCommandException);

        new FileInfo(objDll).Should().NotExist();
        new FileInfo(binDll).Should().NotExist();
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_Args()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = $"""
            #:property Configuration=Release
            {s_program}
            """;

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, args: ["test", "args"], expectedOutput: """
            echo args:test;args
            Hello from Program
            Release config
            """);

        code = code.Replace("Hello", "Hi");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, ["test", "args"], expectedOutput: """
            echo args:test;args
            Hi from Program
            Release config
            """);
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// If hard links are enabled, the <c>bin/app.dll</c> and <c>obj/app.dll</c> files are going to be the same,
    /// so our "copy obj to bin" logic must account for that.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_HardLinks()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);
        var programPath = Path.Join(testInstance.Path, "Program.cs");

        var code = $"""
            #:property CreateHardLinksForCopyFilesToOutputDirectoryIfPossible=true
            #:property CreateSymbolicLinksForCopyFilesToOutputDirectoryIfPossible=true
            {s_program}
            """;

        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All);

        code = code.Replace("Hello", "Hi");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "Hi from Program");
    }

    /// <summary>
    /// Combination of <see cref="UpToDate_SymbolicLink"/> and <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var originalPath = Path.Join(testInstance.Path, "original.cs");
        var code = """
            #!/usr/bin/env dotnet
            #:property Configuration=Release
            Console.WriteLine("v1");
            """;
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(originalPath, code, utf8NoBom);

        var programFileName = "linked";
        var programPath = Path.Join(testInstance.Path, programFileName);

        File.CreateSymbolicLink(path: programPath, pathToTarget: originalPath);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(originalPath, code, utf8NoBom);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", programFileName: programFileName);
    }

    /// <summary>
    /// Interaction of optimization <see cref="CscOnly_AfterMSBuild"/> and <c>Directory.Build.props</c> file.
    /// </summary>
    [Theory, CombinatorialData]
    public void CscOnly_AfterMSBuild_DirectoryBuildProps(bool touch1, bool touch2)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var propsPath = Path.Join(testInstance.Path, "Directory.Build.props");
        var propsContent = """
            <Project>
                <PropertyGroup>
                    <AssemblyName>CustomAssemblyName</AssemblyName>
                </PropertyGroup>
            </Project>
            """;
        File.WriteAllText(propsPath, propsContent);

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        var programVersion = 0;
        void WriteProgramContent()
        {
            programVersion++;

            // #: directive ensures we get CscOnly_AfterMSBuild optimization instead of CscOnly.
            File.WriteAllText(programPath, $"""
                #:property Configuration=Debug
                Console.WriteLine("v{programVersion} " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                """);
        }
        WriteProgramContent();

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: $"v{programVersion} CustomAssemblyName");

        File.Delete(propsPath);

        if (touch1) WriteProgramContent();

        Build(testInstance, BuildLevel.All, expectedOutput: $"v{programVersion} Program");

        File.WriteAllText(propsPath, propsContent);

        if (touch2) WriteProgramContent();

        Build(testInstance, BuildLevel.All, expectedOutput: $"v{programVersion} CustomAssemblyName");
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// This optimization currently does not support <c>#:project</c> references and hence is disabled if those are present.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_ProjectReferences()
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

        var libPath = Path.Join(libDir, "Lib.cs");
        var libCode = """
            namespace Lib;
            public class LibClass
            {
                public static string GetMessage() => "Hello from Lib v1";
            }
            """;
        File.WriteAllText(libPath, libCode);

        var appDir = Path.Join(testInstance.Path, "App");
        Directory.CreateDirectory(appDir);

        var code = """
            #:project ../Lib
            Console.WriteLine("v1 " + Lib.LibClass.GetMessage());
            """;

        var programPath = Path.Join(appDir, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var programFileName = "App/Program.cs";

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Hello from Lib v1", programFileName: programFileName);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        libCode = libCode.Replace("v1", "v2");
        File.WriteAllText(libPath, libCode);

        // Cannot use CSC because we cannot detect updates in the referenced project.
        Build(testInstance, BuildLevel.All, expectedOutput: "v2 Hello from Lib v2", programFileName: programFileName);
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// If users have more complex build customizations, they can opt out of the optimization.
    /// </summary>
    [Theory, CombinatorialData]
    public void CscOnly_AfterMSBuild_OptOut(bool canSkipMSBuild, bool inDirectoryBuildProps)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        const string propertyName = VirtualProjectBuildingCommand.FileBasedProgramCanSkipMSBuild;

        if (inDirectoryBuildProps)
        {
            File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), $"""
                <Project>
                  <PropertyGroup>
                    <{propertyName}>{canSkipMSBuild}</{propertyName}>
                  </PropertyGroup>
                </Project>
                """);
        }

        var code = $"""
            #:property Configuration=Release
            {(inDirectoryBuildProps ? "" : $"#:property {propertyName}={canSkipMSBuild}")}
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release");

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, canSkipMSBuild ? BuildLevel.Csc : BuildLevel.All, expectedOutput: "v2 Release");
    }

    /// <summary>
    /// See <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_AuxiliaryFilesNotReused()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property Configuration=Release
            Console.Write("v1 ");
            #if !DEBUG
            Console.Write("Release");
            #endif
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        Build(testInstance, BuildLevel.All, expectedOutput: "v1 Release");

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        // Reusing CSC args from previous run here.
        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2 Release");

        code = code.Replace("v2", "v3");
        code = code.Replace("#:property Configuration=Release", "");
        File.WriteAllText(programPath, code);

        // Using built-in CSC args here (cannot reuse auxiliary files like csc.rsp here).
        Build(testInstance, BuildLevel.Csc, expectedOutput: "v3 ");
    }

    /// <summary>
    /// Testing <see cref="CscOnly"/> optimization when the NuGet cache is cleared between builds.
    /// See <see href="https://github.com/dotnet/sdk/issues/45169"/>.
    /// </summary>
    [Fact]
    public void CscOnly_NuGetCacheCleared()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            Console.Write("v1");
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var packageDir = Path.Join(testInstance.Path, "packages");
        TestCommand CustomizeCommand(TestCommand command) => command.WithEnvironmentVariable("NUGET_PACKAGES", packageDir);

        Assert.False(Directory.Exists(packageDir));

        // Ensure the packages exist first.
        Build(testInstance, BuildLevel.All, expectedOutput: "v1", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));

        // Now clear the build outputs (but not packages) to verify CSC is used even from "first run".
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", customizeCommand: CustomizeCommand);

        code = code.Replace("v2", "v3");
        File.WriteAllText(programPath, code);

        // Clear NuGet cache.
        Directory.Delete(packageDir, recursive: true);
        Assert.False(Directory.Exists(packageDir));

        Build(testInstance, BuildLevel.All, expectedOutput: "v3", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));
    }

    /// <summary>
    /// Combination of <see cref="CscOnly_NuGetCacheCleared"/> and <see cref="CscOnly_AfterMSBuild"/>.
    /// </summary>
    [Fact]
    public void CscOnly_AfterMSBuild_NuGetCacheCleared()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: OutOfTreeBaseDirectory);

        var code = """
            #:property PublishAot=false
            #:package System.CommandLine@2.0.0-beta4.22272.1
            new System.CommandLine.RootCommand("v1");
            Console.WriteLine("v1");
            """;

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, code);

        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(programPath);
        if (Directory.Exists(artifactsDir)) Directory.Delete(artifactsDir, recursive: true);

        var packageDir = Path.Join(testInstance.Path, "packages");
        TestCommand CustomizeCommand(TestCommand command) => command.WithEnvironmentVariable("NUGET_PACKAGES", packageDir);

        Assert.False(Directory.Exists(packageDir));

        Build(testInstance, BuildLevel.All, expectedOutput: "v1", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));

        code = code.Replace("v1", "v2");
        File.WriteAllText(programPath, code);

        Build(testInstance, BuildLevel.Csc, expectedOutput: "v2", customizeCommand: CustomizeCommand);

        code = code.Replace("v2", "v3");
        File.WriteAllText(programPath, code);

        // Clear NuGet cache.
        Directory.Delete(packageDir, recursive: true);
        Assert.False(Directory.Exists(packageDir));

        Build(testInstance, BuildLevel.All, expectedOutput: "v3", customizeCommand: CustomizeCommand);

        Assert.True(Directory.Exists(packageDir));
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
                      <Import Project="Sdk.props" Sdk="Aspire.Hosting.Sdk" Version="9.1.0" />

                      <PropertyGroup>
                        <TargetFramework>net11.0</TargetFramework>
                        <LangVersion>preview</LangVersion>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
                      </ItemGroup>

                      <ItemGroup>
                        <Compile Condition="'$(EnableDefaultCompileItems)' != 'true'" Include="{programPath}" />
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
                        <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Condition="'$(EnableDefaultCompileItems)' != 'true'" Include="{programPath}" />
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
                "Message":{{ToJson(FileBasedProgramsResources.CannotConvertDirective)}}}]}
                """.ReplaceLineEndings(""));
    }

    [Fact]
    public void Api_Diagnostic_02()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
                        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                        <DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>
                        <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
                        <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <PublishAot>true</PublishAot>
                        <PackAsTool>true</PackAsTool>
                      </PropertyGroup>

                      <ItemGroup>
                        <Clean Include="/artifacts/*" />
                      </ItemGroup>

                      <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />

                      <PropertyGroup>
                        <RestoreUseStaticGraphEvaluation>false</RestoreUseStaticGraphEvaluation>
                        <Features>$(Features);FileBasedProgram</Features>
                      </PropertyGroup>

                      <ItemGroup>
                        <Compile Condition="'$(EnableDefaultCompileItems)' != 'true'" Include="{programPath}" />
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
                "Message":{{ToJson(string.Format(FileBasedProgramsResources.UnrecognizedDirective, "unknown"))}}}]}
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory(baseDirectory: cscOnly ? OutOfTreeBaseDirectory : null);
        var filePath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(filePath, """"
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"""EntryPointFilePath: {entryPointFilePath}""");
            """");

        // Remove artifacts from possible previous runs of this test.
        var artifactsDir = VirtualProjectBuilder.GetArtifactsPath(filePath);
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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
        var testInstance = TestAssetsManager.CreateTestDirectory();
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

    [Fact]
    public void EntryPointFilePath_SymbolicLink()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        var fileName = "Program.cs";
        var programPath = Path.Join(testInstance.Path, fileName);
        File.WriteAllText(programPath, """
            #!/usr/bin/env dotnet
            var entryPointFilePath = AppContext.GetData("EntryPointFilePath") as string;
            Console.WriteLine($"EntryPointFilePath: {entryPointFilePath}");
            """);

        new DotnetCommand(Log, "run", fileName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {programPath}");

        var linkName = "linked";
        var linkPath = Path.Join(testInstance.Path, linkName);
        File.CreateSymbolicLink(linkPath, programPath);

        new DotnetCommand(Log, "run", linkName)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"EntryPointFilePath: {linkPath}");
    }

    [Fact]
    public void MSBuildGet_Simple()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "build", "Program.cs", "-getProperty:TargetFramework")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(ToolsetInfo.CurrentTargetFramework);
    }

    /// <summary>
    /// Check that <c>-get</c> commands work the same in project-based and file-based apps.
    /// </summary>
    [Theory]
    [InlineData(true, "build", "--getProperty:TargetFramework;Configuration")]
    [InlineData(true, "build", "--getItem:MyItem", "--getProperty:MyProperty")]
    [InlineData(true, "build", "--getItem:MyItem", "--getProperty:MyProperty", "-t:MyTarget")]
    [InlineData(true, "build", "--getItem:MyItem", "--getProperty:MyProperty", "--getTargetResult:MyTarget")]
    [InlineData(true, "build", "/getProperty:TargetFramework")]
    [InlineData(true, "build", "/getProperty:TargetFramework", "-p:LangVersion=wrong")] // evaluated only, so no failure
    [InlineData(false, "build", "/getProperty:TargetFramework", "-t:Build", "-p:LangVersion=wrong")] // fails with build error but still outputs info
    [InlineData(true, "build", "-getProperty:Configuration", "-getResultOutputFile:out.txt")]
    [InlineData(true, "build", "-getProperty:OutputType,Configuration", "-getResultOutputFile:out1.txt", "-getResultOutputFile:out2.txt")]
    [InlineData(true, "run", "-getProperty:Configuration")] // not supported, the arg is passed through to the app
    [InlineData(true, "restore", "-getProperty:Configuration")]
    [InlineData(true, "publish", "-getProperty:OutputType", "-p:PublishAot=false")]
    [InlineData(true, "pack", "-getProperty:OutputType", "-p:PublishAot=false")]
    [InlineData(true, "clean", "-getProperty:Configuration")]
    public void MSBuildGet_Consistent(bool success, string subcommand, params string[] args)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
                <Target Name="MyTarget" Returns="MyTargetReturn">
                    <ItemGroup>
                        <MyItem Include="item.txt" />
                        <MyTargetReturn Include="return.txt" />
                    </ItemGroup>
                    <PropertyGroup>
                        <MyProperty>MyValue</MyProperty>
                    </PropertyGroup>
                </Target>
            </Project>
            """);

        var fileBasedResult = new DotnetCommand(Log, [subcommand, "Program.cs", .. args])
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        var fileBasedFiles = ReadFiles();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.csproj"), s_consoleProject);

        var projectBasedResult = new DotnetCommand(Log, [subcommand, .. args])
            .WithWorkingDirectory(testInstance.Path)
            .Execute();

        var projectBasedFiles = ReadFiles();

        fileBasedResult.StdOut.Should().Be(projectBasedResult.StdOut);
        fileBasedResult.StdErr.Should().Be(projectBasedResult.StdErr);
        fileBasedResult.ExitCode.Should().Be(projectBasedResult.ExitCode).And.Be(success ? 0 : 1);
        fileBasedFiles.Should().Equal(projectBasedFiles);

        Dictionary<string, string> ReadFiles()
        {
            var result = new DirectoryInfo(testInstance.Path)
                .EnumerateFiles()
                .ExceptBy(["Program.cs", "Directory.Build.props", "Program.csproj"], f => f.Name)
                .ToDictionary(f => f.Name, f => File.ReadAllText(f.FullName));

            foreach (var (file, text) in result)
            {
                Log.WriteLine($"File '{file}':");
                Log.WriteLine(text);
                File.Delete(Path.Join(testInstance.Path, file));
            }

            return result;
        }
    }
}
