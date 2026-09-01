// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.DotNet.NativeWrapper;

/// <summary>
///  A platform-aware string marshaller that uses UTF-16 on Windows and UTF-8 on Unix platforms.
/// </summary>
/// <remarks>
///  <para>
///   This marshaller automatically selects the appropriate string encoding based on the current
///   operating system.
///  </para>
/// </remarks>
[CustomMarshaller(typeof(string), MarshalMode.Default, typeof(PlatformStringMarshaller))]
public static unsafe class PlatformStringMarshaller
{
    /// <summary>
    ///  Converts a managed string to an unmanaged pointer using platform-appropriate encoding.
    /// </summary>
    /// <param name="managed">The managed string to convert.</param>
    /// <returns>A pointer to the unmanaged string, or zero if the input is null.</returns>
    public static nint ConvertToUnmanaged(string? managed)
    {
        if (managed is null)
        {
            return 0;
        }

        return OperatingSystem.IsWindows()
            ? (nint)Utf16StringMarshaller.ConvertToUnmanaged(managed)
            : (nint)Utf8StringMarshaller.ConvertToUnmanaged(managed);
    }

    /// <summary>
    ///  Converts an unmanaged string pointer to a managed string using platform-appropriate encoding.
    /// </summary>
    /// <param name="unmanaged">The unmanaged string pointer.</param>
    /// <returns>The managed string, or null if the pointer is zero.</returns>
    public static string? ConvertToManaged(nint unmanaged)
    {
        if (unmanaged == 0)
        {
            return null;
        }

        return OperatingSystem.IsWindows()
            ? Utf16StringMarshaller.ConvertToManaged((ushort*)unmanaged)
            : Utf8StringMarshaller.ConvertToManaged((byte*)unmanaged);
    }

    /// <summary>
    ///  Frees memory allocated by <see cref="ConvertToUnmanaged"/>.
    /// </summary>
    /// <param name="unmanaged">The unmanaged string pointer to free.</param>
    public static void Free(nint unmanaged)
    {
        if (unmanaged == 0)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Utf16StringMarshaller.Free((ushort*)unmanaged);
        }
        else
        {
            Utf8StringMarshaller.Free((byte*)unmanaged);
        }
    }
}
#endif
