// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class ArtifactPostProcessingManager
{
    private readonly Lock _lock = new();
    private readonly Dictionary<TestModule, ApplicationState> _applications = [];
    private readonly List<ArtifactPostProcessingArtifact> _artifacts = [];

    public void RecordCapabilities(
        TestModule module,
        string? targetFramework,
        string? architecture,
        HandshakeMessage handshakeMessage)
    {
        string[] kinds = ParseCapabilities(handshakeMessage, HandshakeMessagePropertyNames.SupportedPostProcessorKinds);
        string[] extensions = ParseCapabilities(handshakeMessage, HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy)
            .Select(extension => extension.ToLowerInvariant())
            .ToArray();

        if (kinds.Length == 0 && extensions.Length == 0)
        {
            return;
        }

        lock (_lock)
        {
            if (!_applications.TryGetValue(module, out ApplicationState? application))
            {
                application = new ApplicationState(module, targetFramework, architecture);
                _applications.Add(module, application);
            }

            application.SupportedKinds.UnionWith(kinds);
            application.SupportedExtensions.UnionWith(extensions);
        }
    }

    public void RecordArtifact(
        TestModule module,
        string? targetFramework,
        string? architecture,
        string executionId,
        FileArtifactMessage artifact)
    {
        lock (_lock)
        {
            _artifacts.Add(new ArtifactPostProcessingArtifact(
                artifact.FullPath!,
                artifact.Kind,
                module.TargetPath,
                targetFramework,
                architecture,
                executionId));
        }
    }

    public async Task ExecuteAsync(
        BuildOptions buildOptions,
        TerminalTestReporter output,
        CtrlCCancellationManager ctrlC)
    {
        try
        {
            await ExecuteCoreAsync(buildOptions, output, ctrlC);
        }
        catch (Exception ex)
        {
            // Individual jobs already degrade their own failures to warnings. This guard covers
            // everything around them — planning, the progress line, telemetry — so that no part of a
            // best-effort convenience layered on a finished run can escape and turn that run into a
            // CLI crash with a different exit code.
            Logger.LogTrace($"Artifact post-processing failed: {ex}");
        }
    }

    private async Task ExecuteCoreAsync(
        BuildOptions buildOptions,
        TerminalTestReporter output,
        CtrlCCancellationManager ctrlC)
    {
        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            SnapshotApplications(),
            SnapshotArtifacts());
        ArtifactPostProcessingJob[] runnableJobs =
        [
            .. plan.Jobs.Where(job =>
            {
                bool supported = !TestApplication.RequiresHttpTransport(job.Application.Module);
                if (!supported)
                {
                    Logger.LogTrace(
                        $"Skipping artifact post-processing for WebAssembly module '{job.Application.Module.TargetPath}' because no browser-aware merge host is available.");
                }

                return supported;
            }),
        ];

        if (runnableJobs.Length == 0)
        {
            return;
        }

        // Post-processing runs after the last test application has exited and before the run summary
        // is rendered, so without this line a merge that takes a while is indistinguishable from a
        // hung CLI: the progress area is empty and no assembly is still running.
        output.WriteInformationMessage(CliCommandStrings.ArtifactPostProcessingStarted);

        long startTimestamp = Stopwatch.GetTimestamp();
        int executedJobs = 0;
        int failedJobs = 0;

        foreach (ArtifactPostProcessingJob job in runnableJobs)
        {
            if (ctrlC.Token.IsCancellationRequested)
            {
                break;
            }

            string tempDirectory = Path.Combine(
                Path.GetTempPath(),
                $"dotnet-test-postproc-{Guid.NewGuid():N}");

            executedJobs++;

            try
            {
                Directory.CreateDirectory(tempDirectory);
                string manifestPath = Path.Combine(tempDirectory, "manifest.json");
                string outputDirectory = GetOutputDirectory(buildOptions, job);
                Directory.CreateDirectory(outputDirectory);
                WriteManifest(manifestPath, outputDirectory, job.Groups.SelectMany(group => group.Artifacts));

                var invocation = new ArtifactPostProcessingInvocation(manifestPath);
                var toolOptions = new TestOptions(
                    IsHelp: false,
                    IsDiscovery: false,
                    ListTestsFormat: TestListFormat.Text,
                    IsArtifactPostProcessing: true);

                using var application = new TestApplication(
                    job.Application.Module,
                    buildOptions,
                    toolOptions,
                    // Post-processing merges artifacts across modules, so it keeps writing to the
                    // shared results directory even when the run uses a per-module layout.
                    TestResultsDirectoryResolver.CreateShared(buildOptions.PathOptions, Directory.GetCurrentDirectory()),
                    output,
                    onHelpRequested: _ => { },
                    artifactPostProcessingManager: this,
                    artifactPostProcessingInvocation: invocation);

                int exitCode = await application.RunAsync(ctrlC);
                ApplyOutputs(output, job, invocation.SnapshotOutputs());

                if (invocation.FailureMessage is { } failureMessage)
                {
                    failedJobs += ReportFailureUnlessCancelled(output, ctrlC, string.Format(
                        CultureInfo.CurrentCulture,
                        CliCommandStrings.ArtifactPostProcessingFailed,
                        job.Application.Module.TargetPath,
                        failureMessage)) ? 1 : 0;
                }
                else if (exitCode != ExitCode.Success)
                {
                    failedJobs += ReportFailureUnlessCancelled(output, ctrlC, string.Format(
                        CultureInfo.CurrentCulture,
                        CliCommandStrings.ArtifactPostProcessingProcessFailed,
                        job.Application.Module.TargetPath,
                        exitCode)) ? 1 : 0;
                }
            }
            catch (Exception ex)
            {
                // Post-processing is a best-effort convenience on top of a completed test run: the
                // original artifacts are always still on disk and still reported, so no failure here
                // may escape and turn a finished run into a CLI crash with a different exit code.
                Logger.LogTrace($"Artifact post-processing with '{job.Application.Module.TargetPath}' failed: {ex}");

                failedJobs += ReportFailureUnlessCancelled(output, ctrlC, string.Format(
                    CultureInfo.CurrentCulture,
                    CliCommandStrings.ArtifactPostProcessingFailed,
                    job.Application.Module.TargetPath,
                    ex.Message)) ? 1 : 0;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, recursive: true);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.LogTrace($"Failed to clean artifact post-processing temporary directory '{tempDirectory}': {ex}");
                }
            }
        }

        ArtifactPostProcessingTelemetry.TrackPostProcessing(
            plan,
            executedJobs,
            failedJobs,
            Stopwatch.GetElapsedTime(startTimestamp));
    }

    /// <summary>
    /// Reports a post-processing failure, unless the user cancelled the run, and returns whether it
    /// was reported. Cancellation kills the post-processing process the same way it kills a test
    /// application, so the resulting failure is the cancellation the user asked for rather than a
    /// post-processing problem worth reporting — or counting as a failure in telemetry.
    /// </summary>
    internal static bool ReportFailureUnlessCancelled(
        TerminalTestReporter output,
        CtrlCCancellationManager ctrlC,
        string message)
    {
        if (ctrlC.Token.IsCancellationRequested)
        {
            return false;
        }

        output.WriteWarningMessage(message);
        return true;
    }

    internal IReadOnlyList<ArtifactPostProcessingApplication> SnapshotApplications()
    {
        lock (_lock)
        {
            return
            [
                .. _applications.Values.Select(application => new ArtifactPostProcessingApplication(
                    application.Module,
                    application.TargetFramework,
                    application.Architecture,
                    new HashSet<string>(application.SupportedKinds, StringComparer.Ordinal),
                    new HashSet<string>(application.SupportedExtensions, StringComparer.Ordinal)))
            ];
        }
    }

    internal IReadOnlyList<ArtifactPostProcessingArtifact> SnapshotArtifacts()
    {
        lock (_lock)
        {
            return [.. _artifacts];
        }
    }

    private static string[] ParseCapabilities(HandshakeMessage handshakeMessage, byte propertyName)
        => !handshakeMessage.Properties.TryGetValue(propertyName, out string? capabilities)
            ? []
            : capabilities
                .Split(CliConstants.SemiColon, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(capability => capability.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    internal static string GetOutputDirectory(BuildOptions buildOptions, ArtifactPostProcessingJob job)
    {
        if (buildOptions.PathOptions.ResultsDirectoryPath is { } resultsDirectory)
        {
            return Path.GetFullPath(resultsDirectory);
        }

        if (job.Application.Module.UseArtifactsOutput
            && TestResultsDirectoryResolver.GetResultsDirectoryRoot(
                buildOptions.PathOptions,
                job.Application.Module,
                Directory.GetCurrentDirectory()) is { } artifactsResultsDirectory)
        {
            return Path.GetFullPath(artifactsResultsDirectory);
        }

        ArtifactPostProcessingArtifact[] inputs =
        [
            .. job.Groups
                .SelectMany(group => group.Artifacts)
                .OrderBy(artifact => artifact.Path, FileUtilities.PathComparer)
        ];

        // Without --results-directory every test application writes its artifacts next to its own
        // binaries, so no directory belongs to the run as a whole. Prefer one the elected application
        // already writes to: the merged artifact then lands beside the reports it summarizes instead
        // of inside an unrelated project's output. An application can be elected purely for the kinds
        // it advertises without having produced any of the inputs, so fall back to the first input
        // directory in path order.
        ArtifactPostProcessingArtifact preferredInput = inputs.FirstOrDefault(artifact =>
            FileUtilities.PathComparer.Equals(artifact.ProducingTestModule, job.Application.Module.TargetPath))
            ?? inputs[0];

        return Path.GetDirectoryName(Path.GetFullPath(preferredInput.Path))!;
    }

    private static void WriteManifest(
        string manifestPath,
        string outputDirectory,
        IEnumerable<ArtifactPostProcessingArtifact> artifacts)
    {
        using FileStream stream = File.Create(manifestPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteString("outputDirectory", outputDirectory);
        writer.WriteStartArray("inputs");
        foreach (ArtifactPostProcessingArtifact artifact in artifacts
            .OrderBy(artifact => artifact.Path, FileUtilities.PathComparer))
        {
            writer.WriteStartObject();
            writer.WriteString("path", artifact.Path);
            WriteNullableString(writer, "kind", artifact.Kind);
            WriteNullableString(writer, "producingTestModule", artifact.ProducingTestModule);
            WriteNullableString(writer, "targetFramework", artifact.TargetFramework);
            WriteNullableString(writer, "architecture", artifact.Architecture);
            writer.WriteString("executionId", artifact.ExecutionId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
        }
        else
        {
            writer.WriteString(propertyName, value);
        }
    }

    internal static void ApplyOutputs(
        TerminalTestReporter output,
        ArtifactPostProcessingJob job,
        IReadOnlyList<ArtifactPostProcessingArtifact> processedArtifacts)
    {
        foreach (ArtifactPostProcessingArtifact processedArtifact in processedArtifacts)
        {
            string outputExtension = Path.GetExtension(processedArtifact.Path).ToLowerInvariant();
            // The dispatcher gives one processor both its kind-tagged inputs and matching legacy
            // extension inputs, so the returned output consumes both groups.
            ArtifactPostProcessingGroup[] consumedGroups =
            [
                .. job.Groups.Where(group =>
                    group.IsKind
                        ? string.Equals(group.Key, processedArtifact.Kind, StringComparison.Ordinal)
                        : string.Equals(group.Key, outputExtension, StringComparison.Ordinal))
            ];

            if (consumedGroups.Length > 0)
            {
                var consumedPaths = new HashSet<string>(
                    consumedGroups.SelectMany(group => group.Artifacts).Select(artifact => artifact.Path),
                    FileUtilities.PathComparer);
                output.RemoveArtifacts(consumedPaths);
            }

            output.ArtifactAdded(
                outOfProcess: true,
                job.Application.Module.TargetPath,
                job.Application.TargetFramework,
                job.Application.Architecture,
                processedArtifact.ExecutionId,
                testName: null,
                processedArtifact.Path);
        }
    }

    private sealed class ApplicationState(TestModule module, string? targetFramework, string? architecture)
    {
        public TestModule Module { get; } = module;
        public string? TargetFramework { get; } = targetFramework;
        public string? Architecture { get; } = architecture;
        public HashSet<string> SupportedKinds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> SupportedExtensions { get; } = new(StringComparer.Ordinal);
    }
}

internal sealed class ArtifactPostProcessingInvocation(string manifestPath)
{
    private readonly Lock _lock = new();
    private readonly List<ArtifactPostProcessingArtifact> _outputs = [];
    private string? _failureMessage;

    public string ManifestPath { get; } = manifestPath;

    public string? FailureMessage
    {
        get
        {
            lock (_lock)
            {
                return _failureMessage;
            }
        }
    }

    public void RecordFailure(string message)
    {
        lock (_lock)
        {
            _failureMessage ??= message;
        }
    }

    public void RecordOutput(
        TestModule module,
        string? targetFramework,
        string? architecture,
        string executionId,
        FileArtifactMessage artifact)
    {
        lock (_lock)
        {
            _outputs.Add(new ArtifactPostProcessingArtifact(
                artifact.FullPath!,
                artifact.Kind,
                module.TargetPath,
                targetFramework,
                architecture,
                executionId));
        }
    }

    public IReadOnlyList<ArtifactPostProcessingArtifact> SnapshotOutputs()
    {
        lock (_lock)
        {
            return [.. _outputs];
        }
    }
}
