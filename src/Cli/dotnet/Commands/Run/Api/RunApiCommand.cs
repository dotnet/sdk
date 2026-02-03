// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Run.Api;

/// <summary>
/// Takes JSON from stdin lines, produces JSON on stdout lines, doesn't perform any changes.
/// Can be used by IDEs to see the project file behind a file-based program.
/// </summary>
internal sealed class RunApiCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    public override int Execute()
    {
        for (string? line; (line = Console.ReadLine()) != null;)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                RunApiInput input = JsonSerializer.Deserialize(line, RunFileApiJsonSerializerContext.Default.RunApiInput)!;
                RunApiOutput output = input.Execute();
                Respond(output);
            }
            catch (Exception ex)
            {
                Respond(new RunApiOutput.Error { Message = ex.Message, Details = ex.ToString() });
            }
        }

        return 0;

        static void Respond(RunApiOutput message)
        {
            string json = JsonSerializer.Serialize(message, RunFileApiJsonSerializerContext.Default.RunApiOutput);
            Console.WriteLine(json);
        }
    }
}

[JsonDerivedType(typeof(GetProject), nameof(GetProject))]
[JsonDerivedType(typeof(GetRunCommand), nameof(GetRunCommand))]
internal abstract class RunApiInput
{
    private RunApiInput() { }

    public abstract RunApiOutput Execute();

    public sealed class GetProject : RunApiInput
    {
        public string? ArtifactsPath { get; init; }
        public required string EntryPointFileFullPath { get; init; }

        public override RunApiOutput Execute()
        {
            var sourceFile = SourceFile.Load(EntryPointFileFullPath);
            var directives = FileLevelDirectiveHelpers.FindDirectives(sourceFile, reportAllErrors: true, ErrorReporters.CreateCollectingReporter(out var diagnostics));
            string artifactsPath = ArtifactsPath ?? VirtualProjectBuilder.GetArtifactsPath(EntryPointFileFullPath);

            var csprojWriter = new StringWriter();
            VirtualProjectBuilder.WriteProjectFile(
                csprojWriter,
                directives,
                VirtualProjectBuilder.GetDefaultProperties(VirtualProjectBuildingCommand.TargetFrameworkVersion),
                isVirtualProject: true,
                targetFilePath: EntryPointFileFullPath,
                artifactsPath: artifactsPath);

            return new RunApiOutput.Project
            {
                Content = csprojWriter.ToString(),
                Diagnostics = diagnostics.ToImmutableArray(),
            };
        }
    }

    public sealed class GetRunCommand : RunApiInput
    {
        public string? ArtifactsPath { get; init; }
        public required string EntryPointFileFullPath { get; init; }

        public override RunApiOutput Execute()
        {
            var msbuildArgs = MSBuildArgs.FromVerbosity(VerbosityOptions.quiet);
            var buildCommand = new VirtualProjectBuildingCommand(
                EntryPointFileFullPath, msbuildArgs, artifactsPath: ArtifactsPath);

            buildCommand.MarkArtifactsFolderUsed();

            var runCommand = new RunCommand(
                noBuild: false,
                projectFileFullPath: null,
                entryPointFileFullPath: EntryPointFileFullPath,
                launchProfile: null,
                noLaunchProfile: false,
                noLaunchProfileArguments: false,
                device: null,
                listDevices: false,
                noRestore: false,
                noCache: false,
                interactive: false,
                msbuildArgs: msbuildArgs,
                applicationArgs: [],
                readCodeFromStdin: false,
                environmentVariables: ReadOnlyDictionary<string, string>.Empty);

            var result = runCommand.ReadLaunchProfileSettings();
            var targetCommand = (Utils.Command)runCommand.GetTargetCommand(result.Profile, buildCommand.CreateProjectInstance, cachedRunProperties: null, logger: null);

            return new RunApiOutput.RunCommand
            {
                ExecutablePath = targetCommand.CommandName,
                CommandLineArguments = targetCommand.CommandArgs,
                WorkingDirectory = targetCommand.StartInfo.WorkingDirectory,
                EnvironmentVariables = targetCommand.CustomEnvironmentVariables ?? ReadOnlyDictionary<string, string?>.Empty,
            };
        }
    }
}

[JsonDerivedType(typeof(Error), nameof(Error))]
[JsonDerivedType(typeof(Project), nameof(Project))]
[JsonDerivedType(typeof(RunCommand), nameof(RunCommand))]
internal abstract class RunApiOutput
{
    private RunApiOutput() { }

    /// <summary>
    /// When the API shape or behavior changes, this should be incremented so the callers (IDEs) can act accordingly
    /// (e.g., show an error message when an incompatible SDK version is being used).
    /// </summary>
    [JsonPropertyOrder(-1)]
    public int Version { get; } = 1;

    public sealed class Error : RunApiOutput
    {
        public required string Message { get; init; }
        public required string Details { get; init; }
    }

    public sealed class Project : RunApiOutput
    {
        public required string Content { get; init; }
        public required ImmutableArray<SimpleDiagnostic> Diagnostics { get; init; }
    }

    public sealed class RunCommand : RunApiOutput
    {
        public required string ExecutablePath { get; init; }
        public required string CommandLineArguments { get; init; }
        public required string? WorkingDirectory { get; init; }
        public required IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; }
    }
}

[JsonSerializable(typeof(RunApiInput))]
[JsonSerializable(typeof(RunApiOutput))]
internal partial class RunFileApiJsonSerializerContext : JsonSerializerContext;
