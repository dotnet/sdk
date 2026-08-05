// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Windows.Win32.Foundation;

internal static unsafe class IID
{
    private static ref readonly Guid IID_NULL
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> data =
            [
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            ];

            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(data));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid Get<T>() where T : unmanaged, IComIID
    {
#if NETFRAMEWORK
        // In .NET Framework we need to use the interface to get the Guid.
        return default(T).Guid;
#else
        return T.Guid;
#endif
    }

    /// <summary>
    ///  Empty <see cref="Guid"/>.
    /// </summary>
    public static Guid* Empty() => (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in IID_NULL));
}
