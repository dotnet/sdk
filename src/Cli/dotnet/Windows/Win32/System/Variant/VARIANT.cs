// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using static Windows.Win32.System.Variant.VARENUM;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using System.Diagnostics.CodeAnalysis;
using Windows.Win32.Foundation;
using System.Runtime.CompilerServices;

namespace Windows.Win32.System.Variant;

internal unsafe partial struct VARIANT : IDisposable
{
    /// <summary>
    ///  Gets an empty <see cref="VARIANT"/> instance.
    /// </summary>
    public static VARIANT Empty { get; }

    /// <summary>
    ///  Gets a value indicating whether this <see cref="VARIANT"/> is empty.
    /// </summary>
    public bool IsEmpty => vt == VT_EMPTY && data.llVal == 0;

    /// <summary>
    ///  Gets the <see cref="VARENUM"/> type of this <see cref="VARIANT"/>.
    /// </summary>
    public VARENUM Type => vt & VT_TYPEMASK;

    /// <summary>
    ///  Gets a value indicating whether this <see cref="VARIANT"/> is a by-reference value.
    /// </summary>
    public bool Byref => vt.HasFlag(VT_BYREF);

    /// <summary>
    ///  Gets a reference to the <see cref="VARENUM"/> value type field.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Use <see cref="Type"/> to read the type as some of the bits overlap with <see cref="VT_DECIMAL"/> data.
    ///  </para>
    /// </remarks>
    [UnscopedRef]
    public ref VARENUM vt => ref Anonymous.Anonymous.vt;

    /// <summary>
    ///  Gets a reference to the data union of this <see cref="VARIANT"/>.
    /// </summary>
    [UnscopedRef]
    public ref _Anonymous_e__Union._Anonymous_e__Struct._Anonymous_e__Union data => ref Anonymous.Anonymous.Anonymous;

    /// <summary>
    ///  Releases resources used by this <see cref="VARIANT"/>.
    /// </summary>
    public void Dispose() => Clear();

    /// <summary>
    ///  Clears the value of this <see cref="VARIANT"/>, releasing any associated resources.
    /// </summary>
    public void Clear()
    {
        // PropVariantClear is essentially a superset of VariantClear it calls CoTaskMemFree on the following types:
        //
        //     - VT_LPWSTR, VT_LPSTR, VT_CLSID (psvVal)
        //     - VT_BSTR_BLOB (bstrblobVal.pData)
        //     - VT_CF (pclipdata->pClipData, pclipdata)
        //     - VT_BLOB, VT_BLOB_OBJECT (blob.pData)
        //     - VT_STREAM, VT_STREAMED_OBJECT (pStream)
        //     - VT_VERSIONED_STREAM (pVersionedStream->pStream, pVersionedStream)
        //     - VT_STORAGE, VT_STORED_OBJECT (pStorage)
        //
        // If the VARTYPE is a VT_VECTOR, the contents are cleared as above and CoTaskMemFree is also called on
        // cabstr.pElems.
        //
        // https://learn.microsoft.com/windows/win32/api/oleauto/nf-oleauto-variantclear#remarks
        //
        //     - VT_BSTR (SysFreeString)
        //     - VT_DISPATCH / VT_UNKOWN (->Release(), if not VT_BYREF)

        if (IsEmpty)
        {
            return;
        }

        fixed (void* t = &this)
        {
            PInvoke.PropVariantClear((PROPVARIANT*)t);
        }

        vt = VT_EMPTY;
        data = default;
    }

    /// <summary>
    ///  Converts the specified <see cref="VARIANT"/> to a <see cref="decimal"/>.
    /// </summary>
    /// <param name="value">The <see cref="VARIANT"/> to convert.</param>
    /// <exception cref="InvalidCastException">Thrown if the <see cref="VARIANT"/> does not contain a decimal value.</exception>
    public static explicit operator decimal(VARIANT value)
        => value.vt == VT_DECIMAL ? value.Anonymous.decVal : ThrowInvalidCast<decimal>();

    /// <summary>
    ///  Converts the specified <see cref="VARIANT"/> to an <see cref="int"/>.
    /// </summary>
    /// <param name="value">The <see cref="VARIANT"/> to convert.</param>
    /// <exception cref="InvalidCastException">Thrown if the <see cref="VARIANT"/> does not contain an int value.</exception>
    public static explicit operator int(VARIANT value)
        => value.vt is VT_I4 or VT_INT ? value.data.intVal : ThrowInvalidCast<int>();

    /// <summary>
    ///  Converts the specified <see cref="int"/> to a <see cref="VARIANT"/>.
    /// </summary>
    /// <param name="value">The int value to convert.</param>
    public static explicit operator VARIANT(int value)
        => new()
        {
            vt = VT_I4,
            data = new() { intVal = value }
        };

    /// <summary>
    ///  Converts the specified <see cref="VARIANT"/> to a <see cref="uint"/>.
    /// </summary>
    /// <param name="value">The <see cref="VARIANT"/> to convert.</param>
    /// <exception cref="InvalidCastException">Thrown if the <see cref="VARIANT"/> does not contain a uint value.</exception>
    public static explicit operator uint(VARIANT value)
        => value.vt is VT_UI4 or VT_UINT ? value.data.uintVal : ThrowInvalidCast<uint>();

    /// <summary>
    ///  Converts the specified <see cref="uint"/> to a <see cref="VARIANT"/>.
    /// </summary>
    /// <param name="value">The uint value to convert.</param>
    public static explicit operator VARIANT(uint value)
        => new()
        {
            vt = VT_UI4,
            data = new() { uintVal = value }
        };

    /// <summary>
    ///  Converts the specified <see cref="VARIANT"/> to a <see cref="bool"/>.
    /// </summary>
    /// <param name="value">The <see cref="VARIANT"/> to convert.</param>
    /// <exception cref="InvalidCastException">Thrown if the <see cref="VARIANT"/> does not contain a bool value.</exception>
    public static explicit operator bool(VARIANT value)
        => value.vt == VT_BOOL ? value.data.boolVal != VARIANT_BOOL.VARIANT_FALSE : ThrowInvalidCast<bool>();

    /// <summary>
    ///  Converts the specified <see cref="bool"/> to a <see cref="VARIANT"/>.
    /// </summary>
    /// <param name="value">The bool value to convert.</param>
    public static explicit operator VARIANT(bool value)
        => new()
        {
            vt = VT_BOOL,
            data = new() { boolVal = value ? VARIANT_BOOL.VARIANT_TRUE : VARIANT_BOOL.VARIANT_FALSE }
        };

    /// <summary>
    ///  Converts the specified <see cref="VARIANT"/> to an <see cref="IDispatch"/> pointer.
    /// </summary>
    /// <param name="value">The <see cref="VARIANT"/> to convert.</param>
    /// <exception cref="InvalidCastException">Thrown if the <see cref="VARIANT"/> does not contain an IDispatch pointer.</exception>
    public static explicit operator IDispatch*(VARIANT value)
        => value.vt == VT_DISPATCH ? value.data.pdispVal : ThrowInvalidPointerCast<IDispatch>();

    /// <summary>
    ///  Converts the specified <see cref="IDispatch"/> pointer to a <see cref="VARIANT"/>.
    /// </summary>
    /// <param name="value">The IDispatch pointer to convert.</param>
    public static explicit operator VARIANT(IDispatch* value)
        => new()
        {
            vt = VT_DISPATCH,
            data = new() { pdispVal = value }
        };

    /// <summary>
    ///  Converts the specified <see cref="BSTR"/> to a <see cref="VARIANT"/>.
    /// </summary>
    /// <param name="value">The <see cref="BSTR"/> to convert.</param>
    public static explicit operator VARIANT(BSTR value)
        => new()
        {
            vt = VT_BSTR,
            data = new() { bstrVal = value }
        };

    /// <summary>
    ///  Converts the specified <see cref="VARIANT"/> to a <see cref="string"/>.
    /// </summary>
    /// <param name="value">The <see cref="VARIANT"/> to convert.</param>
    /// <exception cref="InvalidCastException">Thrown if the <see cref="VARIANT"/> does not contain a string value.</exception>
    public static explicit operator string(VARIANT value) => value.vt switch
    {
        VT_BSTR => value.data.bstrVal.ToString(),
        VT_LPWSTR => new((char*)value.data.pcVal.Value),        // Technically a PROPVARIANT.pwszVal
        _ => ThrowInvalidCast<string>(),
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T ThrowInvalidCast<T>() => throw new InvalidCastException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T* ThrowInvalidPointerCast<T>() where T : unmanaged => throw new InvalidCastException();

    /// <summary>
    ///  Returns the managed type returned from <see cref="ToObject()"/>.
    /// </summary>
    /// <returns>The managed <see cref="Type"/> corresponding to this <see cref="VARIANT"/>, or <c>null</c> if unknown or empty.</returns>
    public Type? GetManagedType() => IsEmpty ? null : GetManagedType(Type);

    /// <summary>
    ///  Returns the managed type returned from <see cref="ToObject()"/> for the given <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The <see cref="VARENUM"/> type to get the managed type for.</param>
    /// <returns>The managed <see cref="Type"/> corresponding to the specified <paramref name="type"/>, or <c>null</c> if unknown.</returns>
    public static Type? GetManagedType(VARENUM type)
    {
        return type switch
        {
            VT_I1 => typeof(sbyte),
            VT_UI1 => typeof(byte),
            VT_I2 => typeof(short),
            VT_UI2 => typeof(ushort),
            VT_I4 => typeof(int),
            VT_UI4 => typeof(uint),
            VT_I8 => typeof(long),
            VT_UI8 => typeof(ulong),
            VT_INT => typeof(int),
            VT_UINT => typeof(uint),
            VT_R4 => typeof(float),
            VT_R8 => typeof(double),
            VT_BOOL => typeof(bool),
            VT_ERROR => typeof(int),
            VT_CY => typeof(decimal),
            VT_DATE => typeof(DateTime),
            VT_FILETIME => typeof(DateTime),
            VT_BSTR => typeof(string),
            VT_LPSTR => typeof(string),
            VT_LPWSTR => typeof(string),
            VT_DECIMAL => typeof(decimal),
            VT_VARIANT => typeof(VARIANT),
            VT_CLSID => typeof(Guid),
            VT_BLOB => typeof(byte[]),
            VT_STREAM => typeof(Stream),
            VT_UNKNOWN => null,
            VT_DISPATCH => null,
            VT_CF => null,
            VT_STREAMED_OBJECT => null,
            VT_STORAGE => null,
            VT_STORED_OBJECT => null,
            VT_VERSIONED_STREAM => null,
            _ => GetArrayType(type),
        };

        static Type? GetArrayType(VARENUM type)
        {
            if (!type.HasFlag(VT_ARRAY) && !type.HasFlag(VT_VECTOR))
            {
                return null;
            }

            type &= VT_TYPEMASK;

            return type switch
            {
                VT_I1 => typeof(sbyte[]),
                VT_UI1 => typeof(byte[]),
                VT_I2 => typeof(short[]),
                VT_UI2 => typeof(ushort[]),
                VT_I4 => typeof(int[]),
                VT_UI4 => typeof(uint[]),
                VT_I8 => typeof(long[]),
                VT_UI8 => typeof(ulong[]),
                VT_INT => typeof(int[]),
                VT_UINT => typeof(uint[]),
                VT_R4 => typeof(float[]),
                VT_R8 => typeof(double[]),
                VT_BOOL => typeof(bool[]),
                VT_ERROR => typeof(int[]),
                VT_CY => typeof(decimal[]),
                VT_DATE => typeof(DateTime[]),
                VT_FILETIME => typeof(DateTime[]),
                VT_BSTR => typeof(string[]),
                VT_LPSTR => typeof(string[]),
                VT_LPWSTR => typeof(string[]),
                VT_DECIMAL => typeof(decimal[]),
                VT_VARIANT => typeof(VARIANT[]),
                VT_CLSID => typeof(Guid[]),
                VT_STREAM => typeof(Stream[]),
                VT_BLOB => null,
                VT_UNKNOWN => null,
                VT_DISPATCH => null,
                VT_CF => null,
                VT_STREAMED_OBJECT => null,
                VT_STORAGE => null,
                VT_STORED_OBJECT => null,
                VT_VERSIONED_STREAM => null,
                _ => null,
            };
        }
    }

    /// <summary>
    ///  Conversion operator to convert a <see cref="VARIANT"/> to a <see cref="string"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator VARIANT(string value) => new()
    {
        // Runtime marshalling converts strings to BSTR variants
        vt = VT_BSTR,
        data = new() { bstrVal = new(value) }
    };

    /// <summary>
    ///  Conversion operator to convert a <see cref="VARIANT"/> to a <see cref="double"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator VARIANT(double value) => new()
    {
        vt = VT_R8,
        data = new() { dblVal = value }
    };

    /// <summary>
    ///  Converts the given object to <see cref="VARIANT"/>.
    /// </summary>
    public static VARIANT FromObject(object? value)
    {
        if (value is null)
        {
            return Empty;
        }

        if (value is string stringValue)
        {
            return (VARIANT)stringValue;
        }
        else if (value is bool boolValue)
        {
            return (VARIANT)boolValue;
        }
        else if (value is short shortValue)
        {
            return (VARIANT)shortValue;
        }
        else if (value is int intValue)
        {
            return (VARIANT)intValue;
        }
        else if (value is uint uintValue)
        {
            return (VARIANT)uintValue;
        }
        else if (value is double doubleValue)
        {
            return (VARIANT)doubleValue;
        }

        // Need to fill out to match Marshal behavior so we can remove the call.
        // https://github.com/dotnet/winforms/issues/8596

        VARIANT variant = default;
        Marshal.GetNativeVariantForObject(value, (nint)(void*)&variant);
        return variant;
    }
}
