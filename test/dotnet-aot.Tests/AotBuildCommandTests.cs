// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.Build.Server;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[ResourceLock(nameof(Reporter))]
[ResourceLock(nameof(SdkDirectoryScope))]
[ResourceLock(WellKnownResources.Console)]
[ResourceLock(WellKnownResources.EnvironmentVariables)]
public class AotBuildCommandTests
{
    private static readonly string[] s_environmentVariables =
    [
        "MSBUILDUSESERVER",
        "DOTNET_CLI_USE_MSBUILD_SERVER",
        "DOTNET_CLI_RUN_MSBUILD_OUTOFPROC",
        "MSBUILD_EXE_PATH",
        "MSBUILD_LOGGING_ARGS",
        "MSBUILDDEBUGONSTART",
        "MSBUILDDUMPPROCESSCOUNTERS",
        "MSBUILDENSURESTDOUTFORTASKPROCESSES",
        "MSBUILDDISABLENODEREUSE",
        "MSBUILDFORCEMULTITHREADED",
        "MSBUILDENABLEMULTITHREADED",
        "MSBUILDTERMINALLOGGER",
        "MSBUILDLIVELOGGER",
    ];

    private readonly Dictionary<string, string?> _originalEnvironment = [];
    private string _directory = null!;
    private string _project = null!;
    private CultureInfo _originalCulture = null!;

    [TestInitialize]
    public void Initialize()
    {
        foreach (string name in s_environmentVariables)
        {
            _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        _originalCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        _directory = Path.Join(Path.GetTempPath(), $"dotnet-aot-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _project = Path.Join(_directory, "App.csproj");
        File.WriteAllText(_project, "<Project />");
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach ((string name, string? value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
        CultureInfo.CurrentUICulture = _originalCulture;
        Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public void EligibleBuildsReachTheSharedBuildHandler()
    {
        var explicitProject = Parser.Parse(["build", _project, "--no-restore", "-bl:1.binlog"]);
        var discoveredProject = Parser.Parse(["build", "--no-restore"]);

        Assert.AreEqual(42, AotBuildCommand.Execute(explicitProject, _ => 42, _directory));
        Assert.AreEqual(43, AotBuildCommand.Execute(discoveredProject, _ => 43, _directory));
    }

    [TestMethod]
    [DataRow(null, null, true)]
    [DataRow(null, "false", false)]
    [DataRow("1", "false", true)]
    [DataRow("0", "true", false)]
    [DataRow("true", "true", false)]
    public void ServerSelectionRespectsMsbuildOverride(string? msbuildUseServer, string? cliUseServer, bool eligible)
    {
        Environment.SetEnvironmentVariable("MSBUILDUSESERVER", msbuildUseServer);
        Environment.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", cliUseServer);
        var parseResult = Parser.Parse(["build", _project]);

        if (eligible)
        {
            Assert.AreEqual(42, AotBuildCommand.Execute(parseResult, _ => 42, _directory));
        }
        else
        {
            Assert.ThrowsExactly<CommandNotAvailableInAotException>(() =>
                AotBuildCommand.Execute(parseResult, _ => throw new AssertFailedException("Build was started."), _directory));
        }
    }

    [TestMethod]
    [DataRow("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC", "1")]
    [DataRow("MSBUILD_EXE_PATH", "custom")]
    [DataRow("MSBUILD_LOGGING_ARGS", "-nr:false")]
    [DataRow("MSBUILDDEBUGONSTART", "1")]
    [DataRow("MSBUILDDUMPPROCESSCOUNTERS", "1")]
    [DataRow("MSBUILDENSURESTDOUTFORTASKPROCESSES", "1")]
    [DataRow("MSBUILDDISABLENODEREUSE", "1")]
    [DataRow("MSBUILDFORCEMULTITHREADED", "1")]
    [DataRow("MSBUILDENABLEMULTITHREADED", "1")]
    public void UnsupportedEnvironmentFallsBackBeforeStartingBuild(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        AssertFallback(["build", _project]);
    }

    [TestMethod]
    [DataRow("--disable-build-servers")]
    [DataRow("--getProperty:TargetFramework")]
    [DataRow("-nr:false")]
    [DataRow("-mt")]
    [DataRow("--unknown-option")]
    public void UnsupportedOptionsFallBackBeforeStartingBuild(string option)
        => AssertFallback(["build", _project, option]);

    [TestMethod]
    public void AmbiguousInputsAndResponseFilesFallBack()
    {
        File.WriteAllText(Path.Join(_directory, "Other.csproj"), "<Project />");
        AssertFallback(["build"]);
        AssertFallback(["build", _project, Path.Join(_directory, "Other.csproj")]);
        AssertFallback(["build", Path.Join(_directory, "Program.cs")]);

        File.WriteAllText(Path.Join(_directory, "Directory.Build.rsp"), "-nr:false");
        AssertFallback(["build", _project]);
        File.Delete(Path.Join(_directory, "Directory.Build.rsp"));
        File.WriteAllText(Path.Join(_directory, "MSBuild.rsp"), "-nr:false");
        AssertFallback(["build", _project]);
    }

    [TestMethod]
    [DataRow("restore")]
    [DataRow("clean")]
    [DataRow("test")]
    public void EligibleCommandsReachTheirSharedHandler(string command)
    {
        string[] options = command switch
        {
            "restore" => ["--locked-mode", "--verbosity", "minimal"],
            "clean" => ["--configuration", "Release"],
            _ => ["--no-restore", "--tl:on", "--filter", "Name=Example"],
        };

        var explicitProject = ParseCommand(command, [_project, .. options, "-bl:1.binlog"]);
        var discoveredProject = ParseCommand(command, options);
        Assert.AreEqual(42, ExecuteCommand(command, explicitProject, _ => 42));
        Assert.AreEqual(43, ExecuteCommand(command, discoveredProject, _ => 43));
    }

    [TestMethod]
    [DataRow("-p:Example=value")]
    [DataRow("--property:Example=value")]
    [DataRow("/p:Example=value")]
    public void CleanAcceptsInlineMsbuildProperties(string property)
    {
        var result = ParseCommand("clean", [_project, property]);
        Assert.AreEqual(42, ExecuteCommand("clean", result, _ => 42));
    }

    [TestMethod]
    [DataRow("-p:Example")]
    [DataRow("-p:=value")]
    public void CleanRejectsAmbiguousMsbuildProperties(string property)
        => AssertCommandFallback("clean", [_project, property]);

    [TestMethod]
    [DataRow("restore")]
    [DataRow("clean")]
    [DataRow("test")]
    public void DisabledServerAndResponseFilesFallBackBeforeStartingCommand(string command)
    {
        string[] options = command == "test" ? ["--tl:on"] : [];
        Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        AssertCommandFallback(command, [_project, .. options]);

        Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
        File.WriteAllText(Path.Join(_directory, "Directory.Build.rsp"), "-nr:false");
        AssertCommandFallback(command, [_project, .. options]);
    }

    [TestMethod]
    [DataRow("restore", "--disable-build-servers")]
    [DataRow("clean", "--disable-build-servers")]
    [DataRow("restore", "--getProperty:TargetFramework")]
    [DataRow("clean", "--getItem:Compile")]
    [DataRow("restore", "--unknown-option")]
    [DataRow("clean", "--unknown-option")]
    [DataRow("test", "--disable-build-servers")]
    [DataRow("test", "--unknown-option")]
    public void UnsupportedCommandOptionsFallBackBeforeStartingCommand(string command, string option)
    {
        string[] testOptions = command == "test" ? ["--tl:on"] : [];
        AssertCommandFallback(command, [_project, .. testOptions, option]);
    }

    [TestMethod]
    public void VSTestLegacyOutputAndNonProjectShapesFallBack()
    {
        AssertCommandFallback("test", [_project, "--tl:off"]);
        AssertCommandFallback("test", [_project, "--tl:on", "-p:VsTestUseMSBuildOutput=false"]);
        AssertCommandFallback("test", [_project, "--tl:on", "--environment", "NAME=value"]);
        AssertCommandFallback("test", [_project, "--tl:on", "--", "RunConfiguration.MaxCpuCount=1"]);
        AssertCommandFallback("test", [Path.Join(_directory, "Tests.dll"), "--tl:on"]);
        AssertCommandFallback("test", [Path.Join(_directory, "Program.cs"), "--tl:on"]);
        AssertCommandFallback("test", [_project, Path.Join(_directory, "Other.csproj"), "--tl:on"]);
    }

    [TestMethod]
    [DataRow("restore")]
    [DataRow("clean")]
    [DataRow("test")]
    public void UnsupportedInputsFallBackThroughTheRegisteredAction(string command)
    {
        Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
        var result = ParseCommand(command, command == "test" ? [_project, "--tl:on"] : [_project]);
        Assert.ThrowsExactly<CommandNotAvailableInAotException>(() => Parser.Invoke(result));
    }

    [TestMethod]
    [DataRow("pack")]
    [DataRow("publish")]
    public void CommandsRequiringInProcessEvaluationStayManaged(string command)
    {
        var result = Parser.Parse([command, _project, "--no-restore"]);
        Assert.ThrowsExactly<CommandNotAvailableInAotException>(() => Parser.Invoke(result));
    }

    [TestMethod]
    [DataRow(MSBuildClientExitType.ServerBusy)]
    [DataRow(MSBuildClientExitType.UnableToConnect)]
    [DataRow(MSBuildClientExitType.LaunchError)]
    [DataRow(MSBuildClientExitType.UnknownServerState)]
    public void PreSubmissionFailuresCanForwardOnce(MSBuildClientExitType exitType)
    {
        int forwards = 0;
        int result = MSBuildForwardingApp.CompleteServerClientResult(
            new MSBuildClientExitResult { MSBuildClientExitType = exitType },
            cancellationRequested: false,
            () => { forwards++; return 17; });

        Assert.AreEqual(17, result);
        Assert.AreEqual(1, forwards);
    }

    [TestMethod]
    public void UncertainSubmissionAndCancellationNeverForward()
    {
        static int FailIfForwarded() => throw new AssertFailedException("The build must not be replayed.");

        Assert.AreEqual(1, MSBuildForwardingApp.CompleteServerClientResult(
            new MSBuildClientExitResult { MSBuildClientExitType = MSBuildClientExitType.Unexpected },
            cancellationRequested: false, FailIfForwarded));
        Assert.AreEqual(1, MSBuildForwardingApp.CompleteServerClientResult(
            new MSBuildClientExitResult { MSBuildClientExitType = MSBuildClientExitType.ServerBusy },
            cancellationRequested: true, FailIfForwarded));
        Assert.AreEqual(1, MSBuildForwardingApp.CompleteServerClientResult(
            new MSBuildClientExitResult { MSBuildClientExitType = MSBuildClientExitType.Success },
            cancellationRequested: false, FailIfForwarded));
    }

    [TestMethod]
    [DataRow("Success", 0)]
    [DataRow("BuildError", 1)]
    public void ServerBuildResultIsNotReplayed(string exitType, int expected)
    {
        int result = MSBuildForwardingApp.CompleteServerClientResult(
            new MSBuildClientExitResult
            {
                MSBuildClientExitType = MSBuildClientExitType.Success,
                MSBuildAppExitTypeString = exitType,
            },
            cancellationRequested: false,
            () => throw new AssertFailedException("The build must not be replayed."));

        Assert.AreEqual(expected, result);
    }

    private void AssertFallback(string[] arguments)
    {
        var parseResult = Parser.Parse(arguments);
        Assert.ThrowsExactly<CommandNotAvailableInAotException>(() =>
            AotBuildCommand.Execute(parseResult, _ => throw new AssertFailedException("Build was started."), _directory));
    }

    private ParseResult ParseCommand(string command, string[] arguments)
    {
        if (command != "test")
        {
            return Parser.Parse([command, .. arguments]);
        }

        var root = new RootCommand();
        TestCommandDefinition test = TestCommandDefinition.Create(_directory, "VSTest");
        AotTestCommand.ConfigureCommand(test);
        root.Subcommands.Add(test);
        return root.Parse(["test", .. arguments], Parser.ParserConfiguration);
    }

    private int ExecuteCommand(string command, ParseResult parseResult, Func<ParseResult, int> run)
        => command switch
        {
            "restore" => AotMSBuildServerCommand.ExecuteRestore(parseResult, run, _directory),
            "clean" => AotMSBuildServerCommand.ExecuteClean(parseResult, run, _directory),
            "test" => AotTestCommand.ExecuteVSTest(parseResult, run, _directory),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

    private void AssertCommandFallback(string command, string[] arguments)
    {
        ParseResult result = ParseCommand(command, arguments);
        Assert.ThrowsExactly<CommandNotAvailableInAotException>(() =>
            ExecuteCommand(command, result, _ => throw new AssertFailedException("Command was started.")));
    }
}
