// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Utilities
{
    internal static class BitConverterExtensions
    {
        extension(BitConverter)
        {
            public static uint ToUInt32(ReadOnlySpan<byte> value)
            {
                var buffer = new byte[4];
                value.CopyTo(buffer);
                return BitConverter.ToUInt32(buffer, 0);
            }
        }
    }
}
