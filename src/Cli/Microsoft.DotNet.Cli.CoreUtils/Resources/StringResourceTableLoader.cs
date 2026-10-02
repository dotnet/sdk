// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Loads intrinsic string resources from binary <c>.resources</c> data or a managed assembly.
/// </summary>
/// <remarks>
///  <para>
///   The returned dictionary uses ordinal, case-sensitive keys. Null resources always reject the
///   table. By default non-string resources also reject the table;
///   <see cref="StringResourceManagerOptions.IgnoreNonStringResources"/> skips them without retaining
///   their names or values.
///  </para>
///  <para>
///   Resource data is expected to be a trusted application build or deployment artifact. Loaded
///   assembly and stream overloads parse the original backing directly and never copy the complete
///   stream merely to obtain random access.
///  </para>
/// </remarks>
internal static class StringResourceTableLoader
{
    /// <inheritdoc cref="LoadStringTableFromAssembly(Assembly, string, StringResourceManagerOptions)"/>
    public static Dictionary<string, string>? LoadStringTableFromAssembly(
        Assembly assembly,
        string resourceName) => LoadStringTableFromAssembly(
            assembly,
            resourceName,
            StringResourceManagerOptions.None);

    /// <summary>
    ///  Opens an embedded resource as an indexed string table without copying its stream.
    /// </summary>
    /// <param name="assembly">The loaded assembly containing the resource.</param>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The string resource options.</param>
    /// <returns>The indexed table, or <see langword="null"/> when the resource is absent.</returns>
    internal static IndexedStringResourceTable? LoadIndexedTableFromAssembly(
        Assembly assembly,
        string resourceName,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(resourceName);
        options.Validate();

        Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        try
        {
            return LoadIndexedTableFromResourcesStream(stream, options);
        }
        catch (ArgumentException ex)
        {
            throw new BadImageFormatException("The embedded resource is not a valid .resources file.", ex);
        }
    }

    /// <summary>
    ///  Loads an indexed table from a managed assembly image and takes ownership of its backing.
    /// </summary>
    /// <param name="assembly">The complete managed assembly image.</param>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The string resource options.</param>
    /// <param name="owned">The owner that keeps <paramref name="assembly"/> valid.</param>
    /// <returns>The indexed table, or <see langword="null"/> when the resource is absent.</returns>
    internal static unsafe IndexedStringResourceTable? LoadIndexedTableFromAssembly(
        ReadOnlyMemory<byte> assembly,
        string resourceName,
        StringResourceManagerOptions options,
        IDisposable owned)
    {
        (
            IndexedStringResourceTable? table,
            _
        ) = LoadIndexedTableFromAssemblyCore(
            assembly,
            resourceName,
            options,
            owned,
            readResourceAssemblyMetadata: false,
            expectedResourceAssembly: null,
            expectedCultureName: null);

        return table;
    }

    /// <summary>
    ///  Loads an indexed table and owner metadata from a managed resource assembly image.
    /// </summary>
    /// <param name="assembly">The complete managed assembly image.</param>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The string resource options.</param>
    /// <param name="owned">The owner that keeps <paramref name="assembly"/> valid.</param>
    /// <returns>The indexed table and resource assembly metadata.</returns>
    internal static (
        IndexedStringResourceTable? Table,
        SatelliteStringResourceSourceMetadata Metadata
    ) LoadIndexedTableAndMetadataFromAssembly(
        ReadOnlyMemory<byte> assembly,
        string resourceName,
        StringResourceManagerOptions options,
        IDisposable owned)
    {
        (
            IndexedStringResourceTable? table,
            SatelliteStringResourceSourceMetadata? metadata
        ) = LoadIndexedTableFromAssemblyCore(
            assembly,
            resourceName,
            options,
            owned,
            readResourceAssemblyMetadata: true,
            expectedResourceAssembly: null,
            expectedCultureName: null);

        return (
            table,
            metadata
                ?? throw new InvalidOperationException("The resource assembly metadata was not loaded.")
        );
    }

    /// <summary>
    ///  Loads and validates an indexed table from a satellite assembly image.
    /// </summary>
    /// <param name="assembly">The complete satellite assembly image.</param>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The string resource options.</param>
    /// <param name="owned">The owner that keeps <paramref name="assembly"/> valid.</param>
    /// <param name="resourceAssembly">The expected resource-owning assembly metadata.</param>
    /// <param name="cultureName">The expected satellite culture.</param>
    /// <returns>The indexed table, or <see langword="null"/> when the resource is absent.</returns>
    internal static IndexedStringResourceTable? LoadIndexedTableFromSatelliteAssembly(
        ReadOnlyMemory<byte> assembly,
        string resourceName,
        StringResourceManagerOptions options,
        IDisposable owned,
        SatelliteStringResourceSourceMetadata resourceAssembly,
        string cultureName)
    {
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ArgumentNullException.ThrowIfNull(cultureName);
        (
            IndexedStringResourceTable? table,
            _
        ) = LoadIndexedTableFromAssemblyCore(
            assembly,
            resourceName,
            options,
            owned,
            readResourceAssemblyMetadata: false,
            resourceAssembly,
            cultureName);

        return table;
    }

    private static unsafe (
        IndexedStringResourceTable? Table,
        SatelliteStringResourceSourceMetadata? Metadata
    ) LoadIndexedTableFromAssemblyCore(
        ReadOnlyMemory<byte> assembly,
        string resourceName,
        StringResourceManagerOptions options,
        IDisposable owned,
        bool readResourceAssemblyMetadata,
        SatelliteStringResourceSourceMetadata? expectedResourceAssembly,
        string? expectedCultureName)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        ArgumentNullException.ThrowIfNull(owned);
        bool ownershipTransferred = false;
        try
        {
            options.Validate();
            if (assembly.IsEmpty)
            {
                throw new BadImageFormatException("The assembly image is empty.");
            }

            using MemoryHandle pinnedAssembly = assembly.Pin();
            using PEReader peReader = new((byte*)pinnedAssembly.Pointer, assembly.Length);
            if (!peReader.HasMetadata)
            {
                throw new BadImageFormatException("The assembly does not contain managed metadata.");
            }

            MetadataReader metadataReader = peReader.GetMetadataReader();
            SatelliteStringResourceSourceMetadata? resourceAssemblyMetadata = readResourceAssemblyMetadata
                ? ManagedAssemblyMetadataReader.ReadResourceAssembly(metadataReader)
                : null;

            if (expectedResourceAssembly is not null)
            {
                ManagedAssemblyIdentity expectedIdentity = expectedResourceAssembly.ResourceAssemblyIdentity
                    ?? throw new InvalidOperationException(
                        "The resource-owning assembly identity was not initialized.");

                ManagedAssemblyIdentity satelliteIdentity = ManagedAssemblyIdentity.FromMetadata(metadataReader);
                satelliteIdentity.ValidateSatelliteOf(
                    expectedIdentity,
                    expectedCultureName
                        ?? throw new InvalidOperationException("The expected satellite culture was not initialized."),
                    expectedResourceAssembly.SatelliteContractVersion);
            }

            foreach (ManifestResourceHandle handle in metadataReader.ManifestResources)
            {
                ManifestResource resource = metadataReader.GetManifestResource(handle);
                if (!resource.Implementation.IsNil
                    || !metadataReader.StringComparer.Equals(resource.Name, resourceName))
                {
                    continue;
                }

                ReadOnlyMemory<byte> resources = GetEmbeddedResource(assembly, peReader.PEHeaders, resource.Offset);
                ownershipTransferred = true;
                RawResourceReader reader = RawResourceReader.CreateOwned(resources, owned);
                return (IndexedStringResourceTable.Create(reader, options), resourceAssemblyMetadata);
            }

            return (null, resourceAssemblyMetadata);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                owned.Dispose();
            }
        }
    }

    /// <summary>
    ///  Loads an indexed resource image and takes ownership of its backing.
    /// </summary>
    /// <param name="resources">The resource image.</param>
    /// <param name="options">The string resource options.</param>
    /// <param name="owned">The owner that keeps <paramref name="resources"/> valid.</param>
    /// <returns>The indexed string table.</returns>
    internal static IndexedStringResourceTable LoadIndexedTableFromResourcesFile(
        ReadOnlyMemory<byte> resources,
        StringResourceManagerOptions options,
        IDisposable owned) => IndexedStringResourceTable.Create(
            RawResourceReader.CreateOwned(resources, owned),
            options);

    /// <summary>
    ///  Loads an indexed table directly from the current stream position and takes ownership of the
    ///  stream.
    /// </summary>
    /// <param name="stream">The resource stream.</param>
    /// <param name="options">The string resource options.</param>
    /// <returns>The indexed string table.</returns>
    internal static IndexedStringResourceTable LoadIndexedTableFromResourcesStream(
        Stream stream,
        StringResourceManagerOptions options) =>
            IndexedStringResourceTable.Create(CreateStringResourceReader(stream), options);

    /// <summary>
    ///  Loads the string table stored in the named embedded resource of an already-loaded assembly.
    /// </summary>
    /// <param name="assembly">The loaded assembly containing the resource.</param>
    /// <param name="resourceName">The manifest name of the embedded <c>.resources</c> payload.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>
    ///  The ordinal string table, or <see langword="null"/> when the assembly does not contain an
    ///  embedded resource with the given name.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="assembly"/> or <paramref name="resourceName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    /// <exception cref="BadImageFormatException">The embedded resource has an invalid structure.</exception>
    /// <exception cref="NotSupportedException">The embedded resource format is not supported.</exception>
    public static Dictionary<string, string>? LoadStringTableFromAssembly(
        Assembly assembly,
        string resourceName,
        StringResourceManagerOptions options) => LoadTableFromAssembly(assembly, resourceName, options)?.Strings;

    /// <inheritdoc cref="LoadStringTableFromAssembly(ReadOnlyMemory{byte}, string, StringResourceManagerOptions)"/>
    public static Dictionary<string, string>? LoadStringTableFromAssembly(
        ReadOnlyMemory<byte> assembly,
        string resourceName) => LoadStringTableFromAssembly(
            assembly,
            resourceName,
            StringResourceManagerOptions.None);

    /// <summary>
    ///  Loads the string table from an embedded assembly resource.
    /// </summary>
    /// <param name="assembly">The loaded assembly containing the resource.</param>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>The table, or <see langword="null"/> when the resource is absent.</returns>
    internal static StringResourceTable? LoadTableFromAssembly(
        Assembly assembly,
        string resourceName,
        StringResourceManagerOptions options = StringResourceManagerOptions.None)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(resourceName);
        options.Validate();

        Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        try
        {
            using IStringResourceReader reader = CreateStringResourceReader(stream);
            return LoadTableFromReader(reader, options);
        }
        catch (ArgumentException ex)
        {
            throw new BadImageFormatException("The embedded resource is not a valid .resources file.", ex);
        }
    }

    /// <summary>
    ///  Loads the string table stored in the named embedded resource of a managed assembly image.
    /// </summary>
    /// <param name="assembly">The complete bytes of the managed assembly.</param>
    /// <param name="resourceName">The manifest name of the embedded <c>.resources</c> payload.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>
    ///  The ordinal string table, or <see langword="null"/> when the assembly does not contain an
    ///  embedded resource with the given name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="resourceName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    /// <exception cref="BadImageFormatException">
    ///  The assembly or embedded resource has an invalid structure.
    /// </exception>
    /// <exception cref="NotSupportedException">The embedded resource format is not supported.</exception>
    public static Dictionary<string, string>? LoadStringTableFromAssembly(
        ReadOnlyMemory<byte> assembly,
        string resourceName,
        StringResourceManagerOptions options) => LoadTableFromAssembly(assembly, resourceName, options)?.Strings;

    /// <inheritdoc cref="LoadStringTableFromResourcesFile(ReadOnlyMemory{byte}, StringResourceManagerOptions)"/>
    public static Dictionary<string, string> LoadStringTableFromResourcesFile(
        ReadOnlyMemory<byte> resources) => LoadStringTableFromResourcesFile(
            resources,
            StringResourceManagerOptions.None);

    /// <summary>
    ///  Loads the string table from a managed assembly image.
    /// </summary>
    /// <param name="assembly">The complete managed assembly image.</param>
    /// <param name="resourceName">The exact manifest resource name.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>The table, or <see langword="null"/> when the resource is absent.</returns>
    internal static unsafe StringResourceTable? LoadTableFromAssembly(
        ReadOnlyMemory<byte> assembly,
        string resourceName,
        StringResourceManagerOptions options = StringResourceManagerOptions.None)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        options.Validate();
        if (assembly.IsEmpty)
        {
            throw new BadImageFormatException("The assembly image is empty.");
        }

        using MemoryHandle pinnedAssembly = assembly.Pin();
        using PEReader peReader = new((byte*)pinnedAssembly.Pointer, assembly.Length);

        if (!peReader.HasMetadata)
        {
            throw new BadImageFormatException("The assembly does not contain managed metadata.");
        }

        MetadataReader metadataReader = peReader.GetMetadataReader();
        foreach (ManifestResourceHandle handle in metadataReader.ManifestResources)
        {
            ManifestResource resource = metadataReader.GetManifestResource(handle);
            if (!resource.Implementation.IsNil
                || !metadataReader.StringComparer.Equals(resource.Name, resourceName))
            {
                continue;
            }

            ReadOnlyMemory<byte> resources = GetEmbeddedResource(assembly, peReader.PEHeaders, resource.Offset);
            return LoadEmbeddedTable(resources, options);
        }

        return null;
    }

    private static StringResourceTable LoadEmbeddedTable(
        ReadOnlyMemory<byte> resources,
        StringResourceManagerOptions options)
    {
        try
        {
            return LoadTableFromResourcesFile(resources, options);
        }
        catch (ArgumentException ex)
        {
            throw new BadImageFormatException("The embedded resource is not a valid .resources file.", ex);
        }
    }

    /// <summary>
    ///  Loads the intrinsic strings from a binary <c>.resources</c> file image.
    /// </summary>
    /// <param name="resources">The complete bytes of a default-format version 2 <c>.resources</c> file.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>An ordinal dictionary containing every intrinsic string resource.</returns>
    /// <exception cref="ArgumentException">The data is not a <c>.resources</c> file.</exception>
    /// <exception cref="BadImageFormatException">
    ///  The resource data has an invalid structure or contains duplicate names.
    /// </exception>
    /// <exception cref="NotSupportedException">The resource format is not supported.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    [SkipLocalsInit]
    public static Dictionary<string, string> LoadStringTableFromResourcesFile(
        ReadOnlyMemory<byte> resources,
        StringResourceManagerOptions options) => LoadTableFromResourcesFile(resources, options).Strings;

    /// <summary>
    ///  Loads a string table from a binary resource image.
    /// </summary>
    /// <param name="resources">The complete binary resource image.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>The loaded table.</returns>
    [SkipLocalsInit]
    internal static StringResourceTable LoadTableFromResourcesFile(
        ReadOnlyMemory<byte> resources,
        StringResourceManagerOptions options)
    {
        options.Validate();
        using RawResourceReader reader = new(resources);
        return LoadTableFromReader(reader, options);
    }

    private static StringResourceTable LoadTableFromReader(
        IStringResourceReader reader,
        StringResourceManagerOptions options)
    {
        options.Validate();
        bool ignoreNonStringResources = options.AreFlagsSet(
            StringResourceManagerOptions.IgnoreNonStringResources);

        Dictionary<string, string> table = [with(StringComparer.Ordinal)];

        for (int i = 0; i < reader.ResourceCount; i++)
        {
            ResourceTypeCode typeCode = reader.GetResourceTypeCode(i);
            if (typeCode == ResourceTypeCode.Null)
            {
                throw new BadImageFormatException("Null resources are not valid string resources.");
            }

            if (typeCode != ResourceTypeCode.String)
            {
                if (ignoreNonStringResources)
                {
                    continue;
                }

                throw new NotSupportedException("The resource table contains a non-string resource.");
            }

            string name = reader.GetResourceName(i);
            try
            {
                table.Add(name, reader.GetString(i));
            }
            catch (ArgumentException ex)
            {
                throw new BadImageFormatException($"The resource name '{name}' is duplicated.", ex);
            }
        }

        return new(table);
    }

    private static unsafe IStringResourceReader CreateStringResourceReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        bool ownershipTransferred = false;
        try
        {
            if (stream is UnmanagedMemoryStream unmanagedStream)
            {
                long remaining = unmanagedStream.Length - unmanagedStream.Position;
                if (remaining is < 0 or > int.MaxValue)
                {
                    throw new BadImageFormatException("The resource stream is too large.");
                }

                BorrowedMemoryManager memory = new(unmanagedStream.PositionPointer, (int)remaining);
                ownershipTransferred = true;
                return RawResourceReader.CreateOwned(memory.Memory, stream);
            }

            if (stream is MemoryStream memoryStream
                && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                byte[]? bytes = buffer.Array;
                if (bytes is not null)
                {
                    long remaining = memoryStream.Length - memoryStream.Position;
                    if (remaining is < 0 or > int.MaxValue)
                    {
                        throw new BadImageFormatException("The resource stream is too large.");
                    }

                    int offset = checked(buffer.Offset + (int)memoryStream.Position);
                    ReadOnlyMemory<byte> resources = new(bytes, offset, (int)remaining);
                    ownershipTransferred = true;
                    return RawResourceReader.CreateOwned(resources, stream);
                }
            }

            ownershipTransferred = true;
            return new StreamStringResourceReader(stream);
        }
        catch
        {
            if (!ownershipTransferred)
            {
                stream.Dispose();
            }

            throw;
        }
    }

    private static ReadOnlyMemory<byte> GetEmbeddedResource(
        ReadOnlyMemory<byte> image,
        PEHeaders headers,
        long relativeOffset)
    {
        CorHeader corHeader = headers.CorHeader
            ?? throw new BadImageFormatException("The assembly does not contain a CLR header.");

        DirectoryEntry resourceDirectory = corHeader.ResourcesDirectory;
        if (resourceDirectory.Size < sizeof(int)
            || !headers.TryGetDirectoryOffset(resourceDirectory, out int resourceDirectoryOffset)
            || resourceDirectoryOffset < 0
            || resourceDirectory.Size > image.Length - resourceDirectoryOffset
            || !IsContainedInRawSection(headers, resourceDirectory, resourceDirectoryOffset))
        {
            throw new BadImageFormatException("The assembly has an invalid resource directory.");
        }

        if (relativeOffset < 0 || relativeOffset > resourceDirectory.Size - sizeof(int))
        {
            throw new BadImageFormatException("The embedded resource offset is out of range.");
        }

        int resourceOffset = (int)relativeOffset;
        int lengthOffset = resourceDirectoryOffset + resourceOffset;
        int resourceLength = BinaryPrimitives.ReadInt32LittleEndian(image.Span.Slice(lengthOffset, sizeof(int)));
        int contentOffset = lengthOffset + sizeof(int);
        if (resourceLength < 0
            || resourceLength > resourceDirectory.Size - resourceOffset - sizeof(int)
            || resourceLength > image.Length - contentOffset)
        {
            throw new BadImageFormatException("The embedded resource length is out of range.");
        }

        return image.Slice(contentOffset, resourceLength);
    }

    private static bool IsContainedInRawSection(
        PEHeaders headers,
        DirectoryEntry directory,
        int directoryOffset)
    {
        foreach (SectionHeader section in headers.SectionHeaders)
        {
            long sectionRelativeOffset = (long)directory.RelativeVirtualAddress - section.VirtualAddress;
            if (sectionRelativeOffset < 0
                || sectionRelativeOffset > section.SizeOfRawData
                || section.PointerToRawData + sectionRelativeOffset != directoryOffset)
            {
                continue;
            }

            return directory.Size <= section.SizeOfRawData - sectionRelativeOffset;
        }

        return false;
    }
}
