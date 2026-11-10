// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
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
    public void BuildArtifactPostProcessingArguments_ForwardsOptionsThatGovernExtensionLoading()
    {
        var pathOptions = new PathOptions(
            ProjectOrSolutionPath: null,
            SolutionPath: null,
            TestModules: null,
            ResultsDirectoryPath: "/results",
            ResultsDirectoryLayout: ResultsDirectoryLayout.Flat,
            ConfigFilePath: "/config/testconfig.json",
            DiagnosticOutputDirectoryPath: "/diagnostics");

        string arguments = TestApplication.BuildArtifactPostProcessingArguments(
            new StringBuilder("exec A.dll"),
            pathOptions,
            "/tmp/manifest.json",
            "pipe-name");

        arguments.Should().Contain($"{CliConstants.ArtifactPostProcessingToolName}");
        arguments.Should().Contain($"{CliConstants.ArtifactPostProcessingManifestOptionKey} ");
        arguments.Should().Contain(
            TestCommandDefinition.MicrosoftTestingPlatform.ConfigFileOptionName,
            "a merge host resolves its extensions from the same configuration file as the test run");
        arguments.Should().Contain(TestCommandDefinition.MicrosoftTestingPlatform.DiagnosticOutputDirectoryOptionName);
        arguments.Should().Contain($"{CliConstants.ServerOptionKey} {CliConstants.ServerOptionValue}");
    }

    [TestMethod]
    public void BuildArtifactPostProcessingArguments_DoesNotForwardResultsDirectory()
    {
        var pathOptions = new PathOptions(
            ProjectOrSolutionPath: null,
            SolutionPath: null,
            TestModules: null,
            ResultsDirectoryPath: "/results",
            ResultsDirectoryLayout: ResultsDirectoryLayout.Flat,
            ConfigFilePath: null,
            DiagnosticOutputDirectoryPath: null);

        string arguments = TestApplication.BuildArtifactPostProcessingArguments(
            new StringBuilder("exec A.dll"),
            pathOptions,
            "/tmp/manifest.json",
            "pipe-name");

        arguments.Should().NotContain(
            TestCommandDefinition.MicrosoftTestingPlatform.ResultsDirectoryOptionName,
            "the merged output location travels in the manifest, so the SDK keeps control of it");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-number")]
    [DataRow("-1")]
    public void ParseArtifactPostProcessingTimeout_UnusableValue_KeepsDefault(string? configuredTimeout)
        => TestApplication.ParseArtifactPostProcessingTimeout(configuredTimeout)
            .Should().Be(TimeSpan.FromMinutes(15));

    [TestMethod]
    public void ParseArtifactPostProcessingTimeout_PositiveValue_IsInterpretedAsSeconds()
        => TestApplication.ParseArtifactPostProcessingTimeout("30").Should().Be(TimeSpan.FromSeconds(30));

    [TestMethod]
    public void ParseArtifactPostProcessingTimeout_Zero_RemovesTheBound()
        => TestApplication.ParseArtifactPostProcessingTimeout("0").Should().Be(Timeout.InfiniteTimeSpan);

    [TestMethod]
    [DataRow("4294968")]
    [DataRow("2147483647")]
    [DataRow("9999999999")]
    public void ParseArtifactPostProcessingTimeout_ValueTooLargeToWaitOn_RemovesTheBound(string configuredTimeout)
    {
        // Task.WaitAsync throws for a timeout above ~49.7 days instead of waiting, which would fail
        // every merge. Asking for a timeout that long means 'effectively never'.
        TimeSpan timeout = TestApplication.ParseArtifactPostProcessingTimeout(configuredTimeout);

        timeout.Should().Be(Timeout.InfiniteTimeSpan);
        Action wait = () => Task.CompletedTask.WaitAsync(timeout, CancellationToken.None);
        wait.Should().NotThrow("a timeout the framework rejects would fail every merge instead of bounding it");
    }

    [TestMethod]
    public void ShouldPostProcessArtifacts_CompletedRun_MergesArtifacts()
        => MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(),
            noArtifactPostProcessingRequested: false,
            cancellationRequested: false,
            TestRunCancellationReason.None).Should().BeTrue();

    [TestMethod]
    public void ShouldPostProcessArtifacts_HelpOrDiscovery_MergesNothing()
    {
        MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(isHelp: true),
            noArtifactPostProcessingRequested: false,
            cancellationRequested: false,
            TestRunCancellationReason.None).Should().BeFalse("help prints usage and produces no artifacts");

        MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(isDiscovery: true),
            noArtifactPostProcessingRequested: false,
            cancellationRequested: false,
            TestRunCancellationReason.None).Should().BeFalse("discovery runs no tests and produces no artifacts");
    }

    [TestMethod]
    public void ShouldPostProcessArtifacts_OptedOut_MergesNothing()
        => MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(),
            noArtifactPostProcessingRequested: true,
            cancellationRequested: false,
            TestRunCancellationReason.None).Should().BeFalse();

    [TestMethod]
    public void ShouldPostProcessArtifacts_TruncatedRun_MergesNothing()
    {
        // Merging the artifacts of a run that was cut short would hide the truncation behind one
        // authoritative-looking report, so every way of cutting a run short has to skip the merge.
        MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(),
            noArtifactPostProcessingRequested: false,
            cancellationRequested: true,
            TestRunCancellationReason.None).Should().BeFalse("Ctrl+C leaves a truncated run");

        MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(),
            noArtifactPostProcessingRequested: false,
            cancellationRequested: false,
            TestRunCancellationReason.MaximumFailedTests).Should().BeFalse("--maximum-failed-tests leaves a truncated run");

        MicrosoftTestingPlatformTestCommand.ShouldPostProcessArtifacts(
            CreateTestOptions(),
            noArtifactPostProcessingRequested: false,
            cancellationRequested: false,
            TestRunCancellationReason.Timeout).Should().BeFalse("--timeout leaves a truncated run");
    }

    private static TestOptions CreateTestOptions(bool isHelp = false, bool isDiscovery = false)
        => new(isHelp, isDiscovery, TestListFormat.Text);

    [TestMethod]
    public void GetOutputDirectory_WithResultsDirectory_UsesThatDirectory()
    {
        string resultsDirectory = Path.Combine(Path.GetTempPath(), "results");
        ArtifactPostProcessingJob job = CreateJob(
            CreateArtifact(Path.Combine(Path.GetTempPath(), "a", "first.trx"), "microsoft.testing.trx"));

        string outputDirectory = ArtifactPostProcessingManager.GetOutputDirectory(
            CreateBuildOptions(resultsDirectory),
            job);

        outputDirectory.Should().Be(Path.GetFullPath(resultsDirectory));
    }

    [TestMethod]
    public void GetOutputDirectory_WithArtifactsOutput_UsesArtifactsTestDirectory()
    {
        string artifactsDirectory = Path.Combine(Path.GetTempPath(), "artifacts");
        TestModule module = CreateModule() with
        {
            UseArtifactsOutput = true,
            ArtifactsPath = artifactsDirectory,
        };
        ArtifactPostProcessingJob job = CreateJob(
            module,
            CreateArtifact(Path.Combine(artifactsDirectory, "test", "project", "result.trx"), "microsoft.testing.trx"));

        string outputDirectory = ArtifactPostProcessingManager.GetOutputDirectory(
            CreateBuildOptions(),
            job);

        outputDirectory.Should().Be(Path.Combine(artifactsDirectory, "test"));
    }

    [TestMethod]
    public void GetOutputDirectory_WithoutResultsDirectory_PrefersDirectoryOfElectedApplicationInput()
    {
        // 'aaa' sorts before 'zzz', so an implementation that just takes the first input in path
        // order would drop the merged artifact into the output of a project that merely contributed
        // an input, instead of beside the reports of the application performing the merge.
        ArtifactPostProcessingArtifact produced = new(
            Path.Combine(Path.GetTempPath(), "zzz", "elected.trx"),
            "microsoft.testing.trx",
            ProducingTestModule: "A.dll",
            "net10.0",
            "x64",
            "execution-1");
        ArtifactPostProcessingArtifact other = new(
            Path.Combine(Path.GetTempPath(), "aaa", "other.trx"),
            "microsoft.testing.trx",
            ProducingTestModule: "B.dll",
            "net10.0",
            "x64",
            "execution-2");

        string outputDirectory = ArtifactPostProcessingManager.GetOutputDirectory(
            CreateBuildOptions(),
            CreateJob(other, produced));

        outputDirectory.Should().Be(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "zzz")));
    }

    [TestMethod]
    public void GetOutputDirectory_WhenElectedApplicationProducedNoInput_UsesFirstInputDirectoryInPathOrder()
    {
        // An application can be elected purely for the kinds it advertises, without having produced
        // any of the inputs. Path order then keeps the choice deterministic.
        ArtifactPostProcessingArtifact last = new(
            Path.Combine(Path.GetTempPath(), "zzz", "last.trx"),
            "microsoft.testing.trx",
            ProducingTestModule: "B.dll",
            "net10.0",
            "x64",
            "execution-1");
        ArtifactPostProcessingArtifact first = new(
            Path.Combine(Path.GetTempPath(), "aaa", "first.trx"),
            "microsoft.testing.trx",
            ProducingTestModule: "C.dll",
            "net10.0",
            "x64",
            "execution-2");

        string outputDirectory = ArtifactPostProcessingManager.GetOutputDirectory(
            CreateBuildOptions(),
            CreateJob(last, first));

        outputDirectory.Should().Be(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aaa")));
    }

    private static ArtifactPostProcessingJob CreateJob(params ArtifactPostProcessingArtifact[] artifacts)
        => CreateJob(CreateModule(), artifacts);

    private static ArtifactPostProcessingJob CreateJob(TestModule module, params ArtifactPostProcessingArtifact[] artifacts)
    {
        var application = new ArtifactPostProcessingApplication(
            module,
            "net10.0",
            "x64",
            new HashSet<string>(StringComparer.Ordinal) { "microsoft.testing.trx", "microsoft.codecoverage" },
            new HashSet<string>(StringComparer.Ordinal));
        return new ArtifactPostProcessingJob(
            application,
            [new ArtifactPostProcessingGroup("microsoft.testing.trx", IsKind: true, artifacts, [application])]);
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
    public async Task ExecuteAsync_WebAssemblyModule_SkipsUnsupportedPostProcessing()
    {
        var console = new CapturingConsole();
        using var reporter = CreateReporter(console);
        ArtifactPostProcessingManager manager = CreateManagerWithMergeableArtifacts(
            CreateModule("browser-wasm"),
            "first.trx",
            "second.trx");
        using var ctrlC = CreateCancellationManager();

        await manager.ExecuteAsync(CreateBuildOptions(), reporter, ctrlC);

        console.GetOutput().Should().NotContain(CliCommandStrings.ArtifactPostProcessingStarted);
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
        => CreateManagerWithMergeableArtifacts(CreateModule(), artifactPaths);

    private static ArtifactPostProcessingManager CreateManagerWithMergeableArtifacts(
        TestModule module,
        params string[] artifactPaths)
    {
        var manager = new ArtifactPostProcessingManager();
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
            new PathOptions(
                ProjectOrSolutionPath: null,
                SolutionPath: null,
                TestModules: null,
                ResultsDirectoryPath: resultsDirectory,
                ResultsDirectoryLayout: ResultsDirectoryLayout.Flat,
                ConfigFilePath: null,
                DiagnosticOutputDirectoryPath: null),
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

    private static TestModule CreateModule(string runtimeIdentifier = "")
        => new(
            new RunProperties("dotnet", "A.dll", null, runtimeIdentifier, string.Empty, string.Empty),
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
