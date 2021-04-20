// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockFileStream : MemoryStream
    {
        private readonly Action<byte[]> _onFlush;

        public MockFileStream(Action<byte[]> onFlush)
        {
            _onFlush = onFlush;
        }

        public override void Flush()
        {
            byte[] buffer = new byte[Length];
            Read(buffer, 0, buffer.Length);
            _onFlush(buffer);
        }
    }
}
