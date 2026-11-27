// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal class Layer
{
    // NOTE: The SID string below was created using the following snippet. As the code is Windows only we keep the constant,
    // so that we can author Windows layers successfully on non-Windows hosts.
    //
    // private static string CreateUserOwnerAndGroupSID()
    // {
    //     var descriptor = new RawSecurityDescriptor(
    //         ControlFlags.SelfRelative,
    //         new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
    //         new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
    //         null,
    //         null
    //     );
    //
    //     var raw = new byte[descriptor.BinaryLength];
    //     descriptor.GetBinaryForm(raw, 0);
    //     return Convert.ToBase64String(raw);
    // }

    private const string BuiltinUsersSecurityDescriptor = "AQAAgBQAAAAkAAAAAAAAAAAAAAABAgAAAAAABSAAAAAhAgAAAQIAAAAAAAUgAAAAIQIAAA==";

    public virtual Descriptor Descriptor { get; }

    public string BackingFile { get; }

    internal Layer()
    {
        Descriptor = new Descriptor();
        BackingFile = "";
    }
    internal Layer(string backingFile, Descriptor descriptor)
    {
        BackingFile = backingFile;
        Descriptor = descriptor;
    }

    public static Layer FromDescriptor(Descriptor descriptor, string? contentRoot = null)
    {
        return new(ContentStore.PathForDescriptor(descriptor, contentRoot), descriptor);
    }

    public static Layer FromDirectory(string directory, string containerPath, bool isWindowsLayer, string manifestMediaType, int? userId = null)
        => FromDirectory(directory, containerPath, isWindowsLayer, manifestMediaType, userId, modificationTime: null);

    internal static Layer FromDirectory(
        string directory,
        string containerPath,
        bool isWindowsLayer,
        string manifestMediaType,
        int? userId,
        DateTimeOffset? modificationTime,
        string? contentRoot = null,
        CancellationToken cancellationToken = default,
        bool enableInputCache = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset entryModificationTime = modificationTime ?? DateTimeOffset.UtcNow;
        long fileSize;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> uncompressedHash = stackalloc byte[SHA256.HashSizeInBytes];

        // Docker treats a COPY instruction that copies to a path like `/app` by
        // including `app/` as a directory, with no leading slash. Emulate that here.
        containerPath = containerPath.TrimStart(PathSeparators);

        // For Windows layers we need to put files into a "Files" directory without drive letter.
        if (isWindowsLayer)
        {
            // Cut of drive letter:  /* C:\ */
            if (containerPath[1] == ':')
            {
                containerPath = containerPath[3..];
            }

            containerPath = "Files/" + containerPath;
        }

        // Trim training path separator (if present).
        containerPath = containerPath.TrimEnd(PathSeparators);

        // Use only '/' as directory separator.
        containerPath = containerPath.Replace('\\', '/');

        var entryAttributes = new Dictionary<string, string>();
        if (isWindowsLayer)
        {
            // We grant all users access to the application directory
            // https://github.com/buildpacks/rfcs/blob/main/text/0076-windows-security-identifiers.md
            entryAttributes["MSWINDOWS.rawsd"] = BuiltinUsersSecurityDescriptor;
        }

        var fileList = EnumerateLayerFiles(directory, containerPath);
        string? inputFingerprint = null;
        string? candidateKey = enableInputCache && modificationTime.HasValue
            ? LayerInputCache.CandidateKey(fileList, containerPath, isWindowsLayer, manifestMediaType, userId, entryModificationTime, cancellationToken)
            : null;
        if (candidateKey is not null && LayerInputCache.HasCandidate(candidateKey, contentRoot ?? ContentStore.ContentRoot, cancellationToken))
        {
            // Hash the exact canonical tar bytes without compressing them. On a miss,
            // the actual layer's diff ID proves that the same bytes were compressed.
            // This avoids rereading the source after the layer has been written.
            using LayerTarGZipStream inputStream = new(Stream.Null, leaveOpen: true, cancellationToken, compress: false);
            WriteDirectoryTar(inputStream, directory, containerPath, isWindowsLayer, entryAttributes, userId, entryModificationTime, fileList);
            Span<byte> inputHash = stackalloc byte[SHA256.HashSizeInBytes];
            inputStream.GetCurrentUncompressedHash(inputHash);
            inputFingerprint = LayerInputCache.Fingerprint($"sha256:{Convert.ToHexStringLower(inputHash)}", manifestMediaType);
        }
        string? cachedLayerMediaType = manifestMediaType switch
        {
            SchemaTypes.DockerManifestV2 => SchemaTypes.DockerLayerGzip,
            SchemaTypes.OciManifestV1 => SchemaTypes.OciLayerGzipV1,
            _ => null
        };
        if (inputFingerprint is not null && cachedLayerMediaType is not null
            && LayerInputCache.TryRestore(inputFingerprint, cachedLayerMediaType, contentRoot ?? ContentStore.ContentRoot, cancellationToken) is { } cachedLayer)
        {
            return cachedLayer;
        }

        string tempTarballPath = ContentStore.GetBlobTemporaryPath(contentRoot);
        try
        {
            using (FileStream fs = File.Create(tempTarballPath))
            {
                using SHA256 compressedHash = SHA256.Create();
                using CryptoStream compressedStream = new(fs, compressedHash, CryptoStreamMode.Write, leaveOpen: true);
                using (LayerTarGZipStream layerStream = new(compressedStream, leaveOpen: true, cancellationToken))
                {
                    WriteDirectoryTar(layerStream, directory, containerPath, isWindowsLayer, entryAttributes, userId, entryModificationTime, fileList);

                    int bytesWritten = layerStream.GetCurrentUncompressedHash(uncompressedHash);
                    Debug.Assert(bytesWritten == uncompressedHash.Length);
                }

                // Include gzip's trailer, written by Dispose, without rereading the layer.
                compressedStream.FlushFinalBlock();
                compressedHash.Hash!.CopyTo(hash);
                fileSize = fs.Length;

            }

            string contentHash = Convert.ToHexStringLower(hash);
            string uncompressedContentHash = Convert.ToHexStringLower(uncompressedHash);

            string layerMediaType = manifestMediaType switch
            {
                 // TODO: configurable? gzip always?
                SchemaTypes.DockerManifestV2 => SchemaTypes.DockerLayerGzip,
                SchemaTypes.OciManifestV1 => SchemaTypes.OciLayerGzipV1,
                _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnrecognizedMediaType), manifestMediaType))
            };

            Descriptor descriptor = new()
            {
                MediaType = layerMediaType,
                Size = fileSize,
                Digest = $"sha256:{contentHash}",
                UncompressedDigest = $"sha256:{uncompressedContentHash}",
            };

            string storedContent = ContentStore.PathForDescriptor(descriptor, contentRoot);

            Directory.CreateDirectory(contentRoot ?? ContentStore.ContentRoot);

            ContentStore.CommitBlob(tempTarballPath, storedContent, descriptor.Digest, cancellationToken);

            Layer layer = new(storedContent, descriptor);
            if (candidateKey is not null)
            {
                string fingerprint = LayerInputCache.Fingerprint(descriptor.UncompressedDigest!, manifestMediaType);
                if (inputFingerprint is null || inputFingerprint == fingerprint)
                {
                    LayerInputCache.Save(fingerprint, layer, contentRoot ?? ContentStore.ContentRoot, cancellationToken);
                    LayerInputCache.SaveCandidate(candidateKey, fingerprint, contentRoot ?? ContentStore.ContentRoot, cancellationToken);
                }
            }
            return layer;
        }
        finally
        {
            File.Delete(tempTarballPath);
        }
    }

    private static void WriteDirectoryTar(
        LayerTarGZipStream layerStream, string directory, string containerPath, bool isWindowsLayer,
        IEnumerable<KeyValuePair<string, string>> entryAttributes, int? userId, DateTimeOffset entryModificationTime,
        (FileSystemInfo file, string containerPath)[] fileList)
    {
        using (TarWriter writer = new(layerStream, TarEntryFormat.Pax, leaveOpen: true))
        {
            // Windows layers need a Files folder
            if (isWindowsLayer)
            {
                var entry = new PaxTarEntry(TarEntryType.Directory, "Files", entryAttributes)
                {
                    ModificationTime = entryModificationTime
                };
                WriteEntry(writer, layerStream, entry);
            }

            // Write an entry for the application directory.
            WriteTarEntryForFile(writer, layerStream, new DirectoryInfo(directory), containerPath, entryAttributes, isWindowsLayer ? null : userId, entryModificationTime);

            // Write entries for the application directory contents.
            // The enumeration order of a directory is filesystem-defined, so it is sorted to keep
            // the order of entries in the tar stream stable across machines and builds.
            foreach (var item in fileList)
            {
                WriteTarEntryForFile(writer, layerStream, item.file, item.containerPath, entryAttributes, isWindowsLayer ? null : userId, entryModificationTime);
            }

            // Windows layers need a Hives folder, we do not need to create any Registry Hive deltas inside
            if (isWindowsLayer)
            {
                var entry = new PaxTarEntry(TarEntryType.Directory, "Hives", entryAttributes)
                {
                    ModificationTime = entryModificationTime
                };
                WriteEntry(writer, layerStream, entry);
            }

        } // Dispose of the TarWriter before getting the hash so the final data get written to the tar stream

        static void WriteEntry(TarWriter writer, LayerTarGZipStream layerStream, PaxTarEntry entry)
        {
            layerStream.NormalizeNextHeader();
            writer.WriteEntry(entry);
        }

        // Writes a tar entry corresponding to the file system item.
        static void WriteTarEntryForFile(TarWriter writer, LayerTarGZipStream layerStream, FileSystemInfo file, string containerPath, IEnumerable<KeyValuePair<string, string>> entryAttributes, int? userId, DateTimeOffset modificationTime)
        {
            UnixFileMode mode = DetermineFileMode(file);
            PaxTarEntry entry;

            if (file is FileInfo)
            {
                var fileStream = File.OpenRead(file.FullName);
                entry = new(TarEntryType.RegularFile, containerPath, entryAttributes)
                {
                    DataStream = fileStream,
                };
            }
            else
            {
                entry = new(TarEntryType.Directory, containerPath, entryAttributes);
            }

            entry.Mode = mode;
            entry.ModificationTime = modificationTime;
            if (userId is int uid)
            {
                entry.Uid = uid;
            }

            try
            {
                WriteEntry(writer, layerStream, entry);
            }
            finally
            {
                entry.DataStream?.Dispose();
            }

        }
    }

    private static (FileSystemInfo file, string containerPath)[] EnumerateLayerFiles(string directory, string containerPath)
        => new FileSystemEnumerable<(FileSystemInfo file, string containerPath)>(
            directory,
            (ref FileSystemEntry entry) =>
            {
                FileSystemInfo file = entry.ToFileSystemInfo();
                string relativePath = Path.GetRelativePath(directory, file.FullName).Replace(Path.DirectorySeparatorChar, '/');
                return (file, $"{containerPath}/{relativePath}");
            },
            new EnumerationOptions { AttributesToSkip = FileAttributes.System, RecurseSubdirectories = true })
            .OrderBy(static item => item.containerPath, StringComparer.Ordinal).ToArray();

    internal static UnixFileMode DetermineFileMode(FileSystemInfo file)
    {
        const UnixFileMode nonExecuteMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                            UnixFileMode.GroupRead |
                                            UnixFileMode.OtherRead;
        const UnixFileMode executeMode = nonExecuteMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

        // On Unix, we can determine the x-bit based on the filesystem permission.
        // On Windows, we use executable permissions for all entries.
        return (OperatingSystem.IsWindows() || ((file.UnixFileMode & UnixFileMode.UserExecute) != 0)) ? executeMode : nonExecuteMode;
    }

    internal virtual Stream OpenBackingFile() => File.OpenRead(BackingFile);

    private static readonly char[] PathSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    /// <summary>
    /// Normalizes pax headers while hashing tar bytes and optionally compressing them.
    /// </summary>
    private sealed class LayerTarGZipStream : Stream
    {
        private const int TarBlockSize = 512;
        private const int NameLength = 100;
        private const int ChecksumOffset = 148;
        private const int ChecksumLength = 8;
        private const int TypeFlagOffset = 156;
        private const byte ExtendedHeaderTypeFlag = (byte)'x';

        private static ReadOnlySpan<byte> NormalizedPaxHeaderName => "./PaxHeaders/."u8;

        private readonly IncrementalHash sha256Hash;
        private readonly Stream compressionStream;
        private readonly byte[] headerBlock = new byte[TarBlockSize];

        private int headerBytes;
        private bool normalizeNextHeader;

        private readonly CancellationToken _cancellationToken;

        public LayerTarGZipStream(Stream writeStream, bool leaveOpen, CancellationToken cancellationToken, bool compress = true)
        {
            _cancellationToken = cancellationToken;
            sha256Hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            compressionStream = compress ? new GZipStream(writeStream, CompressionMode.Compress, leaveOpen) : writeStream;
        }

        public override bool CanWrite => true;

        internal void NormalizeNextHeader()
        {
            if (normalizeNextHeader || headerBytes != 0)
            {
                throw new InvalidOperationException("The previous pax header has not been completely written.");
            }

            normalizeNextHeader = true;
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            while (normalizeNextHeader && !buffer.IsEmpty)
            {
                int take = Math.Min(TarBlockSize - headerBytes, buffer.Length);
                buffer[..take].CopyTo(headerBlock.AsSpan(headerBytes));
                headerBytes += take;
                buffer = buffer[take..];

                if (headerBytes == TarBlockSize)
                {
                    NormalizePaxHeader(headerBlock);
                    WriteCore(headerBlock);
                    headerBytes = 0;
                    normalizeNextHeader = false;
                }
            }

            WriteCore(buffer);
        }

        private void WriteCore(ReadOnlySpan<byte> buffer)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            sha256Hash.AppendData(buffer);
            compressionStream.Write(buffer);
        }

        private static void NormalizePaxHeader(Span<byte> header)
        {
            if (header[TypeFlagOffset] != ExtendedHeaderTypeFlag)
            {
                return;
            }

            Span<byte> name = header[..NameLength];
            name.Clear();
            NormalizedPaxHeaderName.CopyTo(name);

            Span<byte> checksumField = header.Slice(ChecksumOffset, ChecksumLength);
            checksumField.Fill((byte)' ');

            int checksum = 0;
            foreach (byte b in header)
            {
                checksum += b;
            }

            for (int i = 5; i >= 0; i--)
            {
                checksumField[i] = (byte)('0' + (checksum & 7));
                checksum >>= 3;
            }

            checksumField[6] = 0;
            checksumField[7] = (byte)' ';
        }

        public override void Flush()
        {
            compressionStream.Flush();
        }

        internal int GetCurrentUncompressedHash(Span<byte> buffer) => sha256Hash.GetCurrentHash(buffer);

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (headerBytes > 0)
                {
                    WriteCore(headerBlock.AsSpan(0, headerBytes));
                    headerBytes = 0;
                }
            }
            finally
            {
                try
                {
                    compressionStream.Dispose();
                }
                finally
                {
                    sha256Hash.Dispose();
                    base.Dispose(disposing);
                }
            }
        }

        // This class is never used with async writes, but if it ever is, implement these overrides
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => throw new NotImplementedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
    }
}
