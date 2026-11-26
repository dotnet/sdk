// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  Extensions for <see cref="SpanReader{T}"/>.
/// </summary>
internal static class SpanReaderExtensions
{
    /// <param name="reader">The <see cref="SpanReader{T}"/> to read from.</param>
    extension(ref SpanReader<char> reader)
    {
        /// <summary>
        ///  Tries to read an integer from the current position of the <see cref="SpanReader{T}"/>.
        /// </summary>
        /// <param name="value">When successful, contains the read integer.</param>
        /// <returns>
        ///  <see langword="true"/> if an integer was successfully read; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryReadPositiveInteger(out uint value)
        {
            // Read digits until we hit a non-digit character or the end of the span.
            value = default;
            bool foundDigit = false;

            while (reader.TryPeek(out char next) && char.IsDigit(next))
            {
                value = value * 10u + (uint)(next - '0');
                reader.Advance(1);
                foundDigit = true;
            }

            return foundDigit;
        }
    }

    /// <param name="reader">The <see cref="SpanReader{T}"/> to read from.</param>
    extension(ref SpanReader<byte> reader)
    {
        /// <summary>
        ///  Reads a little-endian <see cref="int"/> and advances past it.
        /// </summary>
        /// <param name="value">On success, the value read.</param>
        /// <returns>
        ///  <see langword="true"/> if four bytes were available and read; otherwise <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadInt32LittleEndian(out int value)
        {
            if (reader.TryRead(sizeof(int), out ReadOnlySpan<byte> bytes))
            {
                value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        ///  Reads a 7-bit encoded (LEB128) <see cref="int"/> and advances past it.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Matches <see cref="System.IO.BinaryWriter.Write7BitEncodedInt(int)"/>. Returns
        ///   <see langword="false"/> for a truncated or overlong (overflowing) encoding rather than
        ///   throwing, leaving the reader's position unchanged, so it is safe on untrusted input.
        ///  </para>
        /// </remarks>
        /// <param name="value">On success, the value read.</param>
        /// <returns>
        ///  <see langword="true"/> if a well-formed value was read; otherwise <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead7BitEncodedInt32(out int value)
        {
            int start = reader.Position;
            uint result = 0;
            const int MaxBytesWithoutOverflow = 4;

            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                if (!reader.TryRead(out byte b))
                {
                    value = 0;
                    reader.Position = start;
                    return false;
                }

                result |= (uint)(b & 0x7F) << shift;
                if (b <= 0x7F)
                {
                    value = (int)result;
                    return true;
                }
            }

            // The fifth byte can only contribute the top four bits; more would overflow an int.
            if (!reader.TryRead(out byte last) || last > 0b1111)
            {
                value = 0;
                reader.Position = start;
                return false;
            }

            result |= (uint)last << (MaxBytesWithoutOverflow * 7);
            value = (int)result;
            return true;
        }
    }
}
