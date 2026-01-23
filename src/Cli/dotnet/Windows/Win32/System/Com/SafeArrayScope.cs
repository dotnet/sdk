// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Windows.Win32.Foundation;
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
internal readonly unsafe ref struct SafeArrayScope<T>
{
    private readonly nint _value;

    /// <summary>
    ///  Gets the underlying SAFEARRAY pointer.
    /// </summary>
    public SAFEARRAY* Value => (SAFEARRAY*)_value;

    /// <summary>
    ///  Initializes a new instance of the <see cref="SafeArrayScope{T}"/> struct with an existing
    ///  <see cref="SAFEARRAY"/> pointer.
    /// </summary>
    /// <param name="value">The <see cref="SAFEARRAY"/> pointer to wrap.</param>
    /// <exception cref="ArgumentException">
    ///  Thrown when the provided <see cref="SAFEARRAY"/> has a VarType that doesn't match the generic type parameter.
    /// </exception>
    public SafeArrayScope(SAFEARRAY* value)
    {
        if (value is null)
        {
            // This SafeArrayScope is meant to receive a SAFEARRAY* from COM.
            _value = (nint)value;
            return;
        }

        if (typeof(T) == typeof(string))
        {
            if (value->VarType is not VARENUM.VT_BSTR)
            {
                throw new ArgumentException($"Wanted SafeArrayScope<{typeof(T)}> but got SAFEARRAY with VarType={value->VarType}");
            }
        }
        else if (typeof(T) == typeof(int))
        {
            if (value->VarType is not VARENUM.VT_I4 and not VARENUM.VT_INT)
            {
                throw new ArgumentException($"Wanted SafeArrayScope<{typeof(T)}> but got SAFEARRAY with VarType={value->VarType}");
            }
        }
        else if (typeof(T) == typeof(double))
        {
            if (value->VarType is not VARENUM.VT_R8)
            {
                throw new ArgumentException($"Wanted SafeArrayScope<{typeof(T)}> but got SAFEARRAY with VarType={value->VarType}");
            }
        }
        else if (typeof(T) == typeof(nint))
        {
            if (value->VarType is not VARENUM.VT_UNKNOWN)
            {
                throw new ArgumentException($"Wanted SafeArrayScope<{typeof(T)}> but got SAFEARRAY with VarType={value->VarType}");
            }
        }
        else if (typeof(T).IsAssignableTo(typeof(IComIID)))
        {
            throw new ArgumentException("Use ComSafeArrayScope instead");
        }
        else if (typeof(T) == typeof(object))
        {
            if (value->VarType is not VARENUM.VT_VARIANT)
            {
                throw new ArgumentException($"Wanted SafeArrayScope<{typeof(T)}> but got SAFEARRAY with VarType={value->VarType}");
            }
        }
        else
        {
            // The type has not been accounted for yet in the SafeArrayScope
            // If the type was intentional, this SafeArrayScope needs to be updated
            // to behave appropriately with this type.
            throw new ArgumentException("Unknown type");
        }

        _value = (nint)value;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SafeArrayScope{T}"/> struct with a newly created
    ///  <see cref="SAFEARRAY"/> of the specified size.
    /// </summary>
    /// <param name="size">The size of the <see cref="SAFEARRAY"/> to create.</param>
    /// <exception cref="ArgumentException">
    ///  Thrown when the generic type parameter is not supported by <see cref="SafeArrayScope{T}"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///  Thrown when the <see cref="SAFEARRAY"/> could not be created.
    /// </exception>
    public SafeArrayScope(uint size)
    {
        VARENUM vt;

        if (typeof(T) == typeof(string))
        {
            vt = VARENUM.VT_BSTR;
        }
        else if (typeof(T) == typeof(int))
        {
            vt = VARENUM.VT_I4;
        }
        else if (typeof(T) == typeof(double))
        {
            vt = VARENUM.VT_R8;
        }
        else if (typeof(T) == typeof(nint) || typeof(T).IsAssignableTo(typeof(IComIID)))
        {
            throw new ArgumentException("Use ComSafeArrayScope instead");
        }
        else if (typeof(T) == typeof(object))
        {
            vt = VARENUM.VT_VARIANT;
        }
        else
        {
            // The type has not been accounted for yet in the SafeArrayScope
            // If the type was intentional, this SafeArrayScope needs to be updated
            // to behave appropriately with this type.
            throw new ArgumentException("Unknown type");
        }

        SAFEARRAYBOUND saBound = new()
        {
            cElements = size,
            lLbound = 0
        };

        _value = (nint)PInvoke.SafeArrayCreate(vt, 1, &saBound);
        if (_value == 0)
        {
            throw new InvalidOperationException("Unable to create SAFEARRAY");
        }
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SafeArrayScope{T}"/> struct with a newly created
    ///  <see cref="SAFEARRAY"/> populated with the elements from the provided array.
    /// </summary>
    /// <param name="array">The array to copy elements from.</param>
    public SafeArrayScope(T[] array) : this((uint)array.Length)
    {
        for (int i = 0; i < array.Length; i++)
        {
            this[i] = array[i];
        }
    }

    /// <summary>
    ///  Gets or sets the element at the specified index in the <see cref="SAFEARRAY"/>.
    /// </summary>
    /// <param name="i">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    /// <remarks>
    ///  <para>
    ///   A copy will be made of anything that is put into the <see cref="SAFEARRAY"/>
    ///   and anything the <see cref="SAFEARRAY"/> gives out is a copy and has been add ref appropriately if applicable.
    ///   Be sure to dispose of items that are given to the <see cref="SAFEARRAY"/> if necessary. All
    ///   items given out by the <see cref="SAFEARRAY"/> should be disposed.
    ///  </para>
    /// </remarks>
    public T? this[int i]
    {
        get
        {
            if (typeof(T) == typeof(string))
            {
                using BSTR result = GetElement<BSTR>(i);
                return (T)(object)result.ToString();
            }
            else if (typeof(T) == typeof(int))
            {
                return (T)(object)GetElement<int>(i);
            }
            else if (typeof(T) == typeof(double))
            {
                return (T)(object)GetElement<double>(i);
            }
            else if (typeof(T) == typeof(nint))
            {
                return (T)(object)GetElement<nint>(i);
            }
            else if (typeof(T) == typeof(object))
            {
                using VARIANT result = GetElement<VARIANT>(i);
                return (T?)result.ToObject();
            }
            else if (typeof(T) == typeof(BSTR))
            {
                BSTR result = GetElement<BSTR>(i);
                return (T)(object)result;
            }

            // Noop. This is an unknown type. We should fill this method out to to do the right
            // thing as we run into new types.
            return default;
        }
        set
        {
            if (Value->VarType == VARENUM.VT_VARIANT)
            {
                using VARIANT variant = VARIANT.FromObject(value);
                PutElement(i, &variant);
            }
            else if (value is string s)
            {
                using BSTR bstrText = new(s);
                PutElement(i, bstrText);
            }
            else if (value is int @int)
            {
                PutElement(i, &@int);
            }
            else if (value is double dbl)
            {
                PutElement(i, &dbl);
            }
            else if (value is nint @nint)
            {
                PutElement(i, (void*)@nint);
            }
        }
    }

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

    private void PutElement(int index, void* value)
    {
        Span<int> indices = [index];
        fixed (int* pIndices = indices)
        {
            PInvoke.SafeArrayPutElement((SAFEARRAY*)_value, pIndices, value).ThrowOnFailure();
        }
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
        *(void**)this = null;

        if (safeArray is not null)
        {
            PInvoke.SafeArrayDestroy(safeArray).ThrowOnFailure();
        }
    }

    /// <summary>
    ///  Explicitly converts a <see cref="SafeArrayScope{T}"/> to a <see cref="VARIANT"/> containing a <see cref="SAFEARRAY"/>.
    /// </summary>
    /// <param name="scope">The <see cref="SafeArrayScope{T}"/> to convert.</param>
    public static explicit operator VARIANT(in SafeArrayScope<T> scope) => new()
    {
        vt = VARENUM.VT_ARRAY | scope.Value->VarType,
        data = new() { parray = (SAFEARRAY*)scope._value }
    };

    /// <summary>
    ///  Implicitly converts a <see cref="SafeArrayScope{T}"/> to a <see cref="SAFEARRAY"/> pointer.
    /// </summary>
    /// <param name="scope">The <see cref="SafeArrayScope{T}"/> to convert.</param>
    public static implicit operator SAFEARRAY*(in SafeArrayScope<T> scope) => (SAFEARRAY*)scope._value;

    /// <summary>
    ///  Implicitly converts a <see cref="SafeArrayScope{T}"/> to a native integer representing the <see cref="SAFEARRAY"/> pointer.
    /// </summary>
    /// <param name="scope">The <see cref="SafeArrayScope{T}"/> to convert.</param>
    public static implicit operator nint(in SafeArrayScope<T> scope) => scope._value;

    /// <summary>
    ///  Implicitly converts a <see cref="SafeArrayScope{T}"/> to a pointer to a <see cref="SAFEARRAY"/> pointer.
    /// </summary>
    /// <param name="scope">The <see cref="SafeArrayScope{T}"/> to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SAFEARRAY**(in SafeArrayScope<T> scope) => (SAFEARRAY**)Unsafe.AsPointer(ref Unsafe.AsRef(in scope._value));

    /// <summary>
    ///  Implicitly converts a <see cref="SafeArrayScope{T}"/> to a pointer to a void pointer.
    /// </summary>
    /// <param name="scope">The <see cref="SafeArrayScope{T}"/> to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator void**(in SafeArrayScope<T> scope) => (void**)Unsafe.AsPointer(ref Unsafe.AsRef(in scope._value));
}
