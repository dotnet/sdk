// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Moq;

namespace dotnet.Tests.CommandTests.Test;

public class TestApplicationHandlerTests
{
    [Fact]
    public void OnTestProcessExited_AfterControllerHandshakeWithoutTestHostHandshake_DoesNotThrowAndReportsHandshakeFailure()
    {
        var consoleMock = new Mock<IConsole>();
        consoleMock.SetupGet(c => c.BufferWidth).Returns(120);
        consoleMock.SetupGet(c => c.BufferHeight).Returns(30);
        consoleMock.SetupGet(c => c.IsOutputRedirected).Returns(true);

        var output = new TerminalTestReporter(consoleMock.Object, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.NoAnsi,
            ShowAssembly = true,
            ShowAssemblyStartAndComplete = true,
        });

        var module = new TestModule(
            new RunProperties("testhost.exe", null, null),
            ProjectFullPath: null,
            TargetFramework: "net9.0",
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: "testhost.exe",
            DotnetRootArchVariableName: null);
        var handler = new TestApplicationHandler(
            output,
            module,
            new TestOptions(IsHelp: false, IsDiscovery: false, EnvironmentVariables: new Dictionary<string, string>()));

        handler.OnHandshakeReceived(
            new HandshakeMessage(new Dictionary<byte, string>
            {
                [HandshakeMessagePropertyNames.ExecutionId] = "execution-id",
                [HandshakeMessagePropertyNames.Architecture] = "x64",
                [HandshakeMessagePropertyNames.Framework] = ".NET 9.0.0",
                [HandshakeMessagePropertyNames.HostType] = "TestHostController",
            }),
            gotSupportedVersion: true);

        Action act = () => handler.OnTestProcessExited(exitCode: 1, outputData: "out", errorData: "err");

        act.Should().NotThrow();
        output.HasHandshakeFailure.Should().BeTrue();
    }
}
