// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

public sealed class RunFileTests_BuildOptions(ITestOutputHelper log) : RunFileTestBase(log)
{
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

        var tempDir = Directory.CreateTempSubdirectory();
        var workDir = TestPathUtility.ResolveTempPrefixLink(tempDir.FullName).TrimEnd(Path.DirectorySeparatorChar);

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

        tempDir.Delete();

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

        var tempDir = Directory.CreateTempSubdirectory();
        var workDir = TestPathUtility.ResolveTempPrefixLink(tempDir.FullName).TrimEnd(Path.DirectorySeparatorChar);

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

        tempDir.Delete();

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
    /// <c>dotnet run -bl file.cs</c> produces a binary log.
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

        // There should be exactly three - two for restore, one for build.
        VerifyBinLogEvaluationDataCount(binaryLogPath, expectedCount: 3);
    }


    /// <summary>
    /// Binary logs from our in-memory projects should have evaluation data.
    /// </summary>
    [Fact]
    public void BinaryLog_EvaluationData_MultiFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <PropertyGroup>
                <ExperimentalFileBasedProgramEnableIncludeDirective>true</ExperimentalFileBasedProgramEnableIncludeDirective>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"),
            $"""
            #:include *.cs
            {s_programDependingOnUtil}
            """);

        var utilPath = Path.Join(testInstance.Path, "Util.cs");
        File.WriteAllText(utilPath, s_util);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "-bl:first.binlog")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, String from Util");

        string binaryLogPath = Path.Join(testInstance.Path, "first.binlog");
        new FileInfo(binaryLogPath).Should().Exist();

        // There should be exactly four - two for restore and one for build as usual, plus one for initial directive evaluation.
        var expectedCount = 4;
        VerifyBinLogEvaluationDataCount(binaryLogPath, expectedCount: expectedCount);

        File.WriteAllText(utilPath, s_util.Replace("String from Util", "v2"));

        new DotnetCommand(Log, "run", "Program.cs", "-bl:second.binlog")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello, v2");

        binaryLogPath = Path.Join(testInstance.Path, "second.binlog");
        new FileInfo(binaryLogPath).Should().Exist();

        // After rebuild, there should be the same number of evaluations.
        VerifyBinLogEvaluationDataCount(binaryLogPath, expectedCount: expectedCount);
    }

    /// <summary>
    /// If we skip build due to up-to-date check, no binlog should be created.
    /// </summary>
    [Fact]
    public void BinaryLog_EvaluationData_UpToDate()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        var programPath = Path.Join(testInstance.Path, "Program.cs");
        File.WriteAllText(programPath, s_program);

        var expectedOutput = "Hello from Program";

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut(expectedOutput);

        string binaryLogPath = Path.Join(testInstance.Path, "msbuild.binlog");
        new FileInfo(binaryLogPath).Should().NotExist();

        new DotnetCommand(Log, "run", "Program.cs", "-bl")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut($"""
                {CliCommandStrings.NoBinaryLogBecauseUpToDate}
                {expectedOutput}
                """);

        new FileInfo(binaryLogPath).Should().NotExist();
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

}
