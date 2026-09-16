// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests.Utilities;

internal sealed class IdentityTestStream(byte[] bytes, int maximumRead, bool failAtEnd = false) : MemoryStream(bytes)
{
    public override bool CanSeek => false;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (failAtEnd && Position == Length)
        {
            throw new IOException("Injected identity read failure.");
        }

        return base.Read(buffer, offset, Math.Min(maximumRead, count));
    }
}