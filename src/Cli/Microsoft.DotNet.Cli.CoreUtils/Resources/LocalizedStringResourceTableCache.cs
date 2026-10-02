// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.ExceptionServices;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Stores a localized table, a missing-source result, or a cached load failure.
/// </summary>
internal sealed class LocalizedStringResourceTableCache
{
    private readonly IndexedStringResourceTable? _table;
    private readonly ExceptionDispatchInfo? _loadFailure;

    /// <summary>
    ///  Initializes a successful or missing-source cache entry.
    /// </summary>
    /// <param name="table">The loaded table, or <see langword="null"/> when the source is absent.</param>
    internal LocalizedStringResourceTableCache(IndexedStringResourceTable? table)
    {
        _table = table;
    }

    /// <summary>
    ///  Initializes a failed cache entry.
    /// </summary>
    /// <param name="loadFailure">The source load failure.</param>
    internal LocalizedStringResourceTableCache(Exception loadFailure)
    {
        _loadFailure = ExceptionDispatchInfo.Capture(loadFailure);
    }

    /// <summary>
    ///  Gets the loaded table or rethrows the cached failure.
    /// </summary>
    internal IndexedStringResourceTable? Table
    {
        get
        {
            _loadFailure?.Throw();
            return _table;
        }
    }

    /// <summary>
    ///  Releases the table backing when loading succeeded.
    /// </summary>
    internal void Release() => _table?.Dispose();
}
