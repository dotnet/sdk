// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
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
        ArtifactPostProcessingPlan plan = ArtifactPostProcessingPlanner.Plan(
            SnapshotApplications(),
            SnapshotArtifacts());

        foreach (ArtifactPostProcessingJob job in plan.Jobs)
        {
            if (ctrlC.Token.IsCancellationRequested)
            {
                break;
            }

            string tempDirectory = Path.Combine(
                Path.GetTempPath(),
                $"dotnet-test-postproc-{Guid.NewGuid():N}");

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
                    output,
                    onHelpRequested: _ => { },
                    artifactPostProcessingManager: this,
                    artifactPostProcessingInvocation: invocation);

                int exitCode = await application.RunAsync(ctrlC);
                ApplyOutputs(output, job, invocation.SnapshotOutputs());

                if (invocation.FailureMessage is { } failureMessage)
                {
                    output.WriteWarningMessage(string.Format(
                        CultureInfo.CurrentCulture,
                        CliCommandStrings.ArtifactPostProcessingFailed,
                        job.Application.Module.TargetPath,
                        failureMessage));
                }
                else if (exitCode != ExitCode.Success)
                {
                    output.WriteWarningMessage(string.Format(
                        CultureInfo.CurrentCulture,
                        CliCommandStrings.ArtifactPostProcessingProcessFailed,
                        job.Application.Module.TargetPath,
                        exitCode));
                }
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or Win32Exception
                or NotSupportedException
                or TimeoutException)
            {
                output.WriteWarningMessage(string.Format(
                    CultureInfo.CurrentCulture,
                    CliCommandStrings.ArtifactPostProcessingFailed,
                    job.Application.Module.TargetPath,
                    ex.Message));
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

    private static string GetOutputDirectory(BuildOptions buildOptions, ArtifactPostProcessingJob job)
    {
        if (buildOptions.PathOptions.ResultsDirectoryPath is { } resultsDirectory)
        {
            return Path.GetFullPath(resultsDirectory);
        }

        string firstInputPath = job.Groups
            .SelectMany(group => group.Artifacts)
            .Select(artifact => artifact.Path)
            .OrderBy(path => path, FileUtilities.PathComparer)
            .First();

        return Path.GetDirectoryName(Path.GetFullPath(firstInputPath))!;
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
