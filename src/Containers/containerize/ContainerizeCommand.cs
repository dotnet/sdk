﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Text;
using Microsoft.NET.Build.Containers;

namespace containerize;

internal class ContainerizeCommand : RootCommand
{
    internal Argument<DirectoryInfo> PublishDirectoryArgument { get; } = new Argument<DirectoryInfo>(
            name: "PublishDirectory",
            description: "The directory for the build outputs to be published.")
            .AcceptLegalFilePathsOnly().AcceptExistingOnly();

    internal Option<string> BaseRegistryOption { get; } = new Option<string>(
            name: "--baseregistry",
            description: "The registry to use for the base image.")
            {
                IsRequired = true
            };

    internal Option<string> BaseImageNameOption { get;  } = new Option<string>(
            name: "--baseimagename",
            description: "The base image to pull.")
            {
                IsRequired = true
            };

    internal Option<string> BaseImageTagOption { get; } = new Option<string>(
            name: "--baseimagetag",
            description: "The base image tag. Ex: 6.0",
            defaultValueFactory: () => "latest");

    internal Option<string> OutputRegistryOption { get; } = new Option<string>(
            name: "--outputregistry",
            description: "The registry to push to.")
            {
                IsRequired = false
            };

    internal Option<string> ImageNameOption { get; } = new Option<string>(
            name: "--imagename",
            description: "The name of the output image that will be pushed to the registry.")
            {
                IsRequired = true
            };

    internal Option<string[]> ImageTagsOption { get; } = new Option<string[]>(
            name: "--imagetags",
            description: "The tags to associate with the new image.")
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string> WorkingDirectoryOption { get; } = new Option<string>(
            name: "--workingdirectory",
            description: "The working directory of the container.")
            {
                IsRequired = true
            };

    internal Option<string[]> EntrypointOption { get; } = new Option<string[]>(
            name: "--entrypoint",
            description: "The entrypoint application of the container.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string[]> EntrypointArgsOption { get; } = new Option<string[]>(
            name: "--entrypointargs",
            description: "Arguments to pass alongside Entrypoint.")
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string> LocalRegistryOption { get; } = new Option<string>(
            name: "--localregistry",
            description: "The local registry to push to")
        .AcceptOnlyFromAmong(KnownLocalRegistryTypes.SupportedLocalRegistryTypes);

    internal Option<Dictionary<string, string>> LabelsOption { get; } = new(
            name: "--labels",
            description: "Labels that the image configuration will include in metadata.",
            parseArgument: result => ParseDictionary(result, errorMessage: "Incorrectly formatted labels: "))
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<Port[]> PortsOption { get; } = new Option<Port[]>(
            name: "--ports",
            description: "Ports that the application declares that it will use. Note that this means nothing to container hosts, by default - it's mostly documentation. Ports should be of the form {number}/{type}, where {type} is tcp or udp",
            parseArgument: result => {
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
                    else if(split.Length == 1)
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
                    result.ErrorMessage = builder.ToString();
                    return Array.Empty<Port>();
                }
                return goodPorts.ToArray();
            })
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<Dictionary<string, string>> EnvVarsOption { get; } = new(
            name: "--environmentvariables",
            description: "Container environment variables to set.",
            parseArgument: result => ParseDictionary(result, errorMessage: "Incorrectly formatted environment variables:  "))
            {
                AllowMultipleArgumentsPerToken = true
            };

    internal Option<string> RidOption { get; } = new Option<string>(name: "--rid", description: "Runtime Identifier of the generated container.");

    internal Option<string> RidGraphPathOption { get; } = new Option<string>(name: "--ridgraphpath", description: "Path to the RID graph file.");

    internal Option<string> ContainerUserOption { get; } = new Option<string>(name: "--container-user", description: "User to run the container as.");


    internal ContainerizeCommand() : base("Containerize an application without Docker.")
    { 
        this.AddArgument(PublishDirectoryArgument);
        this.AddOption(BaseRegistryOption);
        this.AddOption(BaseImageNameOption);
        this.AddOption(BaseImageTagOption);
        this.AddOption(OutputRegistryOption);
        this.AddOption(ImageNameOption);
        this.AddOption(ImageTagsOption);
        this.AddOption(WorkingDirectoryOption);
        this.AddOption(EntrypointOption);
        this.AddOption(EntrypointArgsOption);
        this.AddOption(LabelsOption);
        this.AddOption(PortsOption);
        this.AddOption(EnvVarsOption);
        this.AddOption(RidOption);
        this.AddOption(RidGraphPathOption);
        this.AddOption(LocalRegistryOption);
        this.AddOption(ContainerUserOption);

        this.SetHandler(async (context) =>
        {
            DirectoryInfo _publishDir = context.ParseResult.GetValue(PublishDirectoryArgument);
            string _baseReg = context.ParseResult.GetValue(BaseRegistryOption)!;
            string _baseName = context.ParseResult.GetValue(BaseImageNameOption)!;
            string _baseTag = context.ParseResult.GetValue(BaseImageTagOption)!;
            string? _outputReg = context.ParseResult.GetValue(OutputRegistryOption);
            string _name = context.ParseResult.GetValue(ImageNameOption)!;
            string[] _tags = context.ParseResult.GetValue(ImageTagsOption)!;
            string _workingDir = context.ParseResult.GetValue(WorkingDirectoryOption)!;
            string[] _entrypoint = context.ParseResult.GetValue(EntrypointOption)!;
            string[]? _entrypointArgs = context.ParseResult.GetValue(EntrypointArgsOption);
            Dictionary<string, string> _labels = context.ParseResult.GetValue(LabelsOption) ?? new Dictionary<string, string>();
            Port[]? _ports = context.ParseResult.GetValue(PortsOption);
            Dictionary<string, string> _envVars = context.ParseResult.GetValue(EnvVarsOption) ?? new Dictionary<string, string>();
            string _rid = context.ParseResult.GetValue(RidOption)!;
            string _ridGraphPath = context.ParseResult.GetValue(RidGraphPathOption)!;
            string _localRegistry = context.ParseResult.GetValue(LocalRegistryOption)!;
            string? _containerUser = context.ParseResult.GetValue(ContainerUserOption);
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
                _localRegistry,
                _containerUser,
                context.GetCancellationToken()).ConfigureAwait(false);
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
            argumentResult.ErrorMessage = errorMessage + invalidTokens.Aggregate((x, y) => x = x + ";" + y);
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
