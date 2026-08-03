// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using TestExitCode = Microsoft.DotNet.Cli.Commands.Test.ExitCode;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class ArtifactPostProcessingManagerTests
{
    [TestMethod]
    public void ApplyOutputs_MatchingKind_ReplacesOriginalArtifacts()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingArtifact first = CreateArtifact("first.trx", "microsoft.testing.trx");
        ArtifactPostProcessingArtifact second = CreateArtifact("second.trx", "microsoft.testing.trx");
        ArtifactPostProcessingApplication application = CreateApplication();
        var group = new ArtifactPostProcessingGroup(
            "microsoft.testing.trx",
            IsKind: true,
            [first, second],
            [application]);
        var job = new ArtifactPostProcessingJob(application, [group]);
        reporter.ArtifactAdded(false, "A.dll", "net10.0", "x64", "execution-1", null, first.Path);
        reporter.ArtifactAdded(false, "B.dll", "net10.0", "x64", "execution-2", null, second.Path);
        ArtifactPostProcessingArtifact merged = CreateArtifact("merged.trx", "microsoft.testing.trx");

        ArtifactPostProcessingManager.ApplyOutputs(reporter, job, [merged]);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("merged.trx");
        output.Should().NotContain("first.trx");
        output.Should().NotContain("second.trx");
    }

    [TestMethod]
    public void ApplyOutputs_UnmatchedOutput_StillReportsOutputAndPreservesOriginals()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingArtifact first = CreateArtifact("first.coverage", "microsoft.codecoverage");
        ArtifactPostProcessingArtifact second = CreateArtifact("second.coverage", "microsoft.codecoverage");
        ArtifactPostProcessingApplication application = CreateApplication();
        var group = new ArtifactPostProcessingGroup(
            "microsoft.codecoverage",
            IsKind: true,
            [first, second],
            [application]);
        var job = new ArtifactPostProcessingJob(application, [group]);
        reporter.ArtifactAdded(false, "A.dll", "net10.0", "x64", "execution-1", null, first.Path);
        reporter.ArtifactAdded(false, "B.dll", "net10.0", "x64", "execution-2", null, second.Path);
        ArtifactPostProcessingArtifact converted = CreateArtifact("coverage.cobertura.xml", "cobertura");

        ArtifactPostProcessingManager.ApplyOutputs(reporter, job, [converted]);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("coverage.cobertura.xml");
        output.Should().Contain("first.coverage");
        output.Should().Contain("second.coverage");
    }

    [TestMethod]
    public void ApplyOutputs_KindOutput_AlsoConsumesLegacyInputsWithSameExtension()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingArtifact taggedFirst = CreateArtifact("tagged-first.xml", "example.junit");
        ArtifactPostProcessingArtifact taggedSecond = CreateArtifact("tagged-second.xml", "example.junit");
        ArtifactPostProcessingArtifact legacyFirst = CreateArtifact("legacy-first.xml", kind: null);
        ArtifactPostProcessingArtifact legacySecond = CreateArtifact("legacy-second.xml", kind: null);
        ArtifactPostProcessingApplication application = CreateApplication();
        var taggedGroup = new ArtifactPostProcessingGroup(
            "example.junit",
            IsKind: true,
            [taggedFirst, taggedSecond],
            [application]);
        var fallbackGroup = new ArtifactPostProcessingGroup(
            ".xml",
            IsKind: false,
            [legacyFirst, legacySecond],
            [application]);
        var job = new ArtifactPostProcessingJob(application, [taggedGroup, fallbackGroup]);
        foreach (ArtifactPostProcessingArtifact artifact in taggedGroup.Artifacts.Concat(fallbackGroup.Artifacts))
        {
            reporter.ArtifactAdded(false, "A.dll", "net10.0", "x64", artifact.ExecutionId, null, artifact.Path);
        }

        ArtifactPostProcessingManager.ApplyOutputs(
            reporter,
            job,
            [CreateArtifact("merged.xml", "example.junit")]);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        string output = console.GetOutput();
        output.Should().Contain("merged.xml");
        output.Should().NotContain("tagged-first.xml");
        output.Should().NotContain("tagged-second.xml");
        output.Should().NotContain("legacy-first.xml");
        output.Should().NotContain("legacy-second.xml");
    }

    [TestMethod]
    public void GetArtifactPostProcessingLaunchArguments_DotnetCommand_UsesOnlyExecAndTargetPath()
    {
        ArtifactPostProcessingApplication application = CreateApplication();

        string arguments = TestApplication.GetArtifactPostProcessingLaunchArguments(application.Module);

        arguments.Should().Be("exec A.dll");
    }

    [TestMethod]
    public void GetArtifactPostProcessingLaunchArguments_AppHost_UsesNoTestArguments()
    {
        ArtifactPostProcessingApplication application = CreateApplication();
        TestModule appHostModule = application.Module with
        {
            RunProperties = new RunProperties("A.exe", "--filter injected", null),
        };

        string arguments = TestApplication.GetArtifactPostProcessingLaunchArguments(appHostModule);

        arguments.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenJobFailsUnexpectedly_ReportsWarningWithoutThrowing()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        // A NUL character makes Path.GetFullPath throw ArgumentException while planning the job.
        // ArgumentException is outside the exception set this code used to catch, so before the
        // catch-all it escaped ExecuteAsync and crashed a 'dotnet test' run that had already
        // completed, replacing its exit code.
        ArtifactPostProcessingManager manager = CreateManagerWithMergeableArtifacts("first\0.trx", "second\0.trx");
        using var ctrlC = CreateCancellationManager();

        await manager.ExecuteAsync(CreateBuildOptions(), reporter, ctrlC);
        reporter.TestExecutionCompleted(DateTimeOffset.UtcNow, TestExitCode.Success);

        console.GetOutput().Should().Contain(
            FormatPrefix(CliCommandStrings.ArtifactPostProcessingFailed, "A.dll"),
            "the failure must degrade to a warning instead of escaping");
    }

    /// <summary>
    /// Formats a two-placeholder message up to its second placeholder, so an assertion can match the
    /// reported message without depending on the exception text that fills the placeholder.
    /// </summary>
    private static string FormatPrefix(string format, string firstArgument)
        => string.Format(format, firstArgument, "\u0001").Split('\u0001')[0];

    [TestMethod]
    public async Task ExecuteAsync_WhenCancelledBeforeStarting_RunsNoJobs()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingManager manager = CreateManagerWithMergeableArtifacts("first.trx", "second.trx");
        using var ctrlC = CreateCancellationManager();
        ctrlC.SimulateCtrlC();
        // Running a job creates its results directory before anything else is attempted, so the
        // directory staying absent is the observable proof that no job ran. Asserting on the absence
        // of a warning would not be: a job that ran and failed is silent too, because the failure of
        // a cancelled run is deliberately not reported.
        string resultsDirectory = Path.Combine(
            Path.GetTempPath(),
            $"dotnet-test-postproc-tests-{Guid.NewGuid():N}");

        try
        {
            await manager.ExecuteAsync(CreateBuildOptions(resultsDirectory), reporter, ctrlC);

            Directory.Exists(resultsDirectory).Should().BeFalse(
                "a cancelled run must not start the jobs it planned");
        }
        finally
        {
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void ReportFailureUnlessCancelled_WhenNotCancelled_WritesWarning()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        using var ctrlC = CreateCancellationManager();

        ArtifactPostProcessingManager.ReportFailureUnlessCancelled(reporter, ctrlC, "post-processing warning");

        console.GetOutput().Should().Contain("post-processing warning");
    }

    [TestMethod]
    public void ReportFailureUnlessCancelled_WhenCancelled_WritesNothing()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        using var ctrlC = CreateCancellationManager();
        // Cancellation kills the post-processing process, so its failure is the cancellation the
        // user asked for rather than a post-processing problem worth reporting.
        ctrlC.SimulateCtrlC();

        ArtifactPostProcessingManager.ReportFailureUnlessCancelled(reporter, ctrlC, "post-processing warning");

        console.GetOutput().Should().NotContain("post-processing warning");
    }

    private static ArtifactPostProcessingManager CreateManagerWithMergeableArtifacts(params string[] artifactPaths)
    {
        var manager = new ArtifactPostProcessingManager();
        TestModule module = CreateModule();
        manager.RecordCapabilities(
            module,
            "net10.0",
            "x64",
            new HandshakeMessage(new Dictionary<byte, string>
            {
                [HandshakeMessagePropertyNames.SupportedPostProcessorKinds] = "microsoft.testing.trx",
            }));

        foreach (string artifactPath in artifactPaths)
        {
            manager.RecordArtifact(
                module,
                "net10.0",
                "x64",
                "execution-1",
                new FileArtifactMessage(artifactPath, "TRX", null, null, null, null, "microsoft.testing.trx"));
        }

        return manager;
    }

    private static CtrlCCancellationManager CreateCancellationManager()
        => new(onFirstCtrlC: () => { }, exitAction: _ => { }, subscribeToConsole: false);

    private static BuildOptions CreateBuildOptions(string? resultsDirectory = null)
        => new(
            new PathOptions(null, null, null, ResultsDirectoryPath: resultsDirectory, null, null),
            HasNoRestore: false,
            HasNoBuild: false,
            Verbosity: null,
            NoLaunchProfile: false,
            NoLaunchProfileArguments: false,
            TestApplicationArguments: [],
            MSBuildArgs: [],
            Device: null,
            ListDevices: false,
            EnvironmentVariables: new Dictionary<string, string>());

    private static TerminalTestReporter CreateReporter(CapturingConsole console)
    {
        var reporter = new TerminalTestReporter(console, new TerminalTestReporterOptions
        {
            AnsiMode = AnsiMode.SimpleAnsi,
            ShowProgress = false,
        });
        reporter.TestExecutionStarted(
            DateTimeOffset.UtcNow,
            workerCount: 1,
            isDiscovery: false,
            isHelp: false,
            isRetry: false);
        return reporter;
    }

    private static ArtifactPostProcessingApplication CreateApplication()
    {
        return new ArtifactPostProcessingApplication(
            CreateModule(),
            "net10.0",
            "x64",
            new HashSet<string>(StringComparer.Ordinal) { "microsoft.testing.trx", "microsoft.codecoverage" },
            new HashSet<string>(StringComparer.Ordinal));
    }

    private static TestModule CreateModule()
        => new(
            new RunProperties("dotnet", "A.dll", null),
            ProjectFullPath: null,
            TargetFramework: "net10.0",
            IsTestingPlatformApplication: true,
            LaunchSettings: null,
            TargetPath: "A.dll",
            DotnetRootArchVariableName: null,
            EnvironmentVariables: new Dictionary<string, string>());

    private static ArtifactPostProcessingArtifact CreateArtifact(string path, string? kind)
        => new(path, kind, "A.dll", "net10.0", "x64", Guid.NewGuid().ToString("N"));
}
