// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Windows.Win32.System.Variant;

namespace Windows.Win32.System.Com;

/// <summary>
///  Helper to scope lifetime of a <see cref="SAFEARRAY"/> created via
///  <see cref="PInvoke.SafeArrayCreate(VARENUM, uint, SAFEARRAYBOUND*)"/>
///  Destroys the <see cref="SAFEARRAY"/> (if any) when disposed. Note that this scope currently only works for a
///  one dimensional <see cref="SAFEARRAY"/>.
/// </summary>
/// <remarks>
///  <para>
///   Use in a <see langword="using" /> statement to ensure the <see cref="SAFEARRAY"/> gets disposed.
///  </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the array.</typeparam>
internal readonly unsafe ref struct ComSafeArrayScope<T> where T : unmanaged, IComIID
{
    private readonly nint _value;

    /// <summary>
    ///  Gets the underlying SAFEARRAY pointer.
    /// </summary>
    public SAFEARRAY* Value => (SAFEARRAY*)_value;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ComSafeArrayScope{T}"/> struct with an existing
    ///  <see cref="SAFEARRAY"/> pointer.
    /// </summary>
    /// <param name="value">The <see cref="SAFEARRAY"/> pointer to wrap.</param>
    /// <exception cref="ArgumentException">
    ///  Thrown when the provided <see cref="SAFEARRAY"/> has a VarType that doesn't match the generic type parameter.
    /// </exception>
    public ComSafeArrayScope(SAFEARRAY* value)
    {
        if (value is null)
        {
            // This ComSafeArrayScope is meant to receive a SAFEARRAY* from COM.
            _value = (nint)value;
            return;
        }

        if (value->VarType is not VARENUM.VT_UNKNOWN)
        {
            throw new ArgumentException($"Wanted SafeArrayScope<{typeof(T)}> but got SAFEARRAY with VarType={value->VarType}");
        }

        _value = (nint)value;
    }

    /// <summary>
    ///  Gets the element at the specified index in the <see cref="SAFEARRAY"/>.
    /// </summary>
    /// <param name="i">The zero-based index of the element to get.</param>
    /// <returns>The element at the specified index.</returns>
    /// <remarks>
    ///  <para>
    ///   Anything the <see cref="SAFEARRAY"/> gives out is a copy and has been add ref'ed appropriately.
    ///   All items given out by the <see cref="SAFEARRAY"/> should be disposed.
    ///  </para>
    /// </remarks>
    public ComScope<T> this[int i] => new((T*)GetElement<nint>(i));

    /// <summary>
    ///  Get a copy of the element at the specified index in the <see cref="SAFEARRAY"/>.
    /// </summary>
    private TReturn GetElement<TReturn>(int index) where TReturn : unmanaged
    {
        Span<int> indices = [index];
        TReturn result;
        fixed (int* pIndices = indices)
        {
            PInvoke.SafeArrayGetElement(Value, pIndices, &result).ThrowOnFailure();
        }

        return result;
    }

    /// <summary>
    ///  Gets the number of elements in the <see cref="SAFEARRAY"/>.
    /// </summary>
    public int Length => (int)Value->GetBounds().cElements;

    /// <summary>
    ///  Gets a value indicating whether the <see cref="SAFEARRAY"/> pointer is null.
    /// </summary>
    public bool IsNull => _value == 0;

    /// <summary>
    ///  Gets a value indicating whether the <see cref="SAFEARRAY"/> is empty.
    /// </summary>
    public bool IsEmpty => Length == 0;

    /// <summary>
    ///  Destroys the <see cref="SAFEARRAY"/>.
    /// </summary>
    public void Dispose()
    {
        SAFEARRAY* safeArray = (SAFEARRAY*)_value;

        // Really want this to be null after disposal to avoid double destroy, but we also want
        // to maintain the readonly state of the struct to allow passing as `in` without creating implicit
        // copies (which would break the T** and void** operators).
        *(SAFEARRAY**)this = null;

        if (safeArray is not null)
        {
            PInvoke.SafeArrayDestroy(safeArray).ThrowOnFailure();
        }
    }

    /// <summary>
    ///  Implicitly converts a <see cref="ComSafeArrayScope{T}"/> to a pointer to a <see cref="SAFEARRAY"/> pointer.
    /// </summary>
    /// <param name="scope">The <see cref="ComSafeArrayScope{T}"/> to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SAFEARRAY**(in ComSafeArrayScope<T> scope) =>
        (SAFEARRAY**)Unsafe.AsPointer(ref Unsafe.AsRef(in scope._value));
}
