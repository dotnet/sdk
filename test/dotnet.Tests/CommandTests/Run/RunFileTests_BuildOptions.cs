// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Run.Tests;

[TestClass]
public sealed class RunFileTests_BuildOptions : RunFileTestBase
{
    /// <summary>
    /// Main method is supported just like top-level statements.
    /// </summary>
    [TestMethod]
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
    [TestMethod]
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
    [TestMethod, CombinatorialData]
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
    [TestMethod]
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
    [TestMethod]
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
    [TestMethod]
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
    [TestMethod]
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
    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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
    [TestMethod]
    [DataRow("other;args", "other;args")]
    [DataRow("--;other;args", "other;args")]
    [DataRow("--appArg", "--appArg")]
    [DataRow("-c;Debug;--xyz", "--xyz")]
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
    [TestMethod]
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
    [TestMethod, CombinatorialData]
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
    [TestMethod, CombinatorialData]
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

    [TestMethod, CombinatorialData]
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

    [TestMethod]
    [DataRow("-bl")]
    [DataRow("-BL")]
    [DataRow("-bl:msbuild.binlog")]
    [DataRow("/bl")]
    [DataRow("/bl:msbuild.binlog")]
    [DataRow("--binaryLogger")]
    [DataRow("--binaryLogger:msbuild.binlog")]
    [DataRow("-bl:another.binlog")]
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

    [TestMethod]
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

    [TestMethod]
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
    [TestMethod]
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
    [TestMethod]
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
    [TestMethod]
    public void BinaryLog_EvaluationData_MultiFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"),
            $"""
            #!/usr/bin/env dotnet
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
    [TestMethod]
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

    [TestMethod]
    [DataRow("-tl")]
    [DataRow("-tl:off")]
    [DataRow("-TL:off")]
    [DataRow("-tL:OFF")]
    [DataRow("/tl:off")]
    [DataRow("--terminalLogger:off")]
    [DataRow("-tlp:verbosity=quiet")]
    [DataRow("-TLP:verbosity=quiet")]
    [DataRow("/tlp:DISABLENODEDISPLAY")]
    [DataRow("--terminalLoggerParameters:verbosity=quiet")]
    [DataRow("-clp:NoSummary")]
    [DataRow("-cLp:NoSummary")]
    [DataRow("--consoleLoggerParameters:NoSummary")]
    public void LoggerArgument_Run_ArgumentForms(string arg)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", arg)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("Hello from Program")
            .And.NotHaveStdOutContaining("echo args:");
    }

    [TestMethod, CombinatorialData]
    public void LoggerArgument_Run(bool beforeFile)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        string[] args = beforeFile
            ? ["-tl:off", "Program.cs"]
            : ["Program.cs", "-tl:off"];

        new DotnetCommand(Log, ["run", "--no-cache", .. args])
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");
    }

    [TestMethod]
    public void NoConsoleLogger_Run_SuppressesBuildOutput()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "-v:n")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("Hello from Program")
            .And.HaveStdOutContaining("Program.dll")
            .And.HaveStdOutContaining("CoreCompile");

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "-v:n", "-noconsolelogger")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("Hello from Program");
    }

    [TestMethod]
    public void LoggerArgument_Run_PreservesApplicationArguments()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "-tl:off", "appArg")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:appArg
                Hello from Program
                """);
    }

    [TestMethod]
    public void LoggerArgument_Run_DoubleDashPreservesApplicationArguments()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        new DotnetCommand(Log, "run", "--no-cache", "Program.cs", "--", "-tl:off", "-clp:NoSummary")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("""
                echo args:-tl:off;-clp:NoSummary
                Hello from Program
                """);
    }

    [TestMethod, CombinatorialData]
    public void LoggerArgument_Build(
        [CombinatorialValues("restore", "build")] string command,
        [CombinatorialValues("-tl:off", "-tlp:verbosity=quiet", "-clp:NoSummary")] string loggerArg,
        bool beforeFile)
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), s_program);

        string[] args = beforeFile
            ? [command, loggerArg, "Program.cs"]
            : [command, "Program.cs", loggerArg];

        new DotnetCommand(Log, args)
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass();
    }

    [TestMethod, CombinatorialData]
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

    [TestMethod]
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

    [TestMethod] // https://github.com/dotnet/sdk/issues/50227
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

    [TestMethod]
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

    [TestMethod]
    public void MissingShebangWarning()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Single-file program without shebang should NOT produce CA2266
        // (the warning only fires when there are multiple files via #:include).
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine("hello");
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello");

        // Included file without shebang should not produce CA2266.
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
            class Util { public static string Greet() => "hello"; }
            """);

        // Entry point with shebang and #:include — no warning.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #!/usr/bin/env dotnet
            #:include Util.cs
            Console.WriteLine(Util.Greet());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello");

        // Entry point without shebang and #:include — CA2266 warning expected.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include Util.cs
            Console.WriteLine(Util.Greet());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello");

        // CA2266 can be suppressed via NoWarn.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property NoWarn=CA2266
            #:include Util.cs
            Console.WriteLine(Util.Greet());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello");
    }

    [TestMethod]
    public void MissingShebangWarning_RefDirective()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        EnableRefDirective(testInstance);

        File.WriteAllText(Path.Join(testInstance.Path, "refLib.cs"), """
            #:property OutputType=Library
            namespace RefLib;
            public static class Greeter
            {
                public static string Greet() => "hello from ref";
            }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "refApp.cs"), """
            #:ref refLib.cs
            Console.WriteLine(RefLib.Greeter.Greet());
            """);

        new DotnetCommand(Log, "run", "refApp.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello from ref");

        File.WriteAllText(Path.Join(testInstance.Path, "refApp.cs"), """
            #!/usr/bin/env dotnet
            #:ref refLib.cs
            Console.WriteLine(RefLib.Greeter.Greet());
            """);

        new DotnetCommand(Log, "run", "refApp.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello from ref");
    }

    [TestMethod]
    public void MissingShebangWarning_CompileItemFromDirectoryBuildProps()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        // Directory.Build.props adds a Compile item, but CA2266 should only fire
        // for files included via #:include.
        File.WriteAllText(Path.Join(testInstance.Path, "Util.cs"), """
            class Util { public static string Greet() => "hello"; }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Included.cs"), """
            class Included { public static string Greet() => "included"; }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Directory.Build.props"), """
            <Project>
              <ItemGroup>
                <Compile Include="Util.cs" />
              </ItemGroup>
            </Project>
            """);

        // Entry point without shebang does not warn because the extra Compile item
        // was not added by #:include.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            Console.WriteLine(Util.Greet());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello");

        // Adding shebang should keep the program warning-free.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #!/usr/bin/env dotnet
            Console.WriteLine(Util.Greet());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOut("hello");

        // A real #:include without shebang should still warn.
        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include Included.cs
            Console.WriteLine($"{Util.Greet()} {Included.Greet()}");
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello included");
    }

    [TestMethod]
    public void MissingShebangWarning_NonCsFile()
    {
        var testInstance = TestAssetsManager.CreateTestDirectory();

        File.WriteAllText(Path.Join(testInstance.Path, "file.json"), "{}");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:include file.json
            Console.WriteLine("hello");
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello");

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property ext=.json
            #:include file$(ext)
            Console.WriteLine("hello");
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello");

        File.WriteAllText(Path.Join(testInstance.Path, "file.cs"), """
            class Util { public static string Greet() => "hello from util"; }
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), """
            #:property ext=.cs
            #:include file$(ext)
            Console.WriteLine(Util.Greet());
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello from util");

        File.WriteAllText(Path.Join(testInstance.Path, "file.cs"), """
            Console.WriteLine("hello from file");
            """);

        File.WriteAllText(Path.Join(testInstance.Path, "Program.cs"), $"""
            #:property {CSharpDirective.IncludeOrExclude.MappingPropertyName}=.cs=Content
            #:include file.cs
            Console.WriteLine("hello");
            """);

        new DotnetCommand(Log, "run", "Program.cs")
            .WithWorkingDirectory(testInstance.Path)
            .Execute()
            .Should().Pass()
            .And.HaveStdOutContaining("warning CA2266")
            .And.HaveStdOutContaining("hello");
    }

    /// <summary>
    /// File-based projects using the default SDK do not include embedded resources by default.
    /// </summary>
    [TestMethod]
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
    [TestMethod, CombinatorialData]
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
