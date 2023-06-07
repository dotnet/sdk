﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using Microsoft.NET.Build.Containers;

namespace containerize;

internal class ContainerizeCommand : CliRootCommand
{
    internal CliArgument<DirectoryInfo> PublishDirectoryArgument { get; } = new CliArgument<DirectoryInfo>("PublishDirectory")
    {
        Description = "The directory for the build outputs to be published."
    }.AcceptExistingOnly();

    internal CliOption<string> BaseRegistryOption { get; } = new("--baseregistry")
    {
        Description = "The registry to use for the base image.",
        Required = true
    };

    internal CliOption<string> BaseImageNameOption { get;  } = new("--baseimagename")
    {
        Description = "The base image to pull.",
        Required = true
    };

    internal CliOption<string> BaseImageTagOption { get; } = new("--baseimagetag")
    {
        Description = "The base image tag. Ex: 6.0",
        DefaultValueFactory = (_) => "latest"
    };

    internal CliOption<string> OutputRegistryOption { get; } = new("--outputregistry")
    {
        Description = "The registry to push to.",
        Required = false
    };

    internal CliOption<string> RepositoryOption { get; } = new("--repository")
    {
        Description = "The name of the output container repository that will be pushed to the registry.",
        Required = true
    };

    internal CliOption<string[]> ImageTagsOption { get; } = new("--imagetags")
    {
        Description = "The tags to associate with the new image.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string> WorkingDirectoryOption { get; } = new("--workingdirectory")
    {
        Description = "The working directory of the container.",
        Required = true
    };

    internal CliOption<string[]> EntrypointOption { get; } = new("--entrypoint")
    {
        Description = "The entrypoint application of the container.",
        Required = true,
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string[]> EntrypointArgsOption { get; } = new("--entrypointargs")
    {
        Description = "Arguments to pass alongside Entrypoint.",
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string> LocalRegistryOption { get; } = new CliOption<string>("--localregistry")
    {
        Description = "The local registry to push to"
    };

    internal CliOption<Dictionary<string, string>> LabelsOption { get; } = new("--labels")
    {
        Description = "Labels that the image configuration will include in metadata.",
        CustomParser = result => ParseDictionary(result, errorMessage: "Incorrectly formatted labels: "),
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<Port[]> PortsOption { get; } = new("--ports")
    {
        Description = "Ports that the application declares that it will use. Note that this means nothing to container hosts, by default - it's mostly documentation. Ports should be of the form {number}/{type}, where {type} is tcp or udp",
        AllowMultipleArgumentsPerToken = true,
        CustomParser = result =>
        {
            string[] ports = result.Tokens.Select(x => x.Value).ToArray();
            var goodPorts = new List<Port>();
            var badPorts = new List<(string, ContainerHelpers.ParsePortError)>();

            foreach (string port in ports)
            {
                string[] split = port.Split('/');
                if (split.Length == 2)
                {
                    if (ContainerHelpers.TryParsePort(split[0], split[1], out var portInfo, out var portError))
                    {
                        goodPorts.Add(portInfo.Value);
                    }
                    else
                    {
                        var pe = (ContainerHelpers.ParsePortError)portError!;
                        badPorts.Add((port, pe));
                    }
                }
                else if (split.Length == 1)
                {
                    if (ContainerHelpers.TryParsePort(split[0], out var portInfo, out var portError))
                    {
                        goodPorts.Add(portInfo.Value);
                    }
                    else
                    {
                        var pe = (ContainerHelpers.ParsePortError)portError!;
                        badPorts.Add((port, pe));
                    }
                }
                else
                {
                    badPorts.Add((port, ContainerHelpers.ParsePortError.UnknownPortFormat));
                    continue;
                }
            }

            if (badPorts.Count != 0)
            {
                var builder = new StringBuilder();
                builder.AppendLine("Incorrectly formatted ports:");
                foreach (var (badPort, error) in badPorts)
                {
                    var errors = Enum.GetValues<ContainerHelpers.ParsePortError>().Where(e => error.HasFlag(e));
                    builder.AppendLine($"\t{badPort}:\t({string.Join(", ", errors)})");
                }
                result.AddError(builder.ToString());
                return Array.Empty<Port>();
            }
            return goodPorts.ToArray();
        }
    };

    internal CliOption<Dictionary<string, string>> EnvVarsOption { get; } = new("--environmentvariables")
    {
        Description = "Container environment variables to set.",
        CustomParser = result => ParseDictionary(result, errorMessage: "Incorrectly formatted environment variables:  "),
        AllowMultipleArgumentsPerToken = true
    };

    internal CliOption<string> RidOption { get; } = new("--rid") { Description = "Runtime Identifier of the generated container." };

    internal CliOption<string> RidGraphPathOption { get; } = new("--ridgraphpath") { Description = "Path to the RID graph file." };

    internal CliOption<string> ContainerUserOption { get; } = new("--container-user") { Description = "User to run the container as." };

    internal ContainerizeCommand() : base("Containerize an application without Docker.")
    {
        PublishDirectoryArgument.AcceptLegalFilePathsOnly();
        this.Arguments.Add(PublishDirectoryArgument);
        this.Options.Add(BaseRegistryOption);
        this.Options.Add(BaseImageNameOption);
        this.Options.Add(BaseImageTagOption);
        this.Options.Add(OutputRegistryOption);
        this.Options.Add(RepositoryOption);
        this.Options.Add(ImageTagsOption);
        this.Options.Add(WorkingDirectoryOption);
        this.Options.Add(EntrypointOption);
        this.Options.Add(EntrypointArgsOption);
        this.Options.Add(LabelsOption);
        this.Options.Add(PortsOption);
        this.Options.Add(EnvVarsOption);
        this.Options.Add(RidOption);
        this.Options.Add(RidGraphPathOption);
        LocalRegistryOption.AcceptOnlyFromAmong(KnownLocalRegistryTypes.SupportedLocalRegistryTypes);
        this.Options.Add(LocalRegistryOption);
        this.Options.Add(ContainerUserOption);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            DirectoryInfo _publishDir = parseResult.GetValue(PublishDirectoryArgument)!;
            string _baseReg = parseResult.GetValue(BaseRegistryOption)!;
            string _baseName = parseResult.GetValue(BaseImageNameOption)!;
            string _baseTag = parseResult.GetValue(BaseImageTagOption)!;
            string? _outputReg = parseResult.GetValue(OutputRegistryOption);
            string _name = parseResult.GetValue(RepositoryOption)!;
            string[] _tags = parseResult.GetValue(ImageTagsOption)!;
            string _workingDir = parseResult.GetValue(WorkingDirectoryOption)!;
            string[] _entrypoint = parseResult.GetValue(EntrypointOption)!;
            string[]? _entrypointArgs = parseResult.GetValue(EntrypointArgsOption);
            Dictionary<string, string> _labels = parseResult.GetValue(LabelsOption) ?? new Dictionary<string, string>();
            Port[]? _ports = parseResult.GetValue(PortsOption);
            Dictionary<string, string> _envVars = parseResult.GetValue(EnvVarsOption) ?? new Dictionary<string, string>();
            string _rid = parseResult.GetValue(RidOption)!;
            string _ridGraphPath = parseResult.GetValue(RidGraphPathOption)!;
            string _localContainerDaemon = parseResult.GetValue(LocalRegistryOption)!;
            string? _containerUser = parseResult.GetValue(ContainerUserOption);
            await ContainerBuilder.ContainerizeAsync(
                _publishDir,
                _workingDir,
                _baseReg,
                _baseName,
                _baseTag,
                _entrypoint,
                _entrypointArgs,
                _name,
                _tags,
                _outputReg,
                _labels,
                _ports,
                _envVars,
                _rid,
                _ridGraphPath,
                _localContainerDaemon,
                _containerUser,
                cancellationToken).ConfigureAwait(false);
        });
    }

    private static Dictionary<string, string> ParseDictionary(ArgumentResult argumentResult, string errorMessage)
    {
        Dictionary<string, string> parsed = new();
        string[] tokens = argumentResult.Tokens.Select(x => x.Value).ToArray();
        IEnumerable<string> invalidTokens = tokens.Where(v => v.Split('=', StringSplitOptions.TrimEntries).Length != 2);

        // Is there a non-zero number of Labels that didn't split into two elements? If so, assume invalid input and error out
        if (invalidTokens.Any())
        {
            argumentResult.AddError(errorMessage + invalidTokens.Aggregate((x, y) => x = x + ";" + y));
            return parsed;
        }

        foreach (string token in tokens)
        {
            string[] pair = token.Split('=', StringSplitOptions.TrimEntries);
            parsed[pair[0]] = pair[1];
        }
        return parsed;
    }
}
