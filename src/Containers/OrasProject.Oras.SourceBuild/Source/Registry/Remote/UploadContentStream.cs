// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/UploadContentStream.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.Oras.Registry.Remote;

/// <summary>
/// Exposes a caller-owned upload stream from its current position without disposing it.
/// Position zero maps to the caller's starting position, because an authentication replay
/// rewinds request content to zero and must resend the blob rather than earlier bytes.
/// </summary>
internal sealed class UploadContentStream : Stream
{
    private readonly Stream _inner;
    private readonly long _origin;

    public UploadContentStream(Stream inner)
    {
        _inner = inner;
        _origin = inner.CanSeek ? inner.Position : 0;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length - _origin;

    public override long Position
    {
        get => _inner.Position - _origin;
        set => _inner.Position = checked(_origin + value);
    }

    public override long Seek(long offset, SeekOrigin origin) => origin == SeekOrigin.Begin
        ? _inner.Seek(checked(_origin + offset), SeekOrigin.Begin) - _origin
        : _inner.Seek(offset, origin) - _origin;

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
        _inner.CopyToAsync(destination, bufferSize, cancellationToken);

    public override void Flush()
    {
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
