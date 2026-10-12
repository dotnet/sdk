// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Builds strings from a caller-provided buffer and rents additional storage only when needed.
/// </summary>
internal ref struct ValueStringBuilder
{
    private char[]? _arrayToReturnToPool;
    private Span<char> _buffer;
    private int _length;

    /// <summary>
    ///  Initializes a new builder over an initial buffer.
    /// </summary>
    /// <param name="initialBuffer">The initial character buffer.</param>
    internal ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _buffer = initialBuffer;
        _length = 0;
    }

    /// <summary>
    ///  Gets the number of characters written to the builder.
    /// </summary>
    internal readonly int Length => _length;

    /// <summary>
    ///  Appends a character to the builder.
    /// </summary>
    /// <param name="value">The character to append.</param>
    internal void Append(char value)
    {
        if (_length == _buffer.Length)
        {
            Grow(additionalCapacity: 1);
        }

        _buffer[_length++] = value;
    }

    /// <summary>
    ///  Appends a string to the builder.
    /// </summary>
    /// <param name="value">The string to append.</param>
    internal void Append(string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        int requiredCapacity = checked(_length + value.Length);
        if (requiredCapacity > _buffer.Length)
        {
            Grow(value.Length);
        }

        value.AsSpan().CopyTo(_buffer.Slice(_length));
        _length = requiredCapacity;
    }

    /// <inheritdoc/>
    public override readonly string ToString() => _buffer.Slice(0, _length).ToString();

    /// <summary>
    ///  Returns any rented storage to the shared pool.
    /// </summary>
    internal void Dispose()
    {
        char[]? arrayToReturn = _arrayToReturnToPool;
        this = default;

        if (arrayToReturn is not null)
        {
            ArrayPool<char>.Shared.Return(arrayToReturn);
        }
    }

    private void Grow(int additionalCapacity)
    {
        int requiredCapacity = checked(_length + additionalCapacity);
        int doubledCapacity = checked(_buffer.Length * 2);
        char[] newArray = ArrayPool<char>.Shared.Rent(Math.Max(requiredCapacity, doubledCapacity));
        _buffer.Slice(0, _length).CopyTo(newArray);

        char[]? arrayToReturn = _arrayToReturnToPool;
        _arrayToReturnToPool = newArray;
        _buffer = newArray;

        if (arrayToReturn is not null)
        {
            ArrayPool<char>.Shared.Return(arrayToReturn);
        }
    }
}