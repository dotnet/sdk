// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.NET.Build.Containers;

namespace CreateLayerTarball;

internal class CreateLayerTarballCommand : RootCommand
{
    internal Option<(string absolutefilePath, string relativePath)[]> InputFilesOption { get; } = new("--input-file")
    {
        Description = "Specify once per file in the container, in the format '<absolute path to file>=<relative path inside container working directory>'",
        Required = true,
        Arity = ArgumentArity.OneOrMore,
        CustomParser = result =>
        {
            var maps = new List<(string absolutefilePath, string relativePath)>(result.Tokens.Count);
            foreach (var token in result.Tokens)
            {
                var parts = token.Value.Split('=', StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    result.AddError($"Invalid input file format: '{token.Value}'. Expected format is '<absolute path>=<relative path>'.");
                    continue;
                }
                if (!Path.IsPathRooted(parts[0]))
                {
                    result.AddError($"The absolute path '{parts[0]}' is not rooted. Please provide a valid absolute path.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(parts[1]))
                {
                    result.AddError("The relative path inside the container working directory cannot be empty.");
                    continue;
                }
                maps.Add((absolutefilePath: parts[0], relativePath: parts[1]));
            }
            return maps.ToArray();
        }
    };

    internal Option<string> ContainerRootDirOption { get; } = new("--container-root-dir")
    {
        Description = "The directory within the container to place the files. Each files' RelativePath will be relative to this directory.",
        Required = true
    };
    internal Option<string> TargetRuntimeIdentifierOption { get; } = new("--target-runtime-identifier")
    {
        Description = "Used to determine what kind of layer to create. If this is a Windows RID, a Windows layer will be created; otherwise, a Linux layer will be created.",
        Required = true
    };
    internal Option<string> ParentImageFormatOption { get; } = new("--parent-image-format")
    {
        Description = "The KnownImageFormats or media type of the parent image of the layer to create. This is used to determine which media type should be used for the layer itself.",
        Required = true
    };
    internal Option<string> ContentStoreRootOption { get; } = new("--content-store-root")
    {
        Description = "The path to the local storage location where the created layer will be stored for anything 'downstream' that looks up objects by digest.",
        Required = true
    };
    internal Option<string> GeneratedLayerPath { get; } = new("--generated-layer-path")
    {
        Description = "The path to which the layer will be written. This is the final output of this command, and is the location where the layer can be found after the command completes.",
        Required = true
    };
    internal Option<string> ContainerUserOption { get; } = new("--container-user")
    {
        Description = """
                The username or UID which is a platform-specific structure that allows specific control over which user the process run as.
                This acts as a default value to use when the value is not specified when creating a container.
                For Linux based systems, all of the following are valid: user, uid, user:group, uid:gid, uid:group, user:gid.
                If group/gid is not specified, the default group and supplementary groups of the given user/uid in /etc/passwd and /etc/group from the container are applied.
                If group/gid is specified, supplementary groups from the container are ignored.
    """,
        Required = false
    };

    internal CreateLayerTarballCommand() : base("Create a container Layer tarball out of a given set of files")
    {
        Options.Add(InputFilesOption);
        Options.Add(ContainerRootDirOption);
        Options.Add(ParentImageFormatOption);
        Options.Add(ContentStoreRootOption);
        Options.Add(TargetRuntimeIdentifierOption);
        Options.Add(GeneratedLayerPath);
        Options.Add(ContainerUserOption);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var inputFiles = parseResult.GetValue(InputFilesOption)!;
            var workingDir = parseResult.GetValue(ContainerRootDirOption)!;
            var targetRid = parseResult.GetValue(TargetRuntimeIdentifierOption)!;
            var parentImageFormat = parseResult.GetValue(ParentImageFormatOption)!;
            var contentStoreRoot = parseResult.GetValue(ContentStoreRootOption)!;
            var generatedLayerPath = parseResult.GetValue(GeneratedLayerPath)!;
            var isWindowsLayer = targetRid.StartsWith("win", StringComparison.OrdinalIgnoreCase);
            var containerUserId = isWindowsLayer ? null : ContainerBuilder.TryParseUserId(parseResult.GetValue(ContainerUserOption)!);
            if (Enum.TryParse(parentImageFormat, out KnownImageFormats format))
            {
            }
            else
            {
                format = parentImageFormat switch
                {
                    SchemaTypes.DockerManifestV2 => KnownImageFormats.Docker,
                    SchemaTypes.OciManifestV1 => KnownImageFormats.OCI,
                    _ => throw new ArgumentException(parentImageFormat),
                };
            }
            string layerMediaType = format switch
            {
                KnownImageFormats.Docker => SchemaTypes.DockerLayerGzip,
                KnownImageFormats.OCI => SchemaTypes.OciLayerGzipV1,
                _ => throw new ArgumentException(parentImageFormat),
            };
            var layer = await Layer.FromFiles(inputFiles, workingDir, isWindowsLayer, layerMediaType, new(new(contentStoreRoot)), new(generatedLayerPath), cancellationToken, userId: containerUserId);
            // Log the layer details as json output of a descriptor
            await parseResult.InvocationConfiguration.Output.WriteAsync(Json.Serialize(layer.Descriptor));
        });
    }
}
