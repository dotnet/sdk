// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Containers.Registry;

internal class RegistrySettings
{
    private const int DefaultChunkSizeBytes = 1024 * 64;
    private const int FiveMegs = 5_242_880;

    internal struct EnvVariables
    {
        internal const string ChunkedUploadSizeBytes = "SDK_CONTAINER_REGISTRY_CHUNKED_UPLOAD_SIZE_BYTES";

        internal const string ParallelUploadEnabled = "SDK_CONTAINER_REGISTRY_PARALLEL_UPLOAD";

        internal const string ForceChunkedUpload = "SDK_CONTAINER_DEBUG_REGISTRY_FORCE_CHUNKED_UPLOAD";
    }

    /// <summary>
    /// When chunking is enabled, allows explicit control over the size of the chunks uploaded
    /// </summary>
    /// <remarks>
    /// Our default of 64KB is very conservative, so raising this to 1MB or more can speed up layer uploads reasonably well.
    /// </remarks>
    internal int? ChunkedUploadSizeBytes { get; init; } = Env.GetEnvironmentVariableAsNullableInt(EnvVariables.ChunkedUploadSizeBytes);

    /// <summary>
    /// Whether we should upload blobs in parallel (enabled by default, but disabled for certain registries in conjunction with the explicit support check below).
    /// </summary>
    /// <remarks>
    /// Enabling this can swamp some registries, so this is an escape hatch.
    /// </remarks>
    internal bool ParallelUploadEnabled { get; init; } = Env.GetEnvironmentVariableAsBool(EnvVariables.ParallelUploadEnabled, defaultValue: true);

    /// <summary>
    /// Allows to force chunked upload for debugging purposes.
    /// </summary>
    internal bool ForceChunkedUpload { get; init; } = Env.GetEnvironmentVariableAsBool(EnvVariables.ForceChunkedUpload, defaultValue: false);

    /// <summary>
    /// Computes the effective chunk size to use for the upload
    /// </summary>
    /// <remarks>
    /// The chunk size is determined by the following rules:
    /// We compare the registry's expressed chunk size with any user-provided chunk size (via env var).
    /// If both are set, we use the smaller of the two.
    /// If only one is set, we use that one.
    /// If either is zero, we use the other.
    /// If neither is set, we use the default chunk size.
    /// Finally, AWS ECR has a min size that we use to override the above if it would be below that min size.
    /// </remarks>
    internal int EffectiveChunkSize(int? registryChunkSize, bool isAWS)
    {

        int result =
            (registryChunkSize, ChunkedUploadSizeBytes) switch
            {
                (0, int u) => u,
                (int r, 0) => r,
                (int r, int u) => Math.Min(r, u),
                (int r, null) => r,
                (null, int u) => u,
                (null, null) => DefaultChunkSizeBytes
            };

        if (isAWS)
        {
            // AWS ECR requires a min chunk size of 5MB for all chunks except the last.
            return Math.Max(result, FiveMegs);
        }
        else
        {
            return result;
        }
    }
}
