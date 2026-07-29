// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Holds current and previous cache state used while planning a file-based application build or launch.
/// </summary>
internal sealed class FileBasedAppCacheInfo
{
    /// <summary>Gets the entry-point file metadata.</summary>
    public required FileInfo EntryPointFile { get; init; }

    /// <summary>Gets or sets whether deserialization of the previous cache entry has already been attempted.</summary>
    public bool TriedDeserializingPreviousEntry { get; set; }

    /// <summary>Gets or sets the previous successful cache entry.</summary>
    public RunFileBuildCacheEntry? PreviousEntry { get; set; }

    /// <summary>Gets the cache entry being computed for the current invocation.</summary>
    public required RunFileBuildCacheEntry CurrentEntry { get; init; }

    /// <summary>Gets or sets an implicit file whose presence requires MSBuild.</summary>
    public string? ExampleMSBuildFile { get; set; }

    /// <summary>Gets or sets whether existing direct-compilation auxiliary files remain reusable.</summary>
    public bool InitialCanReuseAuxiliaryFiles { get; set; } = true;

    /// <summary>Gets or sets whether direct compilation can replay arguments from the previous build.</summary>
    public bool CanUseCscViaPreviousArguments { get; set; }

    /// <summary>
    /// Determines whether synthetic direct-compilation auxiliary files can be reused.
    /// </summary>
    /// <param name="report">Receives the reuse decision.</param>
    /// <returns><see langword="true"/> when the auxiliary files can be reused; otherwise, <see langword="false"/>.</returns>
    public bool DetermineFinalCanReuseAuxiliaryFiles(Action<string> report)
    {
        if (PreviousEntry?.CscArguments.IsDefaultOrEmpty == false)
        {
            return false;
        }

        if (!InitialCanReuseAuxiliaryFiles)
        {
            report("CSC auxiliary files can NOT be reused due to the same reason build is needed.");
            return false;
        }

        if (PreviousEntry?.BuildLevel != BuildLevel.Csc)
        {
            report("CSC auxiliary files can NOT be reused because previous build level was not CSC " +
                $"(it was {PreviousEntry?.BuildLevel.ToString() ?? "N/A"}).");
            return false;
        }

        report("CSC auxiliary files can be reused.");
        return true;
    }
}
