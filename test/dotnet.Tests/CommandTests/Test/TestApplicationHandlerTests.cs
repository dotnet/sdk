// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

public class TestApplicationHandlerTests
{
    /// <summary>
    /// Regression test for the handler-level routing fix in https://github.com/dotnet/sdk/pull/52308
    /// (linked to https://github.com/dotnet/sdk/issues/51608). When the test host process exits
    /// after only a controller (non-"TestHost") handshake — i.e. <c>_receivedTestHostHandshake</c>
    /// is false — <see cref="TestApplicationHandler.OnTestProcessExited"/> must route to
    /// <c>HandshakeFailure</c> with the module's TargetPath and TargetFramework preserved, rather
    /// than to <c>AssemblyRunCompleted</c> (which would hit the defensive empty-path fallback in
    /// <c>TerminalTestReporter</c> and drop the assembly identifier from the output).
    /// </summary>
    [Fact]
    public void OnTestProcessExited_WhenOnlyControllerHandshakeReceived_RoutesToHandshakeFailureWithModuleContext()
    {
        var capturingConsole = new CapturingConsole();

        var options = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        };

        using var reporter = new TerminalTestReporter(capturingConsole, options);

        const string targetPath = "/repo/bin/Debug/net9.0/MyTest.dll";
        const string projectPath = "/repo/MyTest.csproj";
        const string targetFramework = "net9.0";

        var module = new TestModule(
            RunProperties: new RunProperties("dotnet", targetPath, "/repo"),
            ProjectFullPath: projectPath,
            TargetFramework: targetFramework,
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: targetPath,
            DotnetRootArchVariableName: null);

        var testOptions = new TestOptions(IsHelp: false, IsDiscovery: false, EnvironmentVariables: new Dictionary<string, string>());

        var handler = new TestApplicationHandler(reporter, module, testOptions);

        // Controller-only handshake — hostType is not "TestHost", so _receivedTestHostHandshake stays false
        // and AssemblyRunStarted is never called (so the executionId is never registered in _assemblies).
        var handshake = new HandshakeMessage(new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.PID] = "1234",
            [HandshakeMessagePropertyNames.Architecture] = "x64",
            [HandshakeMessagePropertyNames.Framework] = ".NETCoreApp,Version=v9.0",
            [HandshakeMessagePropertyNames.OS] = "Windows",
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = ProtocolConstants.SupportedVersions,
            [HandshakeMessagePropertyNames.HostType] = "TestHostController",
            [HandshakeMessagePropertyNames.ModulePath] = targetPath,
            [HandshakeMessagePropertyNames.ExecutionId] = "exec-1",
            [HandshakeMessagePropertyNames.InstanceId] = "inst-1",
        });

        Action handshakeAct = () => handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);
        handshakeAct.Should().NotThrow();

        Action exitAct = () => handler.OnTestProcessExited(exitCode: 1, outputData: "stdout-line", errorData: "stderr-line");
        exitAct.Should().NotThrow();

        reporter.HasHandshakeFailure.Should().BeTrue();

        // The handler must preserve module context (TargetPath + TargetFramework) when routing to
        // HandshakeFailure. If a future regression of #52308 routes through AssemblyRunCompleted's
        // defensive fallback instead, the assembly identifier would be empty and this assertion would fail.
        string rendered = capturingConsole.GetOutput();
        rendered.Should().Contain(targetPath);
        rendered.Should().Contain(targetFramework);
        rendered.Should().Contain("stdout-line");
        rendered.Should().Contain("stderr-line");
    }

    private sealed class CapturingConsole : IConsole
    {
        private readonly StringBuilder _output = new();
        private ConsoleColor _foreground = ConsoleColor.Gray;
        private ConsoleColor _background = ConsoleColor.Black;

#pragma warning disable CS0067 // Event is never used; required by IConsole contract.
        public event ConsoleCancelEventHandler? CancelKeyPress;
#pragma warning restore CS0067

        public int BufferHeight => 30;

        public int BufferWidth => 120;

        public bool IsOutputRedirected => true;

        public string GetOutput() => _output.ToString();

        public void SetForegroundColor(ConsoleColor color) => _foreground = color;

        public void SetBackgroundColor(ConsoleColor color) => _background = color;

        public ConsoleColor GetForegroundColor() => _foreground;

        public ConsoleColor GetBackgroundColor() => _background;

        public void WriteLine() => _output.AppendLine();

        public void WriteLine(string? value) => _output.AppendLine(value);

        public void WriteLine(object? value) => _output.AppendLine(value?.ToString());

        public void WriteLine(string format, object? arg0) => _output.AppendLine(string.Format(format, arg0));

        public void WriteLine(string format, object? arg0, object? arg1) => _output.AppendLine(string.Format(format, arg0, arg1));

        public void WriteLine(string format, object? arg0, object? arg1, object? arg2) => _output.AppendLine(string.Format(format, arg0, arg1, arg2));

        public void WriteLine(string format, object?[]? args) => _output.AppendLine(string.Format(format, args ?? Array.Empty<object?>()));

        public void Write(string format, object?[]? args) => _output.Append(string.Format(format, args ?? Array.Empty<object?>()));

        public void Write(string? value) => _output.Append(value);

        public void Write(char value) => _output.Append(value);

        public void Clear() => _output.Clear();
    }
}
