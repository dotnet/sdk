// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        private const byte Version = 3;
        private const byte Type = (byte)PayloadType.ManagedCodeUpdate;

        public IReadOnlyList<UpdateDelta> Deltas { get; } = deltas;
        public ResponseLoggingLevel ResponseLoggingLevel { get; } = responseLoggingLevel;

        /// <summary>
        /// Called by the dotnet-watch.
        /// </summary>
        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            await using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            binaryWriter.Write(Type);
            binaryWriter.Write(Version);
            binaryWriter.Write(Deltas.Count);

            foreach (var delta in Deltas)
            {
                binaryWriter.Write(delta.ModuleId.ToString());
                await binaryWriter.WriteAsync(delta.MetadataDelta, cancellationToken);
                await binaryWriter.WriteAsync(delta.ILDelta, cancellationToken);
                await binaryWriter.WriteAsync(delta.PdbDelta, cancellationToken);
                binaryWriter.WriteArray(delta.UpdatedTypes);
            }

            binaryWriter.Write((byte)ResponseLoggingLevel);
        }

        /// <summary>
        /// Called by the dotnet-watch.
        /// </summary>
        public static void WriteLog(Stream stream, IReadOnlyCollection<(string message, AgentMessageSeverity severity)> log)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            writer.Write(log.Count);

            foreach (var (message, severity) in log)
            {
                writer.Write(message);
                writer.Write((byte)severity);
            }
        }

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public static async ValueTask<UpdatePayload> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = binaryReader.ReadByte();
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var count = binaryReader.ReadInt32();

            var deltas = new UpdateDelta[count];
            for (var i = 0; i < count; i++)
            {
                var moduleId = Guid.Parse(binaryReader.ReadString());
                var metadataDelta = await binaryReader.ReadBytesAsync(cancellationToken);
                var ilDelta = await binaryReader.ReadBytesAsync(cancellationToken);
                var pdbDelta = await binaryReader.ReadBytesAsync(cancellationToken);
                var updatedTypes = binaryReader.ReadIntArray();

                deltas[i] = new UpdateDelta(moduleId, metadataDelta: metadataDelta, ilDelta: ilDelta, pdbDelta: pdbDelta, updatedTypes);
            }

            var responseLoggingLevel = (ResponseLoggingLevel)binaryReader.ReadByte();

            return new UpdatePayload(deltas, responseLoggingLevel: responseLoggingLevel);
        }

        /// <summary>
        /// Called by delta applier.
        /// </summary>
        public static IEnumerable<(string message, AgentMessageSeverity severity)> ReadLog(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var entryCount = reader.ReadInt32();

            for (var i = 0; i < entryCount; i++)
            {
                yield return (reader.ReadString(), (AgentMessageSeverity)reader.ReadByte());
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
        public void Write(Stream stream)
        {
            using var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            binaryWriter.Write(Version);
            binaryWriter.Write(Capabilities);
            binaryWriter.Flush();
        }

        /// <summary>
        /// Called by dotnet-watch.
        /// </summary>
        public static ClientInitializationPayload Read(Stream stream)
        {
            using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = binaryReader.ReadByte();
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var capabilities = binaryReader.ReadString();
            return new ClientInitializationPayload(capabilities);
        }
    }

    internal readonly struct StaticAssetPayload(
        string assemblyName,
        string relativePath,
        ReadOnlyMemory<byte> contents,
        bool replyExpected,
        bool isApplicationProject)
    {
        private const byte Version = 0;
        private const byte Type = (byte)PayloadType.StaticAssetUpdate;

        // If this is set to true, the caller is expecting a success/failure reply to be sent.
        public bool ReplyExpected { get; } = replyExpected;
        public string AssemblyName { get; } = assemblyName;
        public bool IsApplicationProject { get; } = isApplicationProject;
        public string RelativePath { get; } = relativePath;
        public ReadOnlyMemory<byte> Contents { get; } = contents;

        public async ValueTask WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            var syncStream = new MemoryStream();

            using (var binaryWriter = new BinaryWriter(syncStream, Encoding.UTF8, leaveOpen: true))
            {
                binaryWriter.Write(Type);
                binaryWriter.Write(Version);
                binaryWriter.Write(ReplyExpected);
                binaryWriter.Write(AssemblyName);
                binaryWriter.Write(IsApplicationProject);
                binaryWriter.Write(RelativePath);
                binaryWriter.Write(Contents.Length);
            }

            syncStream.Position = 0;
            await syncStream.CopyToAsync(stream, cancellationToken);
            await stream.WriteAsync(Contents, cancellationToken);
        }

        public static async ValueTask<StaticAssetPayload> ReadAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var version = binaryReader.ReadByte();
            if (version != Version)
            {
                throw new NotSupportedException($"Unsupported version {version}.");
            }

            var replyExpected = binaryReader.ReadBoolean();
            var assemblyName = binaryReader.ReadString();
            var isAppProject = binaryReader.ReadBoolean();
            var relativePath = binaryReader.ReadString();
            var contents = await binaryReader.ReadBytesAsync(cancellationToken);

            return new StaticAssetPayload(
                assemblyName: assemblyName,
                relativePath: relativePath,
                contents: contents,
                replyExpected: replyExpected,
                isApplicationProject: isAppProject);
        }
    }

    internal static class BinaryReaderWriterExtesions
    {
        public static ValueTask WriteAsync(this BinaryWriter binaryWriter, byte[] bytes, CancellationToken cancellationToken)
        {
            binaryWriter.Write(bytes.Length);
            binaryWriter.Flush();
            return binaryWriter.BaseStream.WriteAsync(bytes, cancellationToken);
        }

        public static void WriteArray(this BinaryWriter binaryWriter, int[] values)
        {
            if (values is null)
            {
                binaryWriter.Write(0);
                return;
            }

            binaryWriter.Write(values.Length);
            foreach (var value in values)
            {
                binaryWriter.Write(value);
            }
        }

        public static async ValueTask<byte[]> ReadBytesAsync(this BinaryReader binaryReader, CancellationToken cancellationToken)
        {
            var numBytes = binaryReader.ReadInt32();

            var bytes = new byte[numBytes];

            var read = 0;
            while (read < numBytes)
            {
                read += await binaryReader.BaseStream.ReadAsync(bytes.AsMemory(read), cancellationToken);
            }

            return bytes;
        }

        public static int[] ReadIntArray(this BinaryReader binaryReader)
        {
            var numValues = binaryReader.ReadInt32();
            if (numValues == 0)
            {
                return [];
            }

            var values = new int[numValues];

            for (var i = 0; i < numValues; i++)
            {
                values[i] = binaryReader.ReadInt32();
            }

            return values;
        }
    }
}
