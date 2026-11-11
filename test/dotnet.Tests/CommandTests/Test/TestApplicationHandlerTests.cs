// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

public class TestApplicationHandlerTests
{
    [Theory]
    [InlineData("net472", null, ".NET Framework 4.8.10008.0", "net472|x64, runtime: net481")]
    [InlineData("net481", "net472", ".NET Framework 4.8.10008.0", "net481|x64")]
    [InlineData(null, "net472", ".NET Framework 4.8.10008.0", "net472|x64, runtime: net481")]
    [InlineData(null, "net481", ".NET Framework 4.8.10008.0", "net481|x64")]
    [InlineData(null, null, ".NET Framework 4.8.10008.0", "x64, runtime: net481")]
    [InlineData("", " ", ".NET Framework 4.8.10008.0", "x64, runtime: net481")]
    [InlineData(" ", "net472", ".NET Framework 4.8.10008.0", "net472|x64, runtime: net481")]
    [InlineData("net10.0-windows10.0.19041.0", null, ".NET 10.0.15", "net10.0-windows10.0.19041.0|x64")]
    [InlineData("net8.0", null, ".NET 10.0.15", "net8.0|x64, runtime: net10.0")]
    [InlineData(null, null, "Unknown runtime", "x64, runtime: Unknown runtime")]
    public void Handshake_PrefersEvaluatedTargetThenOptionalMetadataWithoutInferringFromRuntime(
        string? projectTarget, string? handshakeTarget, string runtime, string identity)
    {
        var console = new CapturingTestConsole();
        using var reporter = CreateReporter(console);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        var handler = CreateHandler(reporter, projectTarget);

        handler.OnHandshakeReceived(CreateHandshake(handshakeTarget, runtime), gotSupportedVersion: true);
        handler.OnTestResultsReceived(PassedResult("first"));
        handler.OnTestProcessExited(0, string.Empty, string.Empty);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        Regex.Matches(console.GetOutput(), Regex.Escape($"Tests.exe ({identity})")).Should().HaveCount(3,
            "start, test result and completion must identify the same target and executing runtime");
        console.GetOutput().Should().Contain("total: 1").And.Contain("succeeded: 1");
    }

    [Theory]
    [InlineData("net472", "net472|x64, runtime: net481")]
    [InlineData(null, "x64, runtime: net481")]
    public void Retry_DoesNotChangeEstablishedTargetIdentity(string? initialTarget, string identity)
    {
        var console = new CapturingTestConsole();
        using var reporter = CreateReporter(console);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, true);
        var handler = CreateHandler(reporter, projectTarget: null);

        handler.OnHandshakeReceived(CreateHandshake(initialTarget), gotSupportedVersion: true);
        handler.OnTestResultsReceived(new TestResultMessages("execution", "first", [],
            [new FailedTestResultMessage("one", "One", TestStates.Failed, null, null, [], null, null, null)]));
        handler.OnHandshakeReceived(CreateHandshake("net481", instanceId: "second"), gotSupportedVersion: true);
        handler.OnTestResultsReceived(PassedResult("second"));
        handler.OnTestProcessExited(0, string.Empty, string.Empty);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        Regex.Matches(console.GetOutput(), Regex.Escape($"Tests.exe ({identity})")).Should().HaveCount(5);
        console.GetOutput().Should().Contain("total: 1 (+1 retried)").And.Contain("failed: 0");
    }

    [Fact]
    public void ControllerBeforeHost_DoesNotPreventOptionalHostTargetMetadata()
    {
        var console = new CapturingTestConsole();
        using var reporter = CreateReporter(console);
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        var handler = CreateHandler(reporter, projectTarget: null);
        HandshakeMessage controller = CreateHandshake(targetFramework: null);
        controller.Properties[HandshakeMessagePropertyNames.HostType] = "TestHostController";

        handler.OnHandshakeReceived(controller, gotSupportedVersion: true);
        handler.OnHandshakeReceived(CreateHandshake("net472"), gotSupportedVersion: true);
        handler.OnTestResultsReceived(PassedResult("first"));
        handler.OnTestProcessExited(0, string.Empty, string.Empty);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, 0);

        Regex.Matches(console.GetOutput(), Regex.Escape("Tests.exe (net472|x64, runtime: net481)")).Should().HaveCount(3);
        console.GetOutput().Should().NotContain("(try 2)");
    }

    [Fact]
    public void OptionalTarget_DoesNotReplaceRequiredRuntimeField()
    {
        using var reporter = CreateReporter(new CapturingTestConsole());
        var handler = CreateHandler(reporter, projectTarget: "net472");
        HandshakeMessage handshake = CreateHandshake("net472");
        handshake.Properties.Remove(HandshakeMessagePropertyNames.Framework);

        Action action = () => handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        action.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Retry_StillRejectsMismatchingRuntime()
    {
        using var reporter = CreateReporter(new CapturingTestConsole());
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, 1, false, false, false);
        var handler = CreateHandler(reporter, projectTarget: "net472");
        handler.OnHandshakeReceived(CreateHandshake("net472"), gotSupportedVersion: true);

        Action action = () => handler.OnHandshakeReceived(
            CreateHandshake("net472", runtime: ".NET Framework 4.7.2", instanceId: "second"), gotSupportedVersion: true);

        action.Should().Throw<InvalidOperationException>();
    }

    private static TerminalTestReporter CreateReporter(CapturingTestConsole console) => new(console.Console, new TerminalTestReporterOptions
    {
        AnsiMode = AnsiMode.NoAnsi,
        ShowAssembly = true,
        ShowAssemblyStartAndComplete = true,
        ShowPassedTests = true,
    });

    private static TestApplicationHandler CreateHandler(TerminalTestReporter reporter, string? projectTarget) => new(
        reporter,
        new TestModule(new RunProperties("Tests.exe", null, null), null, projectTarget, true, null, "Tests.exe", null),
        new TestOptions(false, false, new Dictionary<string, string>()));

    private static HandshakeMessage CreateHandshake(
        string? targetFramework, string runtime = ".NET Framework 4.8.10008.0", string instanceId = "first")
    {
        var properties = new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.Architecture] = "X64",
            [HandshakeMessagePropertyNames.Framework] = runtime,
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = ProtocolConstants.SupportedVersions,
            [HandshakeMessagePropertyNames.HostType] = "TestHost",
            [HandshakeMessagePropertyNames.ExecutionId] = "execution",
            [HandshakeMessagePropertyNames.InstanceId] = instanceId,
        };
        if (targetFramework is not null)
        {
            properties[HandshakeMessagePropertyNames.TargetFramework] = targetFramework;
        }

        return new HandshakeMessage(properties);
    }

    private static TestResultMessages PassedResult(string instanceId) => new("execution", instanceId,
        [new SuccessfulTestResultMessage("one", "One", TestStates.Passed, null, null, null, null, null)], []);
}
