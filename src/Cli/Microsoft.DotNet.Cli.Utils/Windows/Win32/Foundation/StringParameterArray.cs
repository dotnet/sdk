// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Windows.Win32.Foundation;

/// <summary>
///  Helper to allow pinning an array of strings to pass to a native method that takes an
///  array of null-terminated UTF-16 strings.
/// </summary>
public readonly unsafe ref struct StringParameterArray
{
    private readonly GCHandle[]? _pins;
    private readonly nint[]? _param;

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringParameterArray"/> struct.
    /// </summary>
    public StringParameterArray(string[]? values)
    {
        int length = values?.Length ?? 0;
        if (length > 0)
        {
            _param = new nint[length];
            _pins = new GCHandle[length + 1];
            for (int i = 0; i < length; i++)
            {
                _pins[i] = GCHandle.Alloc(values![i], GCHandleType.Pinned);
                _param[i] = _pins[i].AddrOfPinnedObject();
            }

            _pins[length] = GCHandle.Alloc(_param, GCHandleType.Pinned);
        }
    }

    /// <summary>
    ///  Converts the <see cref="StringParameterArray"/> to a pointer to an array of null-terminated UTF-16 strings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator char**(in StringParameterArray array)
        => array._param is null ? null : (char**)Unsafe.AsPointer(ref Unsafe.AsRef(in array._param[0]));

    /// <summary>
    ///  Converts the <see cref="StringParameterArray"/> to a pointer to an array of null-terminated UTF-8 strings.
    /// </summary>
    /// <param name="array"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator sbyte**(in StringParameterArray array)
        => array._param is null ? null : (sbyte**)Unsafe.AsPointer(ref Unsafe.AsRef(in array._param[0]));

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose()
    {
        if (_pins is null)
        {
            return;
        }

        for (int i = 0; i < _pins.Length; i++)
        {
            _pins[i].Free();
        }
    }
}
