// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Resolves strings lazily over an indexed version-2 resource image.
/// </summary>
internal sealed partial class IndexedStringResourceTable : DisposableBase
{
    private readonly IStringResourceReader _reader;
    private volatile CacheEntry? _lastEntry;
    private int _activeLookups;

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
    /// <param name="value">The string value when found.</param>
    /// <returns>The lookup result.</returns>
    internal StringResourceLookupKind Lookup(string name, out string? value)
    {
        if (Disposed)
        {
            value = null;
            return StringResourceLookupKind.Stale;
        }

        CacheEntry? entry = _lastEntry;
        if (entry is not null
            && (ReferenceEquals(entry.Name, name)
                || string.Equals(entry.Name, name, StringComparison.Ordinal)))
        {
            value = entry.Value;
            return StringResourceLookupKind.Found;
        }

        // Stream lookups mutate a shared position, so the same lock serializes lookup and disposal.
        if (_reader is StreamStringResourceReader)
        {
            lock (_reader)
            {
                if (Disposed)
                {
                    value = null;
                    return StringResourceLookupKind.Stale;
                }

                return LookupReader(name, out value);
            }
        }

        // Memory readers support concurrent lookup; the count keeps their backing alive until all
        // lookups that joined this generation have completed.
        if (!TryAcquireLookup())
        {
            value = null;
            return StringResourceLookupKind.Stale;
        }

        try
        {
            return LookupReader(name, out value);
        }
        finally
        {
            Interlocked.Decrement(ref _activeLookups);
        }
    }

    private StringResourceLookupKind LookupReader(string name, out string? value)
    {
        CacheEntry? entry = _lastEntry;
        if (entry is not null
            && (ReferenceEquals(entry.Name, name)
                || string.Equals(entry.Name, name, StringComparison.Ordinal)))
        {
            value = entry.Value;
            return StringResourceLookupKind.Found;
        }

        StringResourceLookupKind result = _reader.Lookup(name, out value);
        if (result == StringResourceLookupKind.Found && value is not null)
        {
            _lastEntry = new(name, value);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAcquireLookup()
    {
        if (Disposed)
        {
            return false;
        }

        Interlocked.Increment(ref _activeLookups);
        if (!Disposed)
        {
            return true;
        }

        Interlocked.Decrement(ref _activeLookups);
        return false;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_reader is StreamStringResourceReader)
        {
            lock (_reader)
            {
                _reader.Dispose();
            }

            return;
        }

        SpinWait spinner = new();
        while (Volatile.Read(ref _activeLookups) != 0)
        {
            spinner.SpinOnce();
        }

        _reader.Dispose();
    }
}