// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Provides indexed string lookup over an owned resource backing.
/// </summary>
internal interface IStringResourceReader : IDisposable
{
    /// <summary>
    ///  The number of resources in the table.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    int ResourceCount { get; }

    /// <summary>
    ///  Gets the stored type of a resource without materializing its name or value.
    /// </summary>
    /// <param name="index">The zero-based resource index.</param>
    /// <returns>The stored resource type.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="BadImageFormatException">The resource data is malformed.</exception>
    /// <exception cref="IOException">The resource backing cannot be read.</exception>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    ResourceTypeCode GetResourceTypeCode(int index);

    /// <summary>
    ///  Decodes the resource name at an index.
    /// </summary>
    /// <param name="index">The zero-based resource index.</param>
    /// <returns>The resource name.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="BadImageFormatException">The resource data is malformed.</exception>
    /// <exception cref="IOException">The resource backing cannot be read.</exception>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    string GetResourceName(int index);

    /// <summary>
    ///  Decodes the intrinsic string at an index.
    /// </summary>
    /// <param name="index">The zero-based resource index.</param>
    /// <returns>The string value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <exception cref="BadImageFormatException">The resource data is malformed.</exception>
    /// <exception cref="InvalidOperationException">The resource is not a string.</exception>
    /// <exception cref="IOException">The resource backing cannot be read.</exception>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    string GetString(int index);

    /// <summary>
    ///  Looks up and decodes one intrinsic string.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Implementations are responsible for supporting concurrent calls to this method.
    ///   Any synchronization required by the backing belongs to the reader.
    ///  </para>
    ///  <para>
    ///   Callers must coordinate disposal with active lookups. Supporting concurrent lookup does
    ///   not imply that disposal may overlap reader use.
    ///  </para>
    /// </remarks>
    /// <param name="name">The resource name.</param>
    /// <returns>The decoded string, or <see langword="null"/> when the name is absent or not a string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="BadImageFormatException">The resource data is malformed.</exception>
    /// <exception cref="IOException">The resource backing cannot be read.</exception>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    string? Lookup(string name);
}
