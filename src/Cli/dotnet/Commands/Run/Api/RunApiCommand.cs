﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var sourceFile = VirtualProjectBuildingCommand.LoadSourceFile(EntryPointFileFullPath);
            var errors = ImmutableArray.CreateBuilder<SimpleDiagnostic>();
            var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: true, errors);
            string artifactsPath = ArtifactsPath ?? VirtualProjectBuildingCommand.GetArtifactsPath(EntryPointFileFullPath);

            var csprojWriter = new StringWriter();
            VirtualProjectBuildingCommand.WriteProjectFile(csprojWriter, directives, isVirtualProject: true, targetFilePath: EntryPointFileFullPath, artifactsPath: artifactsPath);

            return new RunApiOutput.Project
            {
                Content = csprojWriter.ToString(),
                Diagnostics = errors.ToImmutableArray(),
            };
        }
    }

    public sealed class GetRunCommand : RunApiInput
    {
        public string? ArtifactsPath { get; init; }
        public required string EntryPointFileFullPath { get; init; }

        public override RunApiOutput Execute()
        {
            var buildCommand = new VirtualProjectBuildingCommand(
                entryPointFileFullPath: EntryPointFileFullPath,
                msbuildArgs: ["-verbosity:quiet"])
            {
                CustomArtifactsPath = ArtifactsPath,
            };

            buildCommand.PrepareProjectInstance();

            var runCommand = new RunCommand(
                noBuild: false,
                projectFileOrDirectory: null,
                launchProfile: null,
                noLaunchProfile: false,
                noLaunchProfileArguments: false,
                noRestore: false,
                noCache: false,
                interactive: false,
                verbosity: VerbosityOptions.quiet,
                restoreArgs: [],
                args: [EntryPointFileFullPath],
                environmentVariables: ReadOnlyDictionary<string, string>.Empty);

            runCommand.TryGetLaunchProfileSettingsIfNeeded(out var launchSettings);
            var targetCommand = (Utils.Command)runCommand.GetTargetCommand(buildCommand.CreateProjectInstance);
            runCommand.ApplyLaunchSettingsProfileToCommand(targetCommand, launchSettings);

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
