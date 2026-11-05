// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;
using System.Threading;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Opens one managed resource assembly as a memory-mapped data source.
/// </summary>
internal sealed class ManagedAssemblyStringResourceSource
{
    private readonly Func<string, MappedMemoryManager> _openFile;
    private SatelliteStringResourceSourceMetadata? _metadata;

    /// <summary>
    ///  Initializes a managed assembly file source.
    /// </summary>
    /// <param name="assemblyFile">The managed assembly file.</param>
    /// <param name="openFile">The function that memory-maps the file.</param>
    internal ManagedAssemblyStringResourceSource(
        string assemblyFile,
        Func<string, MappedMemoryManager> openFile)
    {
        ArgumentNullException.ThrowIfNull(assemblyFile);
        ArgumentNullException.ThrowIfNull(openFile);
        AssemblyFile = Path.GetFullPath(assemblyFile);
        _openFile = openFile;
    }

    /// <summary>
    ///  The fully qualified managed assembly file.
    /// </summary>
    internal string AssemblyFile { get; }

    /// <summary>
    ///  The metadata read during the most recent successful assembly parse.
    /// </summary>
    internal SatelliteStringResourceSourceMetadata Metadata =>
        Volatile.Read(ref _metadata)
            ?? throw new InvalidOperationException("The managed assembly metadata has not been loaded.");

    /// <summary>
    ///  Loads the named resource table and transfers the mapped file to it.
    /// </summary>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The string resource options.</param>
    /// <returns>The indexed string table.</returns>
    internal IndexedStringResourceTable LoadTable(
        string resourceName,
        StringResourceManagerOptions options)
    {
        MappedMemoryManager assemblyFile = _openFile(AssemblyFile);
        (
            IndexedStringResourceTable? table,
            SatelliteStringResourceSourceMetadata metadata
        ) = StringResourceTableLoader.LoadIndexedTableAndMetadataFromAssembly(
            assemblyFile.Memory,
            resourceName,
            options,
            assemblyFile);

        Volatile.Write(ref _metadata, metadata);
        return table
            ?? throw new MissingManifestResourceException(
                $"The assembly '{AssemblyFile}' does not contain '{resourceName}'.");
    }
}
