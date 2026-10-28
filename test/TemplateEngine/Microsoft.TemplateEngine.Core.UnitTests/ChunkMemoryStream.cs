// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.UnitTests;

internal class ChunkMemoryStream : MemoryStream
{
    private readonly int _chunkSize;

    internal ChunkMemoryStream(int chunkSize)
    {
        _chunkSize = chunkSize;
    }

    internal ChunkMemoryStream(byte[] buffer, int chunkSize) : base(buffer)
    {
        _chunkSize = chunkSize;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count > _chunkSize)
        {
            count = _chunkSize;
        }
        return base.Read(buffer, offset, count);
    }
}
