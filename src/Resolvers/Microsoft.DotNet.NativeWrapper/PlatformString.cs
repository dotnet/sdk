// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.NativeWrapper;

/// <summary>
///  Helper struct for marshalling platform-dependent strings.
/// </summary>
/// <remarks>
///  <para>
///   This is particularly useful for returned strings.
///  </para>
/// </remarks>
public readonly struct PlatformString
{
    /// <summary>
    ///  The native value.
    /// </summary>
    public readonly nint Value { get; }

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        if (Value == 0)
        {
            return string.Empty;
        }

#if NET
        if (!OperatingSystem.IsWindows())
        {
            return Marshal.PtrToStringUTF8(Value) ?? string.Empty;
        }
#endif

        return Marshal.PtrToStringUni(Value) ?? string.Empty;
    }

    /// <summary>
    ///  Implicit conversion from <see cref="PlatformString"/> to <see cref="string"/>.
    /// </summary>
    public static implicit operator string(PlatformString value) => value.ToString();
}
