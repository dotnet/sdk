// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities
{
    internal static class StreamExtensions
    {
        extension(Stream stream)
        {
            public void ReadExactly(Span<byte> buffer)
            {
                int bytesRead = 0;
                byte[] arrayBuffer = new byte[buffer.Length];
                while (bytesRead < buffer.Length)
                {
                    int read = stream.Read(arrayBuffer, bytesRead, buffer.Length - bytesRead);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("Unexpected end of stream while reading Mach-O file.");
                    }
                    bytesRead += read;
                }
                arrayBuffer.CopyTo(buffer);
            }
        }
    }
}
