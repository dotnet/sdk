// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Containers;

internal class RegistrySettings
{
    public RegistrySettings(string? registryName = null, IEnvironmentProvider? environment = null)
    {
        environment ??= new EnvironmentProvider();

        ChunkedUploadSizeBytes = environment.GetEnvironmentVariableAsNullableInt(EnvVariables.ChunkedUploadSizeBytes) ??
            environment.GetEnvironmentVariableAsNullableInt(EnvVariables.ChunkedUploadSizeBytesLegacy);
        ForceChunkedUpload = environment.GetEnvironmentVariable(EnvVariables.ForceChunkedUpload) is not null ?
            environment.GetEnvironmentVariableAsBool(EnvVariables.ForceChunkedUpload, defaultValue: false) :
            environment.GetEnvironmentVariableAsBool(EnvVariables.ForceChunkedUploadLegacy, defaultValue: false);
        ParallelUploadEnabled = environment.GetEnvironmentVariable(EnvVariables.ParallelUploadEnabled) is not null ?
            environment.GetEnvironmentVariableAsBool(EnvVariables.ParallelUploadEnabled, defaultValue: true) :
            environment.GetEnvironmentVariableAsBool(EnvVariables.ParallelUploadEnabledLegacy, defaultValue: true);

        if (registryName is not null)
        {
            IsInsecure = IsInsecureRegistry(environment, registryName);
        }
    }

    private const int DefaultChunkSizeBytes = 1024 * 64;
    private const int FiveMegs = 5_242_880;

    /// <summary>
    /// When chunking is enabled, allows explicit control over the size of the chunks uploaded
    /// </summary>
    /// <remarks>
    /// Our default of 64KB is very conservative, so raising this to 1MB or more can speed up layer uploads reasonably well.
    /// </remarks>
    internal int? ChunkedUploadSizeBytes { get; init; }

    /// <summary>
    /// Allows to force chunked upload for debugging purposes.
    /// </summary>
    internal bool ForceChunkedUpload { get; init; }

    /// <summary>
    /// Whether we should upload blobs in parallel (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Enabling this can swamp some registries, so this is an escape hatch.
    /// </remarks>
    internal bool ParallelUploadEnabled { get; init; }

    /// <summary>
    /// Allows ignoring https certificate errors and changing to http when the endpoint is not an https endpoint.
    /// </summary>
    internal bool IsInsecure { get; init; }

    internal struct EnvVariables
    {
        internal const string ChunkedUploadSizeBytes = "DOTNET_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES";
        internal const string ChunkedUploadSizeBytesLegacy = "SDK_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES";

        internal const string ForceChunkedUpload = "DOTNET_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD";
        internal const string ForceChunkedUploadLegacy = "SDK_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD";
        internal const string ParallelUploadEnabled = "DOTNET_CONTAINER_REGISTRY_PARALLEL_UPLOAD";
        internal const string ParallelUploadEnabledLegacy = "SDK_CONTAINER_REGISTRY_PARALLEL_UPLOAD";

        internal const string InsecureRegistries = "DOTNET_CONTAINER_INSECURE_REGISTRIES";
    }

    private static bool IsInsecureRegistry(IEnvironmentProvider environment, string registryName)
    {
        // Always allow insecure access to 'localhost'.
        if (registryName.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase) ||
            registryName.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // DOTNET_CONTAINER_INSECURE_REGISTRIES is a semicolon separated list of insecure registry names.
        string? insecureRegistriesEnv = environment.GetEnvironmentVariable(EnvVariables.InsecureRegistries);
        if (insecureRegistriesEnv is not null)
        {
            string[] insecureRegistries = insecureRegistriesEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (Array.Exists(insecureRegistries, registry => registryName.Equals(registry, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return DockerCli.IsInsecureRegistry(registryName);
    }
}
