// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed record FileArtifactTransferLimits(long MaximumFileBytes, long MaximumModuleBytes, int MaximumFileCount)
{
    internal const string MaximumFileBytesVariable = "DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_BYTES";
    internal const string MaximumModuleBytesVariable = "DOTNET_CLI_TEST_ARTIFACT_MAX_MODULE_BYTES";
    internal const string MaximumFileCountVariable = "DOTNET_CLI_TEST_ARTIFACT_MAX_FILE_COUNT";
    internal const int MaximumChunkBytes = FileArtifactTransferRequest.MaximumChunkBytes;
    internal static FileArtifactTransferLimits Default { get; } = new(1024L * 1024 * 1024, 4L * 1024 * 1024 * 1024, 10_000);

    internal static FileArtifactTransferLimits FromEnvironment()
        => Parse(
            Environment.GetEnvironmentVariable(MaximumFileBytesVariable),
            Environment.GetEnvironmentVariable(MaximumModuleBytesVariable),
            Environment.GetEnvironmentVariable(MaximumFileCountVariable));

    internal static FileArtifactTransferLimits Parse(string? fileBytes, string? moduleBytes, string? fileCount)
        => new(
            ParseLimit(fileBytes, MaximumFileBytesVariable, Default.MaximumFileBytes, long.MaxValue),
            ParseLimit(moduleBytes, MaximumModuleBytesVariable, Default.MaximumModuleBytes, long.MaxValue),
            (int)ParseLimit(fileCount, MaximumFileCountVariable, Default.MaximumFileCount, int.MaxValue));

    private static long ParseLimit(string? value, string name, long defaultValue, long maximumValue)
    {
        if (value is null)
        {
            return defaultValue;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
            parsed <= 0 || parsed > maximumValue)
        {
            throw new GracefulException(string.Format(CliCommandStrings.ArtifactTransferInvalidLimit, name, maximumValue));
        }

        return parsed;
    }
}
