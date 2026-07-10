// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class TestApplicationHandlerTests : IDisposable
{
    // TerminalTestReporter is IDisposable and starts progress tracking via TestExecutionStarted
    // inside CreateHandler. MSTest instantiates the test class per test, so disposing this list in
    // Dispose() releases every reporter built for the current test.
    private readonly List<IDisposable> _disposables = new();

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// Regression test for the handler-level routing fix in https://github.com/dotnet/sdk/pull/52308
    /// (linked to https://github.com/dotnet/sdk/issues/51608). When the test host process exits
    /// after only a controller (non-"TestHost") handshake — i.e. <c>_receivedTestHostHandshake</c>
    /// is false — <see cref="TestApplicationHandler.OnTestProcessExited"/> must route to
    /// <c>HandshakeFailure</c> with the module's TargetPath and TargetFramework preserved, rather
    /// than to <c>AssemblyRunCompleted</c> (which would hit the defensive empty-path fallback in
    /// <c>TerminalTestReporter</c> and drop the assembly identifier from the output).
    /// </summary>
    [TestMethod]
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

    /// <summary>
    /// Backward-compat regression test. Older Microsoft.Testing.Platform versions don't include the
    /// optional <see cref="HandshakeMessagePropertyNames.ExecutionMode"/> property at all (added in
    /// https://github.com/microsoft/testfx/pull/8794). When the property is absent the SDK must keep
    /// today's behavior and not perform any execution-mode validation.
    /// </summary>
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void OnHandshakeReceived_WhenExecutionModePropertyIsAbsent_AcceptsHandshakeAndDoesNotReportFailure(bool isHelp, bool isDiscovery)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(isHelp: isHelp, isDiscovery: isDiscovery);

        var handshake = BuildHandshake(executionMode: null);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeTrue();
        reporter.HasHandshakeFailure.Should().BeFalse();
        console.GetOutput().Should().NotContain("MismatchingHandshakeExecutionMode");
    }

    [TestMethod]
    [DataRow(false, false, HandshakeMessageExecutionModes.Run)]
    [DataRow(true, false, HandshakeMessageExecutionModes.Help)]
    [DataRow(false, true, HandshakeMessageExecutionModes.Discover)]
    public void OnHandshakeReceived_WhenExecutionModeMatchesSdkExpectation_AcceptsHandshake(bool isHelp, bool isDiscovery, string executionMode)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(isHelp: isHelp, isDiscovery: isDiscovery);

        var handshake = BuildHandshake(executionMode);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeTrue();
        reporter.HasHandshakeFailure.Should().BeFalse();
    }

    /// <summary>
    /// Drives the new validation added in this change: if the host reports an execution mode that
    /// doesn't match what <c>dotnet test</c> intended (e.g. <c>RunArguments</c> or
    /// <c>launchSettings.json</c> injected a <c>--help</c> or <c>--list-tests</c> option), the SDK
    /// must reject the handshake at the protocol level and report the mismatch.
    /// </summary>
    [TestMethod]
    [DataRow(false, false, HandshakeMessageExecutionModes.Help, HandshakeMessageExecutionModes.Run)]
    [DataRow(false, false, HandshakeMessageExecutionModes.Discover, HandshakeMessageExecutionModes.Run)]
    [DataRow(true, false, HandshakeMessageExecutionModes.Run, HandshakeMessageExecutionModes.Help)]
    [DataRow(false, true, HandshakeMessageExecutionModes.Run, HandshakeMessageExecutionModes.Discover)]
    public void OnHandshakeReceived_WhenExecutionModeMismatch_RejectsHandshakeAndReportsFailure(
        bool isHelp, bool isDiscovery, string reportedMode, string expectedMode)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(isHelp: isHelp, isDiscovery: isDiscovery);

        var handshake = BuildHandshake(reportedMode);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeFalse();
        reporter.HasHandshakeFailure.Should().BeTrue();

        string rendered = console.GetOutput();
        rendered.Should().Contain(reportedMode);
        rendered.Should().Contain(expectedMode);
    }

    /// <summary>
    /// Even when the SDK itself was invoked in help mode, an explicit ExecutionMode mismatch reported
    /// by the test host is a protocol-level rejection and must still be surfaced. This pins down the
    /// <c>reportEvenWhenHelp: true</c> opt-out around the legacy "swallow handshake failures in help
    /// mode" workaround in <see cref="TerminalTestReporter.HandshakeFailure"/>.
    /// </summary>
    [TestMethod]
    public void OnHandshakeReceived_WhenSdkInHelpModeAndExecutionModeMismatch_StillReportsFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(isHelp: true, isDiscovery: false);

        var handshake = BuildHandshake(HandshakeMessageExecutionModes.Run);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeFalse();
        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    /// <summary>
    /// Every protocol-level rejection inside <c>OnHandshakeReceived</c> (not just ExecutionMode
    /// mismatch) opts out of the legacy "swallow handshake failures when SDK is in help mode"
    /// workaround. Missing-required-property is one such rejection — covered here. The workaround
    /// is intentionally scoped to <c>OnTestProcessExited</c> calling <c>HandshakeFailure</c>
    /// without ever having received a real handshake (older MTP behavior on <c>--help</c>).
    /// </summary>
    [TestMethod]
    public void OnHandshakeReceived_WhenSdkInHelpModeAndRequiredPropertyMissing_StillReportsFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(isHelp: true, isDiscovery: false);

        // Build a handshake that's missing the required ExecutionId property.
        var handshake = BuildHandshake(executionMode: null);
        var properties = handshake.Properties.Where(kvp => kvp.Key != HandshakeMessagePropertyNames.ExecutionId).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var incompleteHandshake = new HandshakeMessage(properties);

        bool accepted = handler.OnHandshakeReceived(incompleteHandshake, gotSupportedVersion: true);

        accepted.Should().BeFalse();
        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    /// <summary>
    /// When the protocol version itself is unsupported, that failure is reported and we exit early
    /// — we don't also report an ExecutionMode mismatch on the same handshake, since the version
    /// rejection makes any subsequent property validation moot (and it would be confusing to
    /// surface two distinct failures for one handshake).
    /// </summary>
    [TestMethod]
    public void OnHandshakeReceived_WhenUnsupportedProtocolVersion_DoesNotAlsoReportExecutionModeMismatch()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(isHelp: false, isDiscovery: false);

        // ExecutionMode is "help" which would mismatch (SDK expects "run") if we got that far.
        var handshake = BuildHandshake(HandshakeMessageExecutionModes.Help);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: false);

        accepted.Should().BeFalse();
        reporter.HasHandshakeFailure.Should().BeTrue();

        // The version-related failure IS reported and the ExecutionMode-related one is NOT —
        // both sides of the assertion matter: it should catch a regression that produces no
        // failure at all (no version error) as well as one that produces both.
        string rendered = console.GetOutput();
        rendered.Should().Contain("protocol version", "the unsupported-protocol-version failure is expected to be reported");
        rendered.Should().NotContain("execution mode", "the ExecutionMode validation should be skipped when the protocol version itself was rejected");
    }

    /// <summary>
    /// Forward-compat/protocol guard: a future testing-platform release that ships a new ExecutionMode
    /// value without bumping the protocol version, or a host that sends an empty value, is not silently
    /// accepted; we reject so we don't try to interpret a message stream we don't understand.
    /// </summary>
    [TestMethod]
    [DataRow("future-mode")]
    [DataRow("")]
    [DataRow("   ")]
    public void OnHandshakeReceived_WhenExecutionModeIsUnknownOrEmpty_RejectsHandshake(string executionMode)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(isHelp: false, isDiscovery: false);

        var handshake = BuildHandshake(executionMode: executionMode);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeFalse();
        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    /// <summary>
    /// A controller-host handshake (no <c>AssemblyRunStarted</c> bookkeeping) with a mismatched
    /// ExecutionMode must still be rejected — the validation lives in <c>OnHandshakeReceived</c>,
    /// not in a code path gated on <c>HostType == "TestHost"</c>.
    /// </summary>
    [TestMethod]
    public void OnHandshakeReceived_WhenControllerHostReportsMismatchedExecutionMode_RejectsHandshake()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(isHelp: false, isDiscovery: false);

        var handshake = BuildHandshake(
            executionMode: HandshakeMessageExecutionModes.Help,
            hostType: "TestHostController",
            includeInstanceId: false);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeFalse();
        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    /// <summary>
    /// Old-MTP help path: the test host exits without performing a handshake at all because older
    /// Microsoft.Testing.Platform versions don't handshake on <c>--help</c>. The SDK's existing
    /// workaround in <see cref="TerminalTestReporter.HandshakeFailure"/> must keep swallowing that
    /// case so we don't print a spurious failure for the legacy host. The new
    /// <c>reportEvenWhenHelp</c> opt-out must not break this — only the explicit protocol-level
    /// mismatch path opts in.
    /// </summary>
    [TestMethod]
    public void OnTestProcessExited_WhenSdkInHelpModeAndNoHandshakeReceived_DoesNotReportFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(isHelp: true, isDiscovery: false);

        Action act = () => handler.OnTestProcessExited(exitCode: 0, outputData: "help text", errorData: string.Empty);
        act.Should().NotThrow();

        reporter.HasHandshakeFailure.Should().BeFalse();
    }

    private const string TargetPath = "/repo/bin/Debug/net9.0/MyTest.dll";
    private const string ProjectPath = "/repo/MyTest.csproj";
    private const string TargetFramework = "net9.0";

    private (TestApplicationHandler Handler, TerminalTestReporter Reporter, CapturingConsole Console) CreateHandler(bool isHelp, bool isDiscovery)
    {
        var capturingConsole = new CapturingConsole();

        var reporterOptions = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        };

        var reporter = new TerminalTestReporter(capturingConsole, reporterOptions);
        _disposables.Add(reporter);

        // Mirror the test options on the reporter — TerminalTestReporter._isHelp / _isDiscovery are set
        // here, not in the constructor. The reporter's _isHelp flag drives the legacy "swallow handshake
        // failures when in help mode" path that the new reportEvenWhenHelp opt-out is meant to bypass.
        reporter.TestExecutionStarted(DateTimeOffset.UtcNow, workerCount: 1, isDiscovery: isDiscovery, isHelp: isHelp, isRetry: false);

        var module = new TestModule(
            RunProperties: new RunProperties("dotnet", TargetPath, "/repo"),
            ProjectFullPath: ProjectPath,
            TargetFramework: TargetFramework,
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: TargetPath,
            DotnetRootArchVariableName: null);

        var testOptions = new TestOptions(IsHelp: isHelp, IsDiscovery: isDiscovery, EnvironmentVariables: new Dictionary<string, string>());

        return (new TestApplicationHandler(reporter, module, testOptions), reporter, capturingConsole);
    }

    private static HandshakeMessage BuildHandshake(string? executionMode, string hostType = "TestHost", bool includeInstanceId = true)
    {
        var properties = new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.PID] = "1234",
            [HandshakeMessagePropertyNames.Architecture] = "x64",
            [HandshakeMessagePropertyNames.Framework] = ".NETCoreApp,Version=v9.0",
            [HandshakeMessagePropertyNames.OS] = "Windows",
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = ProtocolConstants.SupportedVersions,
            [HandshakeMessagePropertyNames.HostType] = hostType,
            [HandshakeMessagePropertyNames.ModulePath] = TargetPath,
            [HandshakeMessagePropertyNames.ExecutionId] = "exec-1",
        };

        if (includeInstanceId)
        {
            properties[HandshakeMessagePropertyNames.InstanceId] = "inst-1";
        }

        if (executionMode is not null)
        {
            properties[HandshakeMessagePropertyNames.ExecutionMode] = executionMode;
        }

        return new HandshakeMessage(properties);
    }
}
