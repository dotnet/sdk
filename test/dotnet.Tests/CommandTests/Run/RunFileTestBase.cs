// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTestFixture(IMessageSink sink) : IAsyncLifetime
{
    public System.Threading.Tasks.Task InitializeAsync()
    {
        RunFileTestBase.CopyNuGetConfigToRunfileDirectory();

        // Ensure a simple app runs fully with MSBuild before running other csc-only tests
        // so we have packages like ILLink.Tasks restored and csc-only optimization can kick in.
        new DotnetCommand(new SharedTestOutputHelper(sink), "run", "-")
            .WithStandardInput("""
                Console.WriteLine("Hello");
                """)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello");

        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task DisposeAsync() => System.Threading.Tasks.Task.CompletedTask;
}

public abstract class RunFileTestBase(ITestOutputHelper log) : SdkTest(log), IClassFixture<RunFileTestFixture>
{
    internal static string s_includeExcludeDefaultKnownExtensions
        => field ??= string.Join(", ", CSharpDirective.IncludeOrExclude.DefaultMapping.Select(static e => e.Extension));

    internal static readonly string s_program = /* lang=C#-Test */ """
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

    internal static readonly string s_programDependingOnUtil = /* lang=C#-Test */ """
        if (args.Length > 0)
        {
            Console.WriteLine("echo args:" + string.Join(";", args));
        }
        Console.WriteLine("Hello, " + Util.GetMessage());
        """;

    internal static readonly string s_util = /* lang=C#-Test */ """
        static class Util
        {
            public static string GetMessage()
            {
                return "String from Util";
            }
        }
        """;

    internal static readonly string s_programReadingEmbeddedResource = /* lang=C#-Test */ """
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

    internal static readonly string s_resx = """
        <root>
          <data name="MyString">
            <value>TestValue</value>
          </data>
        </root>
        """;

    internal static readonly string s_consoleProject = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>{ToolsetInfo.CurrentTargetFramework}</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;

    internal static readonly string s_launchSettings = """
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
    internal static string OutOfTreeBaseDirectory => field ??= PrepareOutOfTreeBaseDirectory();

    internal static bool HasCaseInsensitiveFileSystem
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

    /// <summary>
    /// Copies NuGet.config to the runfile base directory so virtual projects created by
    /// <c>dotnet run -</c> (stdin) can resolve packages from test feeds. The virtual project
    /// is created under this directory, and NuGet walks up from the project location to
    /// find config files.
    /// </summary>
    internal static void CopyNuGetConfigToRunfileDirectory()
    {
        var sourceNuGetConfig = Path.Join(SdkTestContext.Current.TestExecutionDirectory, "NuGet.config");
        var runfileDir = VirtualProjectBuilder.GetTempSubdirectory();
        Directory.CreateDirectory(runfileDir);
        File.Copy(sourceNuGetConfig, Path.Join(runfileDir, "NuGet.config"), overwrite: true);
    }

    internal static string DirectiveError(string path, int line, string messageFormat, params ReadOnlySpan<object> args)
    {
        return $"{path}({line}): {FileBasedProgramsResources.DirectiveError}: {string.Format(messageFormat, args)}";
    }

    internal static void EnableRefDirective(TestDirectory testInstance)
    {
        var propsPath = Path.Join(testInstance.Path, "Directory.Build.props");
        var propsContent = File.Exists(propsPath) ? File.ReadAllText(propsPath) : null;
        if (propsContent is not null && propsContent.Contains(CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective))
        {
            return;
        }

        File.WriteAllText(propsPath, $"""
            <Project>
              <PropertyGroup>
                <{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>true</{CSharpDirective.Ref.ExperimentalFileBasedProgramEnableRefDirective}>
              </PropertyGroup>
            </Project>
            """);
    }


    internal static void VerifyBinLogEvaluationDataCount(string binaryLogPath, int expectedCount)
    {
        var records = BinaryLog.ReadRecords(binaryLogPath).ToList();
        records.Count(static r => r.Args is ProjectEvaluationStartedEventArgs).Should().Be(expectedCount);
        records.Count(static r => r.Args is ProjectEvaluationFinishedEventArgs).Should().Be(expectedCount);
    }

    private protected void Build(
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

}
