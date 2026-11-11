// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Moq;

namespace dotnet.Tests.CommandTests.Test;

public class TerminalTestReporterTests
{
    [Theory]
    [InlineData(40, false)]
    [InlineData(80, true)]
    [InlineData(120, true)]
    public void AnsiProgress_PrioritizesTargetAndArchitectureOverRuntime(int width, bool showsRuntime)
    {
        var console = new CapturingTestConsole();
        var terminal = new AnsiTerminal(console.Console, baseDirectory: null);
        IStopwatch clock = Mock.Of<IStopwatch>(stopwatch => stopwatch.Elapsed == TimeSpan.FromSeconds(1));
        var progress = new TestProgressState(1, "Tests.exe", "net472", "x64", clock, false)
        {
            RuntimeFramework = "net481",
        };
        var frame = new AnsiTerminalTestProgressFrame(width, 30);
        var line = new AnsiTerminalTestProgressFrame.RenderedProgressItem(1, 0);

        terminal.StartUpdate();
        frame.AppendTestWorkerProgress(progress, line, terminal);
        terminal.StopUpdate();

        string output = StripAnsi(console.GetOutput());
        output.Should().Contain(showsRuntime ? "(net472|x64, runtime: net481)" : "(net472|x64)");
        output.Length.Should().BeLessThanOrEqualTo(width);
        if (!showsRuntime)
        {
            output.Should().NotContain("runtime:");
        }
    }

    [Fact]
    public void AnsiProgress_TruncatesLongAssemblyNameBeforeDroppingPrimaryIdentity()
    {
        var console = new CapturingTestConsole();
        var terminal = new AnsiTerminal(console.Console, baseDirectory: null);
        IStopwatch clock = Mock.Of<IStopwatch>(stopwatch => stopwatch.Elapsed == TimeSpan.FromSeconds(1));
        var progress = new TestProgressState(
            1, "Very.Long.Test.Assembly.Name.That.Must.Not.Hide.Target.exe", "net472", "x64", clock, false)
        {
            RuntimeFramework = "net481",
        };
        var frame = new AnsiTerminalTestProgressFrame(40, 30);
        var line = new AnsiTerminalTestProgressFrame.RenderedProgressItem(1, 0);

        terminal.StartUpdate();
        frame.AppendTestWorkerProgress(progress, line, terminal);
        terminal.StopUpdate();

        string output = StripAnsi(console.GetOutput());
        output.Should().Contain("...").And.Contain("(net472|x64)").And.NotContain("runtime:");
        output.Length.Should().BeLessThanOrEqualTo(40);
    }

    [Theory]
    [InlineData("net472", "net472|x64, runtime: net481")]
    [InlineData("net481", "net481|x64")]
    [InlineData(null, "x64, runtime: net481")]
    public void SimpleProgress_PreservesTargetAndSeparateRuntime(string? targetFramework, string expected)
    {
        var console = new CapturingTestConsole();
        var terminal = new NonAnsiTerminal(console.Console);
        var progress = new TestProgressState(1, "Tests.exe", targetFramework, "x64", Mock.Of<IStopwatch>(), false)
        {
            RuntimeFramework = "net481",
        };

        terminal.RenderProgress([progress]);

        console.GetOutput().Should().Contain($"Tests.exe ({expected})");
    }

    [Fact]
    public void LegacyRetryCall_PreservesEstablishedTargetArchitectureAndRuntime()
    {
        var console = new CapturingTestConsole();
        using var reporter = new TerminalTestReporter(console.Console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        reporter.AssemblyRunStarted("Tests.exe", "net472", "x64", "execution", "first", runtimeFramework: "net481");
        reporter.AssemblyRunStarted("Tests.exe", null, null, "execution", "second");
        reporter.AssemblyRunCompleted("execution", 0, null, null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        Regex.Matches(console.GetOutput(), Regex.Escape("Tests.exe (net472|x64, runtime: net481)")).Should().HaveCount(3);
        console.GetOutput().Should().Contain("(try 2)");
    }

    [Fact]
    public void SingleAssemblySummary_PreservesIdentityWhenAssemblyOutputIsDisabled()
    {
        var console = new CapturingTestConsole();
        using var reporter = new TerminalTestReporter(console.Console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowAssembly = false,
        });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        reporter.AssemblyRunStarted("Tests.exe", "net472", "x64", "execution", "first", runtimeFramework: "net481");
        reporter.AssemblyRunCompleted("execution", 0, null, null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        Regex.Matches(console.GetOutput(), Regex.Escape("Tests.exe (net472|x64, runtime: net481)")).Should().HaveCount(1);
    }

    [Fact]
    public void DiscoverySummary_PreservesTargetAndSeparateRuntime()
    {
        var console = new CapturingTestConsole();
        using var reporter = new TerminalTestReporter(console.Console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, true, false, false);
        reporter.AssemblyRunStarted("Tests.exe", "net472", "x64", "execution", "first", runtimeFramework: "net481");
        reporter.TestDiscovered("execution", "One", "one");
        reporter.AssemblyRunCompleted("execution", 0, null, null);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        Regex.Matches(console.GetOutput(), Regex.Escape("Tests.exe (net472|x64, runtime: net481)")).Should().HaveCount(2);
        console.GetOutput().Should().Contain("One");
    }

    private static string StripAnsi(string value) => Regex.Replace(value, "\x1b\\[[0-9;]*[a-zA-Z]", string.Empty);
}

internal sealed class CapturingTestConsole
{
    private readonly StringBuilder _output = new();

    public CapturingTestConsole()
    {
        var console = new Mock<IConsole>();
        console.SetupGet(value => value.IsOutputRedirected).Returns(true);
        console.SetupGet(value => value.BufferWidth).Returns(120);
        console.SetupGet(value => value.BufferHeight).Returns(30);
        console.Setup(value => value.Write(It.IsAny<string>())).Callback<string>(value => _output.Append(value));
        console.Setup(value => value.Write(It.IsAny<char>())).Callback<char>(value => _output.Append(value));
        console.Setup(value => value.WriteLine(It.IsAny<string>())).Callback<string>(value => _output.AppendLine(value));
        console.Setup(value => value.WriteLine()).Callback(() => _output.AppendLine());
        Console = console.Object;
    }

    public IConsole Console { get; }

    public string GetOutput() => _output.ToString();
}
