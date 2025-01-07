// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.HotReload;

namespace Microsoft.DotNet.Watch
{
    internal enum PayloadType
    {
        ManagedCodeUpdate = 1,
        StaticAssetUpdate = 2,
        InitialUpdatesCompleted = 3,
    }

    internal readonly struct UpdatePayload(IReadOnlyList<UpdateDelta> deltas, ResponseLoggingLevel responseLoggingLevel)
    {
        public const byte ApplySuccessValue = 0;

        private const byte Version = 4;

        public IReadOnlyList<UpdateDelta> Deltas { get; } = deltas;
        public ResponseLoggingLevel ResponseLoggingLevel { get; } = responseLoggingLevel;

        /// <summary>
        /// Called by the dotnet-watch.
        /// </summary>
        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(Version, cancellationToken);
            await stream.WriteAsync(Deltas.Count, cancellationToken);

            foreach (var delta in Deltas)
            {
                await stream.WriteAsync(delta.ModuleId, cancellationToken);
                await stream.WriteByteArrayAsync(delta.MetadataDelta, cancellationToken);
                await stream.WriteByteArrayAsync(delta.ILDelta, cancellationToken);
                await stream.WriteByteArrayAsync(delta.PdbDelta, cancellationToken);
                await stream.WriteAsync(delta.UpdatedTypes, cancellationToken);
            }

            await stream.WriteAsync((byte)ResponseLoggingLevel, cancellationToken);
        }

        /// <summary>
        /// Called by the dotnet-watch.
        /// </summary>
        public static async ValueTask WriteLogAsync(Stream stream, IReadOnlyCollection<(string message, AgentMessageSeverity severity)> log, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(log.Count, cancellationToken);

            foreach (var (message, severity) in log)
            {
                await stream.WriteAsync(message, cancellationToken);
                await stream.WriteAsync((byte)severity, cancellationToken);
            }
        }

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public static async ValueTask<UpdatePayload> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var version = await stream.ReadByteAsync(cancellationToken);
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var count = await stream.ReadInt32Async(cancellationToken);

            var deltas = new UpdateDelta[count];
            for (var i = 0; i < count; i++)
            {
                var moduleId = await stream.ReadGuidAsync(cancellationToken);
                var metadataDelta = await stream.ReadByteArrayAsync(cancellationToken);
                var ilDelta = await stream.ReadByteArrayAsync(cancellationToken);
                var pdbDelta = await stream.ReadByteArrayAsync(cancellationToken);
                var updatedTypes = await stream.ReadIntArrayAsync(cancellationToken);

                deltas[i] = new UpdateDelta(moduleId, metadataDelta: metadataDelta, ilDelta: ilDelta, pdbDelta: pdbDelta, updatedTypes);
            }

            var responseLoggingLevel = (ResponseLoggingLevel)await stream.ReadByteAsync(cancellationToken);
            return new UpdatePayload(deltas, responseLoggingLevel: responseLoggingLevel);
        }

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public static async IAsyncEnumerable<(string message, AgentMessageSeverity severity)> ReadLogAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var entryCount = await stream.ReadInt32Async(cancellationToken);

            for (var i = 0; i < entryCount; i++)
            {
                var message = await stream.ReadStringAsync(cancellationToken);
                var severity = (AgentMessageSeverity)await stream.ReadByteAsync(cancellationToken);
                yield return (message, severity);
            }
        }
    }

    internal readonly struct ClientInitializationPayload(string capabilities)
    {
        private const byte Version = 0;

        public string Capabilities { get; } = capabilities;

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(Version, cancellationToken);
            await stream.WriteAsync(Capabilities, cancellationToken);
        }

        /// <summary>
        /// Called by dotnet-watch.
        /// </summary>
        public static async ValueTask<ClientInitializationPayload> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var version = await stream.ReadByteAsync(cancellationToken);
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var capabilities = await stream.ReadStringAsync(cancellationToken);
            return new ClientInitializationPayload(capabilities);
        }
    }

    internal readonly struct StaticAssetPayload(
        string assemblyName,
        string relativePath,
        byte[] contents,
        bool isApplicationProject)
    {
        private const byte Version = 1;

        public string AssemblyName { get; } = assemblyName;
        public bool IsApplicationProject { get; } = isApplicationProject;
        public string RelativePath { get; } = relativePath;
        public byte[] Contents { get; } = contents;

        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(Version, cancellationToken);
            await stream.WriteAsync(AssemblyName, cancellationToken);
            await stream.WriteAsync(IsApplicationProject, cancellationToken);
            await stream.WriteAsync(RelativePath, cancellationToken);
            await stream.WriteByteArrayAsync(Contents, cancellationToken);
        }

        public static async ValueTask<StaticAssetPayload> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            var version = await stream.ReadByteAsync(cancellationToken);
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var assemblyName = await stream.ReadStringAsync(cancellationToken);
            var isAppProject = await stream.ReadBooleanAsync(cancellationToken);
            var relativePath = await stream.ReadStringAsync(cancellationToken);
            var contents = await stream.ReadByteArrayAsync(cancellationToken);

            return new StaticAssetPayload(
                assemblyName: assemblyName,
                relativePath: relativePath,
                contents: contents,
                isApplicationProject: isAppProject);
        }
    }

    /// <summary>
    /// Implements async read/write helpers that provide functionality of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
    /// See https://github.com/dotnet/runtime/issues/17229
    /// </summary>
    internal static class StreamExtesions
    {
        public static ValueTask WriteAsync(this Stream stream, bool value, CancellationToken cancellationToken)
            => WriteAsync(stream, (byte)(value ? 1 : 0), cancellationToken);

        public static async ValueTask WriteAsync(this Stream stream, byte value, CancellationToken cancellationToken)
        {
            var size = sizeof(byte);
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                buffer[0] = value;
                await stream.WriteAsync(buffer, offset: 0, count: size, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async ValueTask WriteAsync(this Stream stream, int value, CancellationToken cancellationToken)
        {
            var size = sizeof(int);
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
                await stream.WriteAsync(buffer, offset: 0, count: size, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static ValueTask WriteAsync(this Stream stream, Guid value, CancellationToken cancellationToken)
            => stream.WriteAsync(value.ToByteArray(), cancellationToken);

        public static async ValueTask WriteByteArrayAsync(this Stream stream, byte[] value, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(value.Length, cancellationToken);
            await stream.WriteAsync(value, cancellationToken);
        }

        public static async ValueTask WriteAsync(this Stream stream, int[] value, CancellationToken cancellationToken)
        {
            var size = sizeof(int) * (value.Length + 1);
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                BinaryPrimitives.WriteInt32LittleEndian(buffer, value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan((i + 1) * sizeof(int), sizeof(int)), value[i]);
                }

                await stream.WriteAsync(buffer, offset: 0, count: size, cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async ValueTask WriteAsync(this Stream stream, string value, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            await stream.Write7BitEncodedIntAsync(bytes.Length, cancellationToken);
            await stream.WriteAsync(bytes, cancellationToken);
        }

        public static async ValueTask Write7BitEncodedIntAsync(this Stream stream, int value, CancellationToken cancellationToken)
        {
            uint uValue = (uint)value;

            while (uValue > 0x7Fu)
            {
                await stream.WriteAsync((byte)(uValue | ~0x7Fu), cancellationToken);
                uValue >>= 7;
            }

            await stream.WriteAsync((byte)uValue, cancellationToken);
        }

        public static async ValueTask<bool> ReadBooleanAsync(this Stream stream, CancellationToken cancellationToken)
            => await stream.ReadByteAsync(cancellationToken) != 0;

        public static async ValueTask<byte> ReadByteAsync(this Stream stream, CancellationToken cancellationToken)
        {
            int size = sizeof(byte);
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                await ReadExactlyAsync(stream, buffer.AsMemory(0, size), cancellationToken);
                return buffer[0];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async ValueTask<int> ReadInt32Async(this Stream stream, CancellationToken cancellationToken)
        {
            int size = sizeof(int);
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                await ReadExactlyAsync(stream, buffer.AsMemory(0, size), cancellationToken);
                return BinaryPrimitives.ReadInt32LittleEndian(buffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async ValueTask<Guid> ReadGuidAsync(this Stream stream, CancellationToken cancellationToken)
        {
            const int size = 16;
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                await ReadExactlyAsync(stream, buffer.AsMemory(0, size), cancellationToken);
                return new Guid(buffer.AsSpan(0, size));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async ValueTask<byte[]> ReadByteArrayAsync(this Stream stream, CancellationToken cancellationToken)
        {
            var count = await stream.ReadInt32Async(cancellationToken);
            if (count == 0)
            {
                return [];
            }

            var bytes = new byte[count];
            await ReadExactlyAsync(stream, bytes, cancellationToken);
            return bytes;
        }

        public static async ValueTask<int[]> ReadIntArrayAsync(this Stream stream, CancellationToken cancellationToken)
        {
            var count = await stream.ReadInt32Async(cancellationToken);
            if (count == 0)
            {
                return [];
            }

            var result = new int[count];
            int size = count * sizeof(int);
            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                await ReadExactlyAsync(stream, buffer.AsMemory(0, size), cancellationToken);

                for (var i = 0; i < count; i++)
                {
                    result[i] = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(i * sizeof(int)));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return result;
        }

        public static async ValueTask<string> ReadStringAsync(this Stream stream, CancellationToken cancellationToken)
        {
            int size = await stream.Read7BitEncodedIntAsync(cancellationToken);
            if (size < 0)
            {
                throw new InvalidDataException();
            }

            if (size == 0)
            {
                return string.Empty;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: size);
            try
            {
                await ReadExactlyAsync(stream, buffer.AsMemory(0, size), cancellationToken);
                return Encoding.UTF8.GetString(buffer.AsSpan(0, size));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static async ValueTask<int> Read7BitEncodedIntAsync(this Stream stream, CancellationToken cancellationToken)
        {
            const int MaxBytesWithoutOverflow = 4;

            uint result = 0;
            byte b;

            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                b = await stream.ReadByteAsync(cancellationToken);
                result |= (b & 0x7Fu) << shift;

                if (b <= 0x7Fu)
                {
                    return (int)result;
                }
            }

            // Read the 5th byte. Since we already read 28 bits,
            // the value of this byte must fit within 4 bits (32 - 28),
            // and it must not have the high bit set.

            b = await stream.ReadByteAsync(cancellationToken);
            if (b > 0b_1111u)
            {
                throw new InvalidDataException();
            }

            result |= (uint)b << (MaxBytesWithoutOverflow * 7);
            return (int)result;
        }

        private static async ValueTask<int> ReadExactlyAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.Slice(totalRead), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                totalRead += read;
            }

            return totalRead;
        }
    }
}
