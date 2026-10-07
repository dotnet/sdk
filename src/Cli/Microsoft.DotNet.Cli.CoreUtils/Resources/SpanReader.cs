// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Fast stack based <see cref="ReadOnlySpan{T}"/> reader.
/// </summary>
/// <remarks>
///  <para>
///   Care must be used when reading struct values that depend on a specific field state for members to work
///   correctly. For example, <see cref="DateTime"/> has a very specific set of valid values for its packed
///   <see langword="ulong"/> field.
///  </para>
///  <para>
///   Inspired by SequenceReader{T} patterns.
///  </para>
/// </remarks>
/// <param name="span">The span to read.</param>
internal unsafe ref struct SpanReader<T>(ReadOnlySpan<T> span) where T : unmanaged, IEquatable<T>
{
    // Deliberately not an auto property for performance.
    private ReadOnlySpan<T> _unread = span;

    /// <summary>
    ///  Gets the original span that the reader was created with.
    /// </summary>
    public ReadOnlySpan<T> Span { get; } = span;

    /// <summary>
    ///  The portion of the original span that has not yet been read.
    /// </summary>
    public readonly ReadOnlySpan<T> Unread => _unread;

    /// <summary>
    ///  Returns <see langword="true"/> if there is no unread data left in the span.
    /// </summary>
    public readonly bool End => _unread.IsEmpty;

    /// <summary>
    ///  Gets or sets the current position of the reader within the span.
    /// </summary>
    /// <value>The zero-based position of the reader. Setting this value repositions the reader.</value>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when the value is negative or greater than <see cref="Length"/>.
    /// </exception>
    public int Position
    {
        readonly get => Span.Length - _unread.Length;
        set => _unread = Span[value..];
    }

    /// <summary>
    ///  Gets the total length of the original span.
    /// </summary>
    public readonly int Length => Span.Length;

    /// <summary>
    ///  Splits out the next span of data up to the given <paramref name="delimiter"/> or the end of the unread data.
    ///  Positions the reader to the next unread data after the delimiter.
    /// </summary>
    /// <param name="delimiter">The delimiter to look for.</param>
    /// <param name="span">The read data, if any.</param>
    /// <returns><see langword="true"/> if a segment was found; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySplit(T delimiter, out ReadOnlySpan<T> span)
    {
        bool result = false;

        if (_unread.IsEmpty)
        {
            span = default;
        }
        else
        {
            if (!TryReadTo(delimiter, advancePastDelimiter: true, out span))
            {
                span = _unread;
                _unread = default;
            }

            result = true;
        }

        return result;
    }

    /// <summary>
    ///  Splits out the next span of data up to the given <paramref name="delimiters"/> or the end of the unread data.
    ///  Positions the reader to the next unread data after the delimiter.
    /// </summary>
    /// <param name="delimiters">The delimiter to look for.</param>
    /// <param name="span">The read data, if any.</param>
    /// <returns><see langword="true"/> if a segment was found; otherwise, <see langword="false"/>.</returns>
    public bool TrySplitAny(scoped ReadOnlySpan<T> delimiters, out ReadOnlySpan<T> span)
    {
        bool result = false;
        if (_unread.IsEmpty)
        {
            span = default;
        }
        else
        {
            if (!TryReadToAny(delimiters, advancePastDelimiter: true, out span))
            {
                // No delimiter found (or empty delimiters), return all remaining data
                span = _unread;
                _unread = default;
            }

            result = true;
        }

        return result;
    }

    /// <summary>
    ///  Try to read everything up to the given <paramref name="delimiter"/>. Advances the reader past the
    ///  <paramref name="delimiter"/> if found.
    /// </summary>
    /// <inheritdoc cref="TryReadTo(T, bool, out ReadOnlySpan{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadTo(T delimiter, out ReadOnlySpan<T> span) =>
        TryReadTo(delimiter, advancePastDelimiter: true, out span);

    /// <summary>
    ///  Try to read everything up to the given <paramref name="delimiter"/>.
    /// </summary>
    /// <param name="span">The read data, if any.</param>
    /// <param name="delimiter">The delimiter to look for.</param>
    /// <param name="advancePastDelimiter">
    ///  <see langword="true"/> to move past the <paramref name="delimiter"/> if found.
    /// </param>
    /// <returns><see langword="true"/> if the <paramref name="delimiter"/> was found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadTo(T delimiter, bool advancePastDelimiter, out ReadOnlySpan<T> span)
    {
        bool found = false;
        int index = _unread.IndexOf(delimiter);
        span = default;

        if (index != -1)
        {
            found = true;
            if (index > 0)
            {
                span = _unread;
                UncheckedSliceTo(ref span, index);
            }

            if (advancePastDelimiter)
            {
                index++;
            }

            UnsafeAdvance(index);
        }

        return found;
    }

    /// <summary>
    ///  Try to read everything up to the given <paramref name="delimiters"/>.
    /// </summary>
    /// <param name="span">The read data, if any.</param>
    /// <param name="delimiters">The delimiters to look for.</param>
    /// <param name="advancePastDelimiter">
    ///  True to move past the first found instance of any of the given <paramref name="delimiters"/>.
    /// </param>
    /// <returns>True if any of the <paramref name="delimiters"/> were found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadToAny(scoped ReadOnlySpan<T> delimiters, bool advancePastDelimiter, out ReadOnlySpan<T> span)
    {
        bool found = false;
        span = default;

        int index = delimiters.Length switch
        {
            // The downlevel extension of IndexOfAny returns 0 for empty delimiters.
#if NETFRAMEWORK
            0 => -1,
#endif
            2 => _unread.IndexOfAny(delimiters[0], delimiters[1]),
            _ => _unread.IndexOfAny(delimiters)
        };

        if (index != -1)
        {
            found = true;
            if (index > 0)
            {
                span = _unread;
                UncheckedSliceTo(ref span, index);
            }

            if (advancePastDelimiter)
            {
                index++;
            }

            UnsafeAdvance(index);
        }

        return found;
    }

    /// <summary>
    ///  Try to read the next value.
    /// </summary>
    /// <param name="value">
    ///  When this method returns, contains the value that was read, or the default value if no value could be read.
    /// </param>
    /// <returns><see langword="true"/> if a value was successfully read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead(out T value)
    {
        bool success;

        if (_unread.IsEmpty)
        {
            value = default;
            success = false;
        }
        else
        {
            success = true;
            value = _unread[0];
            UnsafeAdvance(1);
        }

        return success;
    }

    /// <summary>
    ///  Try to read a span of the given <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The number of elements to read.</param>
    /// <param name="span">
    ///  When this method returns, contains the span of data that was read, or an empty span if not enough data was available.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the requested number of elements was successfully read; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRead(int count, out ReadOnlySpan<T> span)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        bool success;

        if (count > _unread.Length)
        {
            span = default;
            success = false;
        }
        else
        {
            success = true;
            span = _unread[..count];
            UnsafeAdvance(count);
        }

        return success;
    }

    /// <summary>
    ///  Try to read a value of the given type. The size of the value must be evenly divisible by the size of
    ///  <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of value to read. Must be an unmanaged type.</typeparam>
    /// <param name="value">
    ///  When this method returns, contains the value that was read, or the default value if not enough data was available.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the value was successfully read; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///  Thrown when the size of <typeparamref name="TValue"/> is not evenly divisible by the size of <typeparamref name="T"/>.
    /// </exception>
    /// <remarks>
    ///  <para>
    ///   This is just a straight copy of bits. If <typeparamref name="TValue"/> has methods that depend on
    ///   specific field value constraints this could be unsafe.
    ///  </para>
    ///  <para>
    ///   The compiler will often optimize away the struct copy if you only read from the value.
    ///  </para>
    /// </remarks>
    public bool TryRead<TValue>(out TValue value) where TValue : unmanaged
    {
        if (sizeof(TValue) < sizeof(T) || sizeof(TValue) % sizeof(T) != 0)
        {
            throw new ArgumentException($"The size of {nameof(TValue)} must be evenly divisible by the size of {nameof(T)}.");
        }

        bool success;

        if (sizeof(TValue) > _unread.Length * sizeof(T))
        {
            value = default;
            success = false;
        }
        else
        {
            success = true;
            value = Unsafe.ReadUnaligned<TValue>(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(_unread)));
            UnsafeAdvance(sizeof(TValue) / sizeof(T));
        }

        return success;
    }

    /// <summary>
    ///  Try to read a span of values of the given type. The size of the value must be evenly divisible by the size of
    ///  <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of values to read. Must be an unmanaged type.</typeparam>
    /// <param name="count">The number of values to read.</param>
    /// <param name="value">
    ///  When this method returns, contains the span of values that were read, or an empty span if not enough data
    ///  was available.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the requested number of values was successfully read; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///  Thrown when the size of <typeparamref name="TValue"/> is not evenly divisible by the size of <typeparamref name="T"/>.
    /// </exception>
    /// <remarks>
    ///  <para>
    ///   This effectively does a <see cref="MemoryMarshal.Cast{TFrom, TTo}(ReadOnlySpan{TFrom})"/> and the same
    ///   caveats apply about safety.
    ///  </para>
    /// </remarks>
    public bool TryRead<TValue>(int count, out ReadOnlySpan<TValue> value) where TValue : unmanaged
    {
        if (sizeof(TValue) < sizeof(T) || sizeof(TValue) % sizeof(T) != 0)
        {
            throw new ArgumentException($"The size of {nameof(TValue)} must be evenly divisible by the size of {nameof(T)}.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(count);

        bool success;

        if (sizeof(TValue) * count > _unread.Length * sizeof(T))
        {
            value = default;
            success = false;
        }
        else
        {
            success = true;
#if NETFRAMEWORK
            ReadOnlySpan<T> current = _unread[..(sizeof(TValue) / sizeof(T) * count)];
            value = MemoryMarshal.Cast<T, TValue>(current);
#else
            value = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, TValue>(ref MemoryMarshal.GetReference(_unread)), count);
#endif
            UnsafeAdvance((sizeof(TValue) / sizeof(T)) * count);
        }

        return success;
    }

    /// <summary>
    ///  Peeks at the next value without advancing the reader.
    /// </summary>
    /// <param name="value">The next value or default if at the end.</param>
    /// <returns>False if at the end of the reader.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryPeek(out T value)
    {
        if (_unread.IsEmpty)
        {
            value = default;
            return false;
        }

        value = _unread[0];
        return true;
    }

    /// <summary>
    ///  Advance the reader if the given <paramref name="next"/> values are next.
    /// </summary>
    /// <param name="next">The span to compare the next items to.</param>
    /// <returns><see langword="true"/> if the values were found and the reader advanced.</returns>
    public bool TryAdvancePast(ReadOnlySpan<T> next)
    {
        bool success = false;
        if (_unread.StartsWith(next))
        {
            UnsafeAdvance(next.Length);
            success = true;
        }

        return success;
    }

    /// <summary>
    ///  Advance the reader past consecutive instances of the given <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to advance past.</param>
    /// <returns>How many positions the reader has been advanced.</returns>
    public int AdvancePast(T value)
    {
        int count = 0;

        int index = _unread.IndexOfAnyExcept(value);
        if (index == -1)
        {
            // Everything left is the value
            count = _unread.Length;
            _unread = default;
        }
        else if (index != 0)
        {
            count = index;
            UnsafeAdvance(index);
        }

        return count;
    }

    /// <summary>
    ///  Advance the reader by the given <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The number of positions to advance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="count"/> is negative or would advance past the end of the span.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _unread = _unread[count..];

    /// <summary>
    ///  Try to advance the reader by the given <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The number of positions to advance.</param>
    /// <returns>
    ///  <see langword="true"/> if the reader was successfully advanced; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdvance(int count)
    {
        if (count < 0 || count > _unread.Length)
        {
            return false;
        }

        UnsafeAdvance(count);
        return true;
    }

    /// <summary>
    ///  Rewind the reader by the given <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The number of positions to rewind.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="count"/> is negative or would rewind past the beginning of the span.
    /// </exception>
    public void Rewind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _unread = Span[(Span.Length - _unread.Length - count)..];
    }

    /// <summary>
    ///  Reset the reader to the beginning of the span.
    /// </summary>
    public void Reset() => _unread = Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UnsafeAdvance(int count)
    {
        Debug.Assert((uint)count <= (uint)_unread.Length);
        UncheckedSlice(ref _unread, count, _unread.Length - count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UncheckedSliceTo(ref ReadOnlySpan<T> span, int length)
    {
        Debug.Assert((uint)length <= (uint)span.Length);
#if NETFRAMEWORK
        span = span[..length];
#else
        span = MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(span), length);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UncheckedSlice(ref ReadOnlySpan<T> span, int start, int length)
    {
        Debug.Assert((uint)start <= (uint)span.Length && (uint)length <= (uint)(span.Length - start));
#if NETFRAMEWORK
        span = span.Slice(start, length);
#else
        span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)start), length);
#endif
    }
}
