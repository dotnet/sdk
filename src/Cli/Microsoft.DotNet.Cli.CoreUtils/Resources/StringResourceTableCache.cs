// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.ExceptionServices;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  A resource table published for one cache generation.
/// </summary>
internal sealed class StringResourceTableCache
{
    private readonly IndexedStringResourceTable? _table;
    private readonly ExceptionDispatchInfo? _loadFailure;

    /// <summary>
    ///  Initializes a generation-specific cache entry.
    /// </summary>
    /// <param name="generation">The generation that loaded the table.</param>
    /// <param name="table">The loaded resource table.</param>
    internal StringResourceTableCache(int generation, IndexedStringResourceTable table)
    {
        Generation = generation;
        _table = table;
    }

    /// <summary>
    ///  Initializes a generation-specific failed cache entry.
    /// </summary>
    /// <param name="generation">The generation that attempted to load the table.</param>
    /// <param name="loadFailure">The source load failure.</param>
    internal StringResourceTableCache(int generation, Exception loadFailure)
    {
        Generation = generation;
        _loadFailure = ExceptionDispatchInfo.Capture(loadFailure);
    }

    /// <summary>
    ///  The generation that loaded the table.
    /// </summary>
    internal int Generation { get; }

    /// <summary>
    ///  The loaded resource table.
    /// </summary>
    internal IndexedStringResourceTable Table
    {
        get
        {
            _loadFailure?.Throw();
            return _table
                ?? throw new InvalidOperationException("The resource table cache is invalid.");
        }
    }

    /// <summary>
    ///  Releases the table backing, if loading succeeded.
    /// </summary>
    internal void Release() => _table?.Dispose();
}