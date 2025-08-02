// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Security.Cryptography;
using System.Threading.Tasks;
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

    public FileInfo BackingFile { get; }

    internal Layer()
    {
        Descriptor = new Descriptor();
        BackingFile = null!;
    }
    internal Layer(FileInfo backingFile, Descriptor descriptor)
    {
        BackingFile = backingFile;
        Descriptor = descriptor;
    }

    public static Layer FromDescriptor(Descriptor descriptor, ContentStore store)
    {
        FileInfo path = new(store.PathForDescriptor(descriptor));
        return FromBackingFile(path, descriptor);
    }

    public static Layer FromBackingFile(FileInfo backingFile, Descriptor descriptor)
    {
        return new(backingFile, descriptor);
    }

    public static async Task<Layer> FromBackingFile(FileInfo backingFile, string mediaType, string digestType)
    {
        // need to compute the digest from the backing file
        Func<Stream, ValueTask<byte[]>> hasher = digestType switch
        {
            "sha256" => s => SHA256.HashDataAsync(s),
            "sha512" => s => SHA512.HashDataAsync(s),
            _ => throw new ArgumentException(digestType)
        };
        using (FileStream fs = backingFile.OpenRead())
        {
            byte[] digest = await hasher(fs);
            string digestString = Convert.ToHexStringLower(digest);
            return new(backingFile, new Descriptor(mediaType, digestString, fs.Length));
        }
    }

    public static async Task<Descriptor> DescriptorFromStream(Stream stream, string mediaType, string digestType)
    {
        // need to compute the digest from the stream
        Func<Stream, ValueTask<byte[]>> hasher = digestType switch
        {
            "sha256" => s => SHA256.HashDataAsync(s),
            "sha512" => s => SHA512.HashDataAsync(s),
            _ => throw new ArgumentException(digestType)
        };
        byte[] digest = await hasher(stream);
        string digestString = Convert.ToHexStringLower(digest);
        return new Descriptor(mediaType, digestString, stream.Length);
    }

    public static async Task<Layer> FromStream(Stream stream, string mediaType, string digestType)
    {
        Descriptor descriptor = await DescriptorFromStream(stream, mediaType, digestType);
        return new Layer(null!, descriptor);
    }

    public static async Task<Layer> FromFiles((string absPath, string relPath)[] inputFiles, string containerPath, bool isWindowsLayer, string manifestMediaType, ContentStore store, FileInfo layerWritePath, CancellationToken ct, int? userId = null)
    {
        long fileSize;
        var hash = MemoryPool<byte>.Shared.Rent(SHA256.HashSizeInBytes);
        var uncompressedHash = MemoryPool<byte>.Shared.Rent(SHA256.HashSizeInBytes);
        int? resolvedUserId = isWindowsLayer ? null : userId;

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

        string tempTarballPath = store.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            using (HashDigestGZipStream gz = new(fs, leaveOpen: true))
            {
                using (TarWriter writer = new(gz, TarEntryFormat.Pax, leaveOpen: true))
                {
                    // need to track directories that we have created so we only write each one once.
                    // files that we will not include intermediate directories, so we need to be on the lookout
                    HashSet<string> createdDirectories = new();
                    // Windows layers need a Files folder
                    if (isWindowsLayer)
                    {
                        var entry = new PaxTarEntry(TarEntryType.Directory, "Files", entryAttributes);
                        await writer.WriteEntryAsync(entry, ct);
                    }

                    // Write an entry for the container working directory.
                    if (!string.IsNullOrEmpty(containerPath))
                    {
                        var workingDirectoryEntry = new PaxTarEntry(TarEntryType.Directory, containerPath, entryAttributes)
                        {
                            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                   UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                   UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                        };
                        if (resolvedUserId is int uid)
                        {
                            workingDirectoryEntry.Uid = uid;
                        }
                        await writer.WriteEntryAsync(workingDirectoryEntry, ct);
                    }

                    // Write entries for the application directory contents.
                    foreach ((string absolutePath, string containerRelativePath) in inputFiles)
                    {
                        var file = new FileInfo(absolutePath);
                        var adjustedRelativePath = OperatingSystem.IsWindows() ? containerRelativePath.Replace('\\', '/') : containerRelativePath;
                        var finalRelativePath = $"{containerPath}/{adjustedRelativePath.TrimStart(PathSeparators)}";
                        if (!createdDirectories.Contains(Path.GetDirectoryName(finalRelativePath)!))
                        {
                            await DiscoverAndAddIntermediateDirectories(writer, finalRelativePath, entryAttributes, createdDirectories, resolvedUserId, ct);

                        }
                        await WriteTarEntryForFile(writer, file, finalRelativePath, entryAttributes, resolvedUserId, ct);
                    }

                    // Windows layers need a Hives folder, we do not need to create any Registry Hive deltas inside
                    if (isWindowsLayer)
                    {
                        var entry = new PaxTarEntry(TarEntryType.Directory, "Hives", entryAttributes);
                        await writer.WriteEntryAsync(entry, ct);
                    }

                } // Dispose of the TarWriter before getting the hash so the final data get written to the tar stream

                int bytesWritten = gz.GetCurrentUncompressedHash(uncompressedHash.Memory);
                Debug.Assert(bytesWritten == uncompressedHash.Memory.Length);
            }

            fileSize = fs.Length;

            fs.Position = 0;

            int bW = await SHA256.HashDataAsync(fs, hash.Memory, ct);
            Debug.Assert(bW == hash.Memory.Length);

            // Writes a tar entry corresponding to the file system item.
            static async Task WriteTarEntryForFile(TarWriter writer, FileSystemInfo file, string containerPath, IEnumerable<KeyValuePair<string, string>> entryAttributes, int? userId, CancellationToken ct)
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
                if (userId is int uid)
                {
                    entry.Uid = uid;
                }

                await writer.WriteEntryAsync(entry);

                if (entry.DataStream is not null)
                {
                    // no longer relying on the `using` of the FileStream, so need to do it manually
                    entry.DataStream.Dispose();
                }

                static UnixFileMode DetermineFileMode(FileSystemInfo file)
                {
                    const UnixFileMode nonExecuteMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                        UnixFileMode.GroupRead |
                                                        UnixFileMode.OtherRead;
                    const UnixFileMode executeMode = nonExecuteMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

                    // On Unix, we can determine the x-bit based on the filesystem permission.
                    // On Windows, we use executable permissions for all entries.
                    return (OperatingSystem.IsWindows() || ((file.UnixFileMode | UnixFileMode.UserExecute) != 0)) ? executeMode : nonExecuteMode;
                }
            }


            static async Task DiscoverAndAddIntermediateDirectories(TarWriter writer, string finalRelativePath, Dictionary<string, string> entryAttributes, HashSet<string> createdDirectories, int? userId, CancellationToken ct)
            {
                string[] pathParts = Path.GetDirectoryName(finalRelativePath)!.Split('/');
                string currentPath = string.Empty;

                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    currentPath = currentPath == "" ? pathParts[i] : $"{currentPath}/{pathParts[i]}";
                    if (!createdDirectories.Contains(currentPath) && !string.IsNullOrEmpty(currentPath))
                    {
                        var dirEntry = new PaxTarEntry(TarEntryType.Directory, currentPath, entryAttributes)
                        {
                            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                        };
                        if (userId is int uid)
                        {
                            dirEntry.Uid = uid;
                        }
                        await writer.WriteEntryAsync(dirEntry, ct);
                        createdDirectories.Add(currentPath);
                    }
                }
            }
        }

        string contentHash = Convert.ToHexStringLower(hash.Memory.Span);
        string uncompressedContentHash = Convert.ToHexStringLower(uncompressedHash.Memory.Span);

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

        string storedContent = store.PathForDescriptor(descriptor);
        var _ = store.ContentRoot;
        // TODO: the publish side of things requires that the layer exists in the content root (because we look it up by digest),
        // but we should ideally store these in the msbuild intermediate path so that we can clean it nicely.
        File.Copy(tempTarballPath, storedContent, overwrite: true);
        File.Move(tempTarballPath, layerWritePath.FullName, overwrite: true);

        return new(layerWritePath, descriptor);
    }

    internal virtual Stream OpenBackingFile() => BackingFile.OpenRead();

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// A stream capable of computing the hash digest of raw uncompressed data while also compressing it.
    /// </summary>
    private sealed class HashDigestGZipStream : Stream
    {
        private readonly IncrementalHash sha256Hash;
        private readonly GZipStream compressionStream;

        public HashDigestGZipStream(Stream writeStream, bool leaveOpen, CompressionMode compressionMode = CompressionMode.Compress)
            : base()
        {
            sha256Hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            compressionStream = new GZipStream(writeStream, compressionMode, leaveOpen);
        }

        public override bool CanWrite => compressionStream.CanWrite;
        public override bool CanRead => compressionStream.CanRead;
        public override bool CanSeek => compressionStream.CanSeek;

        public override void Write(byte[] buffer, int offset, int count)
        {
            sha256Hash.AppendData(buffer, offset, count);
            compressionStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            sha256Hash.AppendData(buffer);
            compressionStream.Write(buffer);
        }

        public override void Flush()
        {
            compressionStream.Flush();
        }

        internal int GetCurrentUncompressedHash(Span<byte> buffer) => sha256Hash.GetCurrentHash(buffer);
        internal int GetCurrentUncompressedHash(Memory<byte> buffer) => sha256Hash.GetCurrentHash(buffer.Span);

        protected override void Dispose(bool disposing)
        {
            try
            {
                sha256Hash.Dispose();
                compressionStream.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            sha256Hash.AppendData(buffer, offset, count);
            return compressionStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            sha256Hash.AppendData(buffer.Span);
            return compressionStream.WriteAsync(buffer, cancellationToken);
        }

        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = compressionStream.Read(buffer, offset, count);
            sha256Hash.AppendData(buffer.AsSpan(offset, read));
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = compressionStream.ReadAsync(buffer, offset, count, cancellationToken);
            sha256Hash.AppendData(buffer.AsSpan(offset, read.Result));
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = compressionStream.ReadAsync(buffer, cancellationToken);
            sha256Hash.AppendData(buffer.Span.Slice(0, read.Result));
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
    }
}
