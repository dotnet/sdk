// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Foundation;

internal unsafe partial struct PCWSTR
{
    /// <summary>
    ///  <see langword="true"/> if the pointer is null.
    /// </summary>
    public bool IsNull => Value is null;

    /// <summary>
    ///  Calls <see cref="PInvoke.LocalFree(HLOCAL)"/> on the pointer if it is not null.
    /// </summary>
    public void LocalFree()
    {
        if (Value is not null)
        {
            PInvoke.LocalFree((HLOCAL)(nint)Value);
            Unsafe.AsRef(in this) = default;
        }
    }
}
