// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Holds cache state needed while computing the current entry and selecting later build or launch stages.
/// </summary>
internal sealed class FileBasedAppCacheInfo
{
    /// <summary>Gets the entry-point file used when comparing source metadata with cache timestamps.</summary>
    public required FileInfo EntryPointFile { get; init; }

    /// <summary>
    /// If <see cref="PreviousEntry"/> is <see langword="null"/> and this is
    /// <see langword="true"/>, the previous entry could not be deserialized,
    /// so deserialization should not be attempted again.
    /// </summary>
    public bool TriedDeserializingPreviousEntry { get; set; }

    /// <summary>Gets or sets the previous successfully deserialized cache entry.</summary>
    public RunFileBuildCacheEntry? PreviousEntry { get; set; }

    /// <summary>Gets the cache entry assembled from the current invocation inputs.</summary>
    public required RunFileBuildCacheEntry CurrentEntry { get; init; }

    /// <summary>
    /// Gets or sets the first current implicit build file whose presence requires MSBuild.
    /// </summary>
    public string? ExampleMSBuildFile { get; set; }

    /// <summary>
    /// Gets or sets whether auxiliary direct-compilation files remain reusable after initial cache validation.
    /// SDK or runtime version changes, for example, set this to <see langword="false"/>.
    /// </summary>
    public bool InitialCanReuseAuxiliaryFiles { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the current source change can replay compiler arguments from the previous build.
    /// This value is set while determining whether a build is needed.
    /// </summary>
    public bool CanUseCscViaPreviousArguments { get; set; }

    /// <summary>
    /// Determines whether synthetic direct-compilation auxiliary files can be reused.
    /// </summary>
    /// <returns><see langword="true"/> when the auxiliary files can be reused; otherwise, <see langword="false"/>.</returns>
    public bool DetermineFinalCanReuseAuxiliaryFiles()
    {
        if (PreviousEntry?.CscArguments.IsDefaultOrEmpty == false)
        {
            return false;
        }

        if (!InitialCanReuseAuxiliaryFiles)
        {
            Reporter.Verbose.WriteLine("CSC auxiliary files can NOT be reused due to the same reason build is needed.");
            return false;
        }

        if (PreviousEntry?.BuildLevel != BuildLevel.Csc)
        {
            Reporter.Verbose.WriteLine("CSC auxiliary files can NOT be reused because previous build level was not CSC " +
                $"(it was {PreviousEntry?.BuildLevel.ToString() ?? "N/A"}).");
            return false;
        }

        Reporter.Verbose.WriteLine("CSC auxiliary files can be reused.");
        return true;
    }
}
