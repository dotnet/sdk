// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

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

    [TestMethod]
    [DataRow("net472", null, ".NET Framework 4.8.1", "net472|x64, runtime: net481")]
    [DataRow("net472", "net481", ".NET Framework 4.8.1", "net472|x64, runtime: net481")]
    [DataRow("net481", null, ".NET Framework 4.8.1", "net481|x64")]
    [DataRow("net8.0", null, ".NET 8.0.25", "net8.0|x64")]
    [DataRow("net8.0-windows10.0.19041.0", null, ".NET 8.0.25", "net8.0-windows10.0.19041.0|x64")]
    [DataRow("net8.0", null, ".NET 9.0.3", "net8.0|x64, runtime: net9.0")]
    [DataRow(null, "net472", ".NET Framework 4.8.1", "net472|x64, runtime: net481")]
    [DataRow(null, null, ".NET 8.0.25", "x64, runtime: net8.0")]
    [DataRow(null, "", ".NET 8.0.25", "x64, runtime: net8.0")]
    [DataRow(null, " ", ".NET 8.0.25", "x64, runtime: net8.0")]
    [DataRow("net9.0", null, "Unknown runtime", "net9.0|x64, runtime: Unknown runtime")]
    public void OnHandshakeReceived_KeepsTargetAndRuntimeSeparateAcrossStartResultsAndCompletion(
        string? projectFramework, string? handshakeFramework, string runtimeDescription, string expectedIdentity)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false, isDiscovery: false, showAssembly: true, targetFramework: projectFramework);
        HandshakeMessage handshake = BuildHandshake(HandshakeMessageExecutionModes.Run);
        handshake.Properties[HandshakeMessagePropertyNames.Framework] = runtimeDescription;
        handshake.Properties[HandshakeMessagePropertyNames.Architecture] = "X64";
        if (handshakeFramework is not null)
        {
            handshake.Properties[HandshakeMessagePropertyNames.TargetFramework] = handshakeFramework;
        }

        handler.OnHandshakeReceived(handshake, gotSupportedVersion: true).Should().BeTrue();
        handler.OnTestResultsReceived(new TestResultMessages(
            "exec-1", "inst-1",
            [new SuccessfulTestResultMessage("passed", "Passed test", TestStates.Passed, null, null, null, null, "session")],
            [new FailedTestResultMessage("failed", "Failed test", TestStates.Failed,
                Duration: null, Reason: null, Exceptions: null, StandardOutput: null, ErrorOutput: null,
                SessionUid: "session", Expected: null, Actual: null)]));
        handler.OnTestProcessExited(TestExitCode.AtLeastOneTestFailed, string.Empty, string.Empty);

        string output = console.GetOutput();
        output.Split($"({expectedIdentity})", StringSplitOptions.None).Should().HaveCount(5,
            "the start, both test results, and completion must use the same target/process identity");
        reporter.TotalTests.Should().Be(2);
        reporter.HasHandshakeFailure.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void OnHandshakeReceived_OptionalTargetDoesNotReplaceRequiredRuntime(string? runtimeDescription)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, targetFramework: null);
        HandshakeMessage handshake = BuildHandshake(HandshakeMessageExecutionModes.Run);
        handshake.Properties[HandshakeMessagePropertyNames.TargetFramework] = "net472";
        if (runtimeDescription is null)
        {
            handshake.Properties.Remove(HandshakeMessagePropertyNames.Framework);
        }
        else
        {
            handshake.Properties[HandshakeMessagePropertyNames.Framework] = runtimeDescription;
        }

        handler.OnHandshakeReceived(handshake, gotSupportedVersion: true).Should().BeFalse();

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    public void OnHandshakeReceived_OptionalTargetMayFollowAControllerWithoutMetadata()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false, isDiscovery: false, showAssembly: true, targetFramework: null);
        HandshakeMessage controller = BuildHandshake(
            HandshakeMessageExecutionModes.Run, hostType: HandshakeMessageHostTypes.TestHostOrchestrator);
        controller.Properties[HandshakeMessagePropertyNames.Framework] = ".NET Framework 4.8.1";
        handler.OnHandshakeReceived(controller, gotSupportedVersion: true).Should().BeTrue();
        HandshakeMessage host = BuildHandshake(HandshakeMessageExecutionModes.Run);
        host.Properties[HandshakeMessagePropertyNames.Framework] = ".NET Framework 4.8.1";
        host.Properties[HandshakeMessagePropertyNames.TargetFramework] = "net472";

        handler.OnHandshakeReceived(host, gotSupportedVersion: true).Should().BeTrue();
        handler.OnTestProcessExited(TestExitCode.Success, string.Empty, string.Empty);

        console.GetOutput().Should().Contain("(net472|x64, runtime: net481)");
        reporter.HasHandshakeFailure.Should().BeFalse();
    }

    [TestMethod]
    public void OnHandshakeReceived_LateOptionalTargetKeepsRetryIdentityConsistent()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false, isDiscovery: false, showAssembly: true, targetFramework: null);
        HandshakeMessage handshake = BuildHandshake(HandshakeMessageExecutionModes.Run);
        handshake.Properties[HandshakeMessagePropertyNames.Framework] = ".NET 8.0.25";
        handler.OnHandshakeReceived(handshake, gotSupportedVersion: true).Should().BeTrue();
        handshake.Properties[HandshakeMessagePropertyNames.InstanceId] = "second";
        handshake.Properties[HandshakeMessagePropertyNames.TargetFramework] = "net8.0";

        handler.OnHandshakeReceived(handshake, gotSupportedVersion: true).Should().BeTrue();
        handler.OnTestProcessExited(TestExitCode.Success, string.Empty, string.Empty);

        string output = console.GetOutput();
        output.Split("(x64, runtime: net8.0)", StringSplitOptions.None).Should().HaveCount(4);
        output.Should().NotContain("(net8.0|x64)");
        reporter.HasHandshakeFailure.Should().BeFalse();
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
            DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string>());

        var testOptions = new TestOptions(IsHelp: false, IsDiscovery: false, ListTestsFormat: TestListFormat.Text);

        var handler = new TestApplicationHandler(reporter, module, testOptions);

        // Controller-only handshake — hostType is not "TestHost", so _receivedTestHostHandshake stays false
        // and AssemblyRunStarted is never called (so the executionId is never registered in _assemblies).
        var handshake = new HandshakeMessage(new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.PID] = "1234",
            [HandshakeMessagePropertyNames.Architecture] = "x64",
            [HandshakeMessagePropertyNames.Framework] = ".NET 9.0.0",
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

    [TestMethod]
    public void OnHandshakeReceived_WithExplicitAttemptNumber_UsesReportedAttempt()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            showAssembly: true);

        var handshake = BuildHandshake(
            executionMode: HandshakeMessageExecutionModes.Run,
            attemptNumber: 3);

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeTrue();
        reporter.HasHandshakeFailure.Should().BeFalse();
        console.GetOutput().Should().Contain("(try 3)");
    }

    [TestMethod]
    public void OnHandshakeReceived_WithRetryOrchestrator_EnablesRetryBeforeFirstTestHost()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            showAssembly: true);

        bool orchestratorAccepted = handler.OnHandshakeReceived(
            BuildHandshake(
                executionMode: HandshakeMessageExecutionModes.Run,
                hostType: HandshakeMessageHostTypes.TestHostOrchestrator,
                orchestratorFeature: "RetryOrchestrator"),
            gotSupportedVersion: true);
        bool testHostAccepted = handler.OnHandshakeReceived(
            BuildHandshake(
                executionMode: HandshakeMessageExecutionModes.Run,
                attemptNumber: 1),
            gotSupportedVersion: true);

        orchestratorAccepted.Should().BeTrue();
        testHostAccepted.Should().BeTrue();
        reporter.HasHandshakeFailure.Should().BeFalse();
        console.GetOutput().Should().Contain("(try 1)");
    }

    [TestMethod]
    public void OnHandshakeReceived_WithUnknownOrchestrator_AcceptsWithoutEnablingRetry()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            showAssembly: true);

        bool orchestratorAccepted = handler.OnHandshakeReceived(
            BuildHandshake(
                executionMode: HandshakeMessageExecutionModes.Run,
                hostType: HandshakeMessageHostTypes.TestHostOrchestrator,
                orchestratorFeature: "FutureOrchestrator"),
            gotSupportedVersion: true);
        bool testHostAccepted = handler.OnHandshakeReceived(
            BuildHandshake(
                executionMode: HandshakeMessageExecutionModes.Run,
                attemptNumber: 1),
            gotSupportedVersion: true);

        orchestratorAccepted.Should().BeTrue();
        testHostAccepted.Should().BeTrue();
        reporter.HasHandshakeFailure.Should().BeFalse();
        console.GetOutput().Should().NotContain("(try 1)");
    }

    [TestMethod]
    public void OnHandshakeReceived_WithArtifactPostProcessingCapabilities_RecordsApplication()
    {
        var manager = new ArtifactPostProcessingManager();
        (TestApplicationHandler handler, _, _) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            artifactPostProcessingManager: manager);
        var handshake = BuildHandshake(
            executionMode: HandshakeMessageExecutionModes.Run,
            supportedPostProcessorKinds: "microsoft.testing.trx;example.junit",
            supportedPostProcessorExtensions: ".trx;.xml");

        bool accepted = handler.OnHandshakeReceived(handshake, gotSupportedVersion: true);

        accepted.Should().BeTrue();
        ArtifactPostProcessingApplication application = manager.SnapshotApplications().Should().ContainSingle().Subject;
        application.SupportedKinds.Should().BeEquivalentTo("microsoft.testing.trx", "example.junit");
        application.SupportedExtensions.Should().BeEquivalentTo(".trx", ".xml");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OnHandshakeReceived_LaterHostTargetEnrichesControllerCapabilities(bool hostAdvertisesCapabilities)
    {
        var manager = new ArtifactPostProcessingManager();
        (TestApplicationHandler handler, _, _) = CreateHandler(
            isHelp: false, isDiscovery: false, artifactPostProcessingManager: manager, targetFramework: null);
        HandshakeMessage controller = BuildHandshake(
            HandshakeMessageExecutionModes.Run,
            hostType: "TestHostController",
            includeInstanceId: false,
            supportedPostProcessorKinds: "microsoft.testing.trx");
        controller.Properties[HandshakeMessagePropertyNames.Framework] = ".NET Framework 4.8.1";
        handler.OnHandshakeReceived(controller, gotSupportedVersion: true).Should().BeTrue();
        manager.SnapshotApplications().Should().ContainSingle().Which.TargetFramework.Should().BeNull();

        HandshakeMessage host = BuildHandshake(
            HandshakeMessageExecutionModes.Run,
            supportedPostProcessorKinds: hostAdvertisesCapabilities ? "example.junit" : null);
        host.Properties[HandshakeMessagePropertyNames.Framework] = ".NET Framework 4.8.1";
        host.Properties[HandshakeMessagePropertyNames.TargetFramework] = "net472";

        handler.OnHandshakeReceived(host, gotSupportedVersion: true).Should().BeTrue();

        ArtifactPostProcessingApplication application = manager.SnapshotApplications().Should().ContainSingle().Subject;
        application.TargetFramework.Should().Be("net472");
        string[] expectedKinds = hostAdvertisesCapabilities ? ["microsoft.testing.trx", "example.junit"] : ["microsoft.testing.trx"];
        application.SupportedKinds.Should().BeEquivalentTo(expectedKinds);
    }

    [TestMethod]
    [DataRow("net472", null, "net472")]
    [DataRow(null, "net472", "net472")]
    [DataRow(null, null, null)]
    public void OnFileArtifactsReceived_UsesOnlyKnownTargetMetadata(
        string? projectFramework, string? handshakeFramework, string? expectedFramework)
    {
        var manager = new ArtifactPostProcessingManager();
        (TestApplicationHandler handler, _, _) = CreateHandler(
            isHelp: false, isDiscovery: false, artifactPostProcessingManager: manager, targetFramework: projectFramework);
        HandshakeMessage handshake = BuildHandshake(
            HandshakeMessageExecutionModes.Run, supportedPostProcessorKinds: "microsoft.testing.trx");
        handshake.Properties[HandshakeMessagePropertyNames.Framework] = ".NET Framework 4.8.1";
        if (handshakeFramework is not null)
        {
            handshake.Properties[HandshakeMessagePropertyNames.TargetFramework] = handshakeFramework;
        }

        handler.OnHandshakeReceived(handshake, gotSupportedVersion: true).Should().BeTrue();
        handler.OnFileArtifactsReceived(new FileArtifactMessages(
            "exec-1", "inst-1",
            [new FileArtifactMessage(Path.GetFullPath("result.trx"), "TRX", null, null, null, null, "microsoft.testing.trx")]));

        manager.SnapshotApplications().Should().ContainSingle().Which.TargetFramework.Should().Be(expectedFramework);
        manager.SnapshotArtifacts().Should().ContainSingle().Which.TargetFramework.Should().Be(expectedFramework);
    }

    [TestMethod]
    public void OnFileArtifactsReceived_RecordsArtifactMetadata()
    {
        var manager = new ArtifactPostProcessingManager();
        (TestApplicationHandler handler, _, _) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            artifactPostProcessingManager: manager);
        handler.OnHandshakeReceived(
            BuildHandshake(HandshakeMessageExecutionModes.Run),
            gotSupportedVersion: true).Should().BeTrue();
        string artifactPath = Path.GetFullPath("result.trx");

        handler.OnFileArtifactsReceived(new FileArtifactMessages(
            "exec-1",
            "inst-1",
            [new FileArtifactMessage(artifactPath, "TRX", null, null, null, null, "microsoft.testing.trx")]));

        ArtifactPostProcessingArtifact artifact = manager.SnapshotArtifacts().Should().ContainSingle().Subject;
        artifact.Path.Should().Be(artifactPath);
        artifact.Kind.Should().Be("microsoft.testing.trx");
        artifact.ProducingTestModule.Should().Be(TargetPath);
        artifact.TargetFramework.Should().Be(TargetFramework);
        artifact.Architecture.Should().Be("x64");
        artifact.ExecutionId.Should().Be("exec-1");
    }

    [TestMethod]
    public void OnHandshakeReceived_WhenArtifactPostProcessorHandshakeFails_DoesNotFailTestRun()
    {
        var invocation = new ArtifactPostProcessingInvocation("manifest.json");
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            artifactPostProcessingInvocation: invocation);

        bool accepted = handler.OnHandshakeReceived(
            BuildHandshake(
                HandshakeMessageExecutionModes.Tool,
                hostType: HandshakeMessageHostTypes.TestHost),
            gotSupportedVersion: true);

        accepted.Should().BeFalse();
        invocation.FailureMessage.Should().NotBeNullOrEmpty();
        reporter.HasHandshakeFailure.Should().BeFalse(
            "post-processing failures must not change the test run exit code");
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

    [TestMethod]
    public void OnTestProcessExited_WhenSuccessfulTestMapCollectionSuppressesReporting_DoesNotReportFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            collectTestMap: true);

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeFalse();
    }

    [TestMethod]
    public void OnTestProcessExited_WhenTestMapCollectionFailsWithoutHandshake_ReportsFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            collectTestMap: true);

        handler.OnTestProcessExited(exitCode: 1, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    public void OnTestProcessExited_WhenTestMapCollectionReceivesOnlyControllerHandshake_ReportsFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            collectTestMap: true);

        handler.OnHandshakeReceived(
            BuildHandshake(
                executionMode: HandshakeMessageExecutionModes.Run,
                hostType: "TestHostController",
                includeInstanceId: false),
            gotSupportedVersion: true).Should().BeTrue();

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true, "RefreshMappingsOrchestrator", HandshakeMessageExecutionModes.Run)]
    [DataRow(false, "RunAffectedTestsOrchestrator", HandshakeMessageExecutionModes.Run)]
    [DataRow(true, "RefreshMappingsOrchestrator", null)]
    [DataRow(false, "RunAffectedTestsOrchestrator", null)]
    public void OnTestProcessExited_WhenMatchingAffectedTestsOrchestratorSucceeds_DoesNotReportFailure(
        bool collectTestMap, string feature, string? executionMode)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: !collectTestMap);

        handler.OnHandshakeReceived(
            BuildHandshake(executionMode, HandshakeMessageHostTypes.TestHostOrchestrator, orchestratorFeature: feature),
            gotSupportedVersion: true).Should().BeTrue();

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeFalse();
        reporter.TotalTests.Should().Be(0);
    }

    [TestMethod]
    [DataRow(true, false, "RunAffectedTestsOrchestrator")]
    [DataRow(false, true, "RefreshMappingsOrchestrator")]
    [DataRow(false, false, "RefreshMappingsOrchestrator")]
    [DataRow(false, false, "RunAffectedTestsOrchestrator")]
    [DataRow(true, false, "RetryOrchestrator")]
    [DataRow(false, true, "FutureOrchestrator")]
    [DataRow(true, false, null)]
    [DataRow(false, true, "")]
    public void OnTestProcessExited_WhenOrchestratorDoesNotMatchAffectedTestsMode_ReportsFailure(
        bool collectTestMap, bool affectedTests, string? feature)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: affectedTests);

        handler.OnHandshakeReceived(
            BuildHandshake(HandshakeMessageExecutionModes.Run, HandshakeMessageHostTypes.TestHostOrchestrator, orchestratorFeature: feature),
            gotSupportedVersion: true).Should().BeTrue();

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true, 1)]
    [DataRow(false, 1)]
    [DataRow(true, 8)]
    [DataRow(false, 8)]
    [DataRow(true, 13)]
    [DataRow(false, 13)]
    public void OnTestProcessExited_WhenAffectedTestsOrchestratorFails_ReportsFailure(bool collectTestMap, int exitCode)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: !collectTestMap);

        handler.OnHandshakeReceived(
            BuildHandshake(
                HandshakeMessageExecutionModes.Run,
                HandshakeMessageHostTypes.TestHostOrchestrator,
                orchestratorFeature: collectTestMap ? "RefreshMappingsOrchestrator" : "RunAffectedTestsOrchestrator"),
            gotSupportedVersion: true).Should().BeTrue();

        handler.OnTestProcessExited(exitCode, outputData: "orchestrator stdout", errorData: "orchestrator stderr");

        reporter.HasHandshakeFailure.Should().BeTrue();
        console.GetOutput().Should().Contain(TargetPath).And.Contain("orchestrator stdout").And.Contain("orchestrator stderr");
    }

    [TestMethod]
    [DataRow(true, true, "TestHostController", "RefreshMappingsOrchestrator")]
    [DataRow(true, false, "TestHostController", "RefreshMappingsOrchestrator")]
    [DataRow(false, true, "TestHostController", "RunAffectedTestsOrchestrator")]
    [DataRow(false, false, "TestHostController", "RunAffectedTestsOrchestrator")]
    [DataRow(true, true, HandshakeMessageHostTypes.TestHostOrchestrator, "FutureOrchestrator")]
    [DataRow(true, false, HandshakeMessageHostTypes.TestHostOrchestrator, "FutureOrchestrator")]
    [DataRow(false, true, HandshakeMessageHostTypes.TestHostOrchestrator, "FutureOrchestrator")]
    [DataRow(false, false, HandshakeMessageHostTypes.TestHostOrchestrator, "FutureOrchestrator")]
    public void OnTestProcessExited_WhenAffectedTestsOrchestratorAndPartialHandshake_ReportsFailure(
        bool collectTestMap, bool partialHandshakeFirst, string hostType, string feature)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: !collectTestMap);
        HandshakeMessage partial = BuildHandshake(HandshakeMessageExecutionModes.Run, hostType, orchestratorFeature: feature);
        HandshakeMessage orchestrator = BuildHandshake(
            HandshakeMessageExecutionModes.Run,
            HandshakeMessageHostTypes.TestHostOrchestrator,
            orchestratorFeature: collectTestMap ? "RefreshMappingsOrchestrator" : "RunAffectedTestsOrchestrator");

        handler.OnHandshakeReceived(partialHandshakeFirst ? partial : orchestrator, gotSupportedVersion: true).Should().BeTrue();
        handler.OnHandshakeReceived(partialHandshakeFirst ? orchestrator : partial, gotSupportedVersion: true).Should().BeTrue();
        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true, true, "protocol")]
    [DataRow(true, false, "protocol")]
    [DataRow(false, true, "protocol")]
    [DataRow(false, false, "protocol")]
    [DataRow(true, true, "property")]
    [DataRow(true, false, "property")]
    [DataRow(false, true, "property")]
    [DataRow(false, false, "property")]
    [DataRow(true, true, "mode")]
    [DataRow(true, false, "mode")]
    [DataRow(false, true, "mode")]
    [DataRow(false, false, "mode")]
    public void OnTestProcessExited_WhenAffectedTestsOrchestratorAndRejectedHandshake_StillReportsFailure(
        bool collectTestMap, bool rejectionFirst, string rejection)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: !collectTestMap);
        HandshakeMessage orchestrator = BuildHandshake(
            HandshakeMessageExecutionModes.Run,
            HandshakeMessageHostTypes.TestHostOrchestrator,
            orchestratorFeature: collectTestMap ? "RefreshMappingsOrchestrator" : "RunAffectedTestsOrchestrator");
        var properties = new Dictionary<byte, string>(orchestrator.Properties);
        if (rejection == "property")
        {
            properties.Remove(HandshakeMessagePropertyNames.Framework);
        }
        else if (rejection == "mode")
        {
            properties[HandshakeMessagePropertyNames.ExecutionMode] = HandshakeMessageExecutionModes.Discover;
        }

        var rejected = new HandshakeMessage(properties);
        if (!rejectionFirst)
        {
            handler.OnHandshakeReceived(orchestrator, gotSupportedVersion: true).Should().BeTrue();
        }

        handler.OnHandshakeReceived(rejected, gotSupportedVersion: rejection != "protocol").Should().BeFalse();
        if (rejectionFirst)
        {
            handler.OnHandshakeReceived(orchestrator, gotSupportedVersion: true).Should().BeTrue();
        }

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    public void OnTestProcessExited_WhenAffectedTestsHaveNoHandshake_ReportsFailure()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, affectedTests: true);

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void OnTestProcessExited_WhenAffectedTestsStartTestHost_UsesOrdinaryCompletion(bool collectTestMap)
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: !collectTestMap);
        handler.OnHandshakeReceived(
            BuildHandshake(
                HandshakeMessageExecutionModes.Run,
                HandshakeMessageHostTypes.TestHostOrchestrator,
                orchestratorFeature: collectTestMap ? "RefreshMappingsOrchestrator" : "RunAffectedTestsOrchestrator"),
            gotSupportedVersion: true).Should().BeTrue();
        handler.OnHandshakeReceived(BuildHandshake(HandshakeMessageExecutionModes.Run), gotSupportedVersion: true).Should().BeTrue();

        handler.OnTestProcessExited(exitCode: 1, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void OnTestProcessExited_WhenSuccessfulAffectedModuleFollowsFailedModule_PreservesAggregateFailure(bool collectTestMap)
    {
        (TestApplicationHandler failedHandler, TerminalTestReporter reporter, _) = CreateHandler(
            isHelp: false, isDiscovery: false, collectTestMap: collectTestMap, affectedTests: !collectTestMap);
        var successfulModule = new TestModule(
            RunProperties: new RunProperties("dotnet", "/repo/OtherTest.dll", "/repo"),
            ProjectFullPath: "/repo/OtherTest.csproj",
            TargetFramework: TargetFramework,
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: "/repo/OtherTest.dll",
            DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string>());
        var successfulHandler = new TestApplicationHandler(
            reporter,
            successfulModule,
            new TestOptions(false, false, TestListFormat.Text) { CollectTestMap = collectTestMap, AffectedTests = !collectTestMap });

        failedHandler.OnHandshakeReceived(
            BuildHandshake(HandshakeMessageExecutionModes.Run, "TestHostController"), gotSupportedVersion: true).Should().BeTrue();
        failedHandler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);
        successfulHandler.OnHandshakeReceived(
            BuildHandshake(
                HandshakeMessageExecutionModes.Run,
                HandshakeMessageHostTypes.TestHostOrchestrator,
                orchestratorFeature: collectTestMap ? "RefreshMappingsOrchestrator" : "RunAffectedTestsOrchestrator"),
            gotSupportedVersion: true).Should().BeTrue();
        successfulHandler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();
    }

    /// <summary>
    /// Related to https://github.com/dotnet/sdk/issues/55549. Output already streamed to the terminal is
    /// reported as nothing left to show (<c>ProcessOutputCollector.GetOutputToReport</c> returns empty),
    /// and the handler must then render no output block at all rather than an empty heading.
    /// </summary>
    [TestMethod]
    public void OnTestProcessExited_WithNoOutputToReport_RendersNoOutputBlockInTheExitCodeSummary()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(isHelp: false, isDiscovery: false);

        handler.OnHandshakeReceived(BuildHandshake(executionMode: HandshakeMessageExecutionModes.Run), gotSupportedVersion: true)
            .Should().BeTrue();

        handler.OnTestProcessExited(exitCode: 1, outputData: string.Empty, errorData: string.Empty);

        // The run went through the normal completion summary rather than the handshake-failure path.
        reporter.HasHandshakeFailure.Should().BeFalse();

        string rendered = console.GetOutput();
        rendered.Should().NotContain(CliCommandStrings.StandardOutput);
        rendered.Should().NotContain(CliCommandStrings.StandardError);
    }

    /// <summary>
    /// The other side of <see cref="OnTestProcessExited_WithNoOutputToReport_RendersNoOutputBlockInTheExitCodeSummary"/>:
    /// hosts that never stream (Microsoft.Testing.Platform versions below protocol 1.1.0) rely on the
    /// summary as the only place their output is shown, so it must still be printed there.
    /// </summary>
    [TestMethod]
    public void OnTestProcessExited_WithOutputToReport_StillReportsItInTheExitCodeSummary()
    {
        (TestApplicationHandler handler, _, CapturingConsole console) = CreateHandler(isHelp: false, isDiscovery: false);

        handler.OnHandshakeReceived(BuildHandshake(executionMode: HandshakeMessageExecutionModes.Run), gotSupportedVersion: true)
            .Should().BeTrue();

        handler.OnTestProcessExited(
            exitCode: 1,
            outputData: "buffered-stdout-line",
            errorData: "buffered-stderr-line");

        string rendered = console.GetOutput();
        rendered.Should().Contain("buffered-stdout-line");
        rendered.Should().Contain("buffered-stderr-line");
    }

    /// <summary>
    /// Suppressing already-streamed output must not cost the failing assembly's identity: the
    /// handshake-failure report is what tells the user which module produced the failure.
    /// </summary>
    [TestMethod]
    public void OnTestProcessExited_WithNoOutputToReportAndHandshakeFailed_StillReportsTheAssembly()
    {
        (TestApplicationHandler handler, TerminalTestReporter reporter, CapturingConsole console) = CreateHandler(isHelp: false, isDiscovery: false);

        handler.OnTestProcessExited(exitCode: 1, outputData: string.Empty, errorData: string.Empty);

        reporter.HasHandshakeFailure.Should().BeTrue();

        string rendered = console.GetOutput();
        rendered.Should().Contain(TargetPath, "the failing assembly still has to be identified");
        rendered.Should().Contain(TargetFramework);
    }

    /// <summary>
    /// Artifact post-processing writes the reported output straight back to the terminal instead of going
    /// through a summary. It is not gated on the exit code, so it has to honour "nothing to report" too.
    /// </summary>
    [TestMethod]
    public void OnTestProcessExited_WithNoOutputToReportDuringArtifactPostProcessing_WritesNothing()
    {
        (TestApplicationHandler handler, _, CapturingConsole console) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            artifactPostProcessingInvocation: new ArtifactPostProcessingInvocation("manifest.json"));

        handler.OnTestProcessExited(exitCode: 0, outputData: string.Empty, errorData: string.Empty);

        console.GetOutput().Should().BeEmpty();
    }

    /// <summary>
    /// The other side of the artifact post-processing path: output that was never streamed still has to
    /// reach the terminal there.
    /// </summary>
    [TestMethod]
    public void OnTestProcessExited_WithOutputToReportDuringArtifactPostProcessing_WritesIt()
    {
        (TestApplicationHandler handler, _, CapturingConsole console) = CreateHandler(
            isHelp: false,
            isDiscovery: false,
            artifactPostProcessingInvocation: new ArtifactPostProcessingInvocation("manifest.json"));

        handler.OnTestProcessExited(
            exitCode: 0,
            outputData: "buffered-stdout-line",
            errorData: "buffered-stderr-line");

        console.GetOutput().Should().Contain("buffered-stdout-line").And.Contain("buffered-stderr-line");
    }

    private const string TargetPath = "/repo/bin/Debug/net9.0/MyTest.dll";
    private const string ProjectPath = "/repo/MyTest.csproj";
    private const string TargetFramework = "net9.0";

    private (TestApplicationHandler Handler, TerminalTestReporter Reporter, CapturingConsole Console) CreateHandler(
        bool isHelp,
        bool isDiscovery,
        bool showAssembly = false,
        ArtifactPostProcessingManager? artifactPostProcessingManager = null,
        ArtifactPostProcessingInvocation? artifactPostProcessingInvocation = null,
        bool collectTestMap = false,
        bool affectedTests = false,
        string? targetFramework = TargetFramework)
    {
        var capturingConsole = new CapturingConsole();

        var reporterOptions = new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
            ShowAssembly = showAssembly,
            ShowAssemblyStartAndComplete = showAssembly,
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
            TargetFramework: targetFramework,
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: TargetPath,
            DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string>());

        var testOptions = new TestOptions(
            IsHelp: isHelp,
            IsDiscovery: isDiscovery,
            ListTestsFormat: TestListFormat.Text,
            IsArtifactPostProcessing: artifactPostProcessingInvocation is not null)
        {
            CollectTestMap = collectTestMap,
            AffectedTests = affectedTests,
        };

        return (
            new TestApplicationHandler(
                reporter,
                module,
                testOptions,
                artifactPostProcessingManager,
                artifactPostProcessingInvocation),
            reporter,
            capturingConsole);
    }

    private static HandshakeMessage BuildHandshake(
        string? executionMode,
        string hostType = "TestHost",
        bool includeInstanceId = true,
        int? attemptNumber = null,
        string? orchestratorFeature = null,
        string? supportedPostProcessorKinds = null,
        string? supportedPostProcessorExtensions = null)
    {
        var properties = new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.PID] = "1234",
            [HandshakeMessagePropertyNames.Architecture] = "x64",
            [HandshakeMessagePropertyNames.Framework] = ".NET 9.0.0",
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

        if (attemptNumber.HasValue)
        {
            properties[HandshakeMessagePropertyNames.AttemptNumber] = attemptNumber.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (orchestratorFeature is not null)
        {
            properties[HandshakeMessagePropertyNames.OrchestratorFeature] = orchestratorFeature;
        }

        if (supportedPostProcessorKinds is not null)
        {
            properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds] = supportedPostProcessorKinds;
        }

        if (supportedPostProcessorExtensions is not null)
        {
            properties[HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy] = supportedPostProcessorExtensions;
        }

        return new HandshakeMessage(properties);
    }
}
