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
[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedIn, typeof(ManagedToUnmanagedIn))]
[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedOut, typeof(ManagedToUnmanagedOut))]
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

    private static void PlatformFree(nint unmanaged)
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
    public static void Free(nint unmanaged) => PlatformFree(unmanaged);

    /// <summary>
    ///  Marshaller for managed-to-unmanaged direction (input parameters) with stack allocation support.
    /// </summary>
    public ref struct ManagedToUnmanagedIn
    {
        /// <summary>
        ///  The recommended buffer size for stack allocation.
        /// </summary>
        public static int BufferSize => 256;

        private nint _unmanagedValue;
        private Utf8StringMarshaller.ManagedToUnmanagedIn _utf8Marshaller;
        private bool _useUtf8;

        /// <summary>
        ///  Initializes the marshaller with an optional stack-allocated buffer.
        /// </summary>
        /// <param name="managed">The managed string to marshal.</param>
        /// <param name="buffer">A stack-allocated buffer that may be used for small strings.</param>
        public void FromManaged(string? managed, Span<byte> buffer)
        {
            _useUtf8 = !OperatingSystem.IsWindows();
            if (_useUtf8)
            {
                _utf8Marshaller.FromManaged(managed, buffer);
                _unmanagedValue = 0;
                return;
            }

            _unmanagedValue = (nint)Utf16StringMarshaller.ConvertToUnmanaged(managed);
        }

        /// <summary>
        ///  Returns the unmanaged string pointer.
        /// </summary>
        /// <returns>The unmanaged string pointer.</returns>
        public readonly nint ToUnmanaged() => _useUtf8
            ? (nint)_utf8Marshaller.ToUnmanaged()
            : _unmanagedValue;

        /// <summary>
        ///  Frees any allocated unmanaged memory.
        /// </summary>
        public readonly void Free()
        {
            if (_useUtf8)
            {
                _utf8Marshaller.Free();
                return;
            }

            if (_unmanagedValue != 0)
            {
                Utf16StringMarshaller.Free((ushort*)_unmanagedValue);
            }
        }
    }

    /// <summary>
    ///  Marshaller for managed-to-unmanaged direction (output/return values).
    /// </summary>
    public ref struct ManagedToUnmanagedOut
    {
        private nint _unmanagedValue;

        /// <summary>
        ///  Sets the unmanaged value received from native code.
        /// </summary>
        /// <param name="unmanaged">The unmanaged string pointer.</param>
        public void FromUnmanaged(nint unmanaged) => _unmanagedValue = unmanaged;

        /// <summary>
        ///  Converts the unmanaged string to a managed string.
        /// </summary>
        /// <returns>The managed string, or null if the pointer is zero.</returns>
        public readonly string? ToManaged() => ConvertToManaged(_unmanagedValue);

        /// <summary>
        ///  Frees the unmanaged string memory.
        /// </summary>
        public readonly void Free() => PlatformFree(_unmanagedValue);
    }
}
#endif
