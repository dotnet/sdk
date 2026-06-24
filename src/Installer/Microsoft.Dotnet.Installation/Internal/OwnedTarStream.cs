// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// A lightweight disposable wrapper around a <see cref="Stream"/> used during tar extraction.
/// When the wrapper owns the stream (e.g. a GZipStream decompression layer), disposing the
/// wrapper disposes the stream. When it does not own the stream (the raw archive stream shared
/// across multiple passes), disposing the wrapper is a no-op, keeping the underlying stream open.
/// </summary>
internal readonly struct OwnedTarStream : IDisposable
{
    private readonly bool _ownsStream;

    public Stream Stream { get; }

    public OwnedTarStream(Stream stream, bool ownsStream = true)
    {
        Stream = stream;
        _ownsStream = ownsStream;
    }

    public void Dispose()
    {
        if (_ownsStream)
        {
            Stream.Dispose();
        }
    }
}
