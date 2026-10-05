// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Resolves strings lazily over an indexed version-2 resource image.
/// </summary>
internal sealed class IndexedStringResourceTable : DisposableBase
{
    private readonly IStringResourceReader _reader;

    private IndexedStringResourceTable(IStringResourceReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    ///  Creates a table that owns <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">The indexed reader whose lifetime transfers to the table.</param>
    /// <param name="options">The table loading options.</param>
    /// <returns>A validated indexed string table.</returns>
    internal static IndexedStringResourceTable Create(
        IStringResourceReader reader,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            options.Validate();
            bool ignoreNonStringResources = options.AreFlagsSet(
                StringResourceManagerOptions.IgnoreNonStringResources);

            for (int i = 0; i < reader.ResourceCount; i++)
            {
                ResourceTypeCode typeCode = reader.GetResourceTypeCode(i);
                if (typeCode == ResourceTypeCode.Null)
                {
                    throw new BadImageFormatException("Null resources are not valid string resources.");
                }

                if (typeCode != ResourceTypeCode.String && !ignoreNonStringResources)
                {
                    throw new NotSupportedException("The resource table contains a non-string resource.");
                }
            }

            return new(reader);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    /// <summary>
    ///  Looks up a string without materializing unrelated resource names or values.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The string value, or <see langword="null"/> when the name is absent or not a string.</returns>
    /// <exception cref="ObjectDisposedException">The table has been disposed.</exception>
    internal string? Lookup(string name)
    {
        ObjectDisposedException.ThrowIf(Disposed, this);
        return _reader.Lookup(name);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => _reader.Dispose();
}
