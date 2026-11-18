// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>Reuses deterministic application layers without skipping source or blob validation.</summary>
internal static partial class LayerInputCache
{
    private sealed record Entry(string Fingerprint, string Digest, string UncompressedDigest, string MediaType, long Size);

    // This metadata key can only reject a hit. An existing candidate still needs
    // the exact canonical tar fingerprint and both cached-blob digest checks.
    internal static string? CandidateKey((FileSystemInfo file, string containerPath)[] files,
        string containerPath, bool windowsLayer, string mediaType, int? userId,
        DateTimeOffset modificationTime, CancellationToken cancellationToken)
    {
        try
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Add("1");
            Add(Constants.Version);
            Add(Environment.Version.ToString());
            Add(System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
            Add(containerPath);
            Add(windowsLayer.ToString());
            Add(mediaType);
            Add(userId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
            Add(modificationTime.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var item in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Add(item.containerPath);
                // Source timestamps are normalized by the explicit epoch. Do not
                // reject identical content merely because it was copied or touched.
                Add(item.file is FileInfo file ? file.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) : "directory");
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());

            void Add(string value)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                Span<byte> length = stackalloc byte[sizeof(int)];
                BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
                hash.AppendData(length);
                hash.AppendData(bytes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Source validation and its original errors belong to the tar writer.
            return null;
        }
    }

    internal static bool HasCandidate(string key, string contentRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using FileStream file = File.OpenRead(CandidatePath(key, contentRoot));
            Span<byte> bytes = stackalloc byte[65];
            int read = file.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            cancellationToken.ThrowIfCancellationRequested();
            if (read != 64) return false;
            foreach (byte value in bytes[..64])
            {
                if (!char.IsAsciiHexDigit((char)value)) return false;
            }
            return File.Exists(CachePath(Encoding.ASCII.GetString(bytes[..64]), contentRoot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static void SaveCandidate(string key, string fingerprint, string contentRoot, CancellationToken cancellationToken)
        => SaveText(CandidatePath(key, contentRoot), fingerprint, contentRoot, cancellationToken);

    internal static string Fingerprint(string uncompressedDigest, string mediaType)
    {
        mediaType = mediaType switch
        {
            SchemaTypes.OciManifestV1 => SchemaTypes.OciLayerGzipV1,
            SchemaTypes.DockerManifestV2 => SchemaTypes.DockerLayerGzip,
            _ => mediaType
        };
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string value in new[] { "2", Constants.Version, Environment.Version.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier, mediaType, uncompressedDigest })
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static Layer? TryRestore(string fingerprint, string mediaType, string contentRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Entry? entry = JsonSerializer.Deserialize(File.ReadAllText(CachePath(fingerprint, contentRoot)), LayerCacheJson.Default.Entry);
            if (entry is null || entry.Fingerprint != fingerprint || entry.MediaType != mediaType || entry.Digest is null || entry.UncompressedDigest is null || entry.Size < 0)
            {
                return null;
            }
            DigestUtils.ValidateSupportedDigestFormat(entry.Digest, out _, out _);
            DigestUtils.ValidateSupportedDigestFormat(entry.UncompressedDigest, out _, out _);
            if (Fingerprint(entry.UncompressedDigest, mediaType) != fingerprint)
            {
                return null;
            }
            Descriptor descriptor = new(entry.MediaType, entry.Digest, entry.Size) { UncompressedDigest = entry.UncompressedDigest };
            string path = ContentStore.PathForDescriptor(descriptor, contentRoot);
            using FileStream file = File.OpenRead(path);
            if (file.Length != entry.Size)
            {
                return null;
            }
            using SHA256 compressedHash = SHA256.Create();
            using CryptoStream compressed = new(file, compressedHash, CryptoStreamMode.Read);
            using IncrementalHash uncompressedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (GZipStream gzip = new(compressed, CompressionMode.Decompress, leaveOpen: true))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = gzip.Read(buffer)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    uncompressedHash.AppendData(buffer, 0, read);
                }
            }
            // Include trailing compressed bytes as well as gzip's trailer.
            byte[] remaining = new byte[81920];
            while (compressed.Read(remaining) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            cancellationToken.ThrowIfCancellationRequested();
            InvalidDigestException.ThrowIfMismatched(DigestUtils.GetEncodedValue(entry.Digest), compressedHash.Hash!);
            InvalidDigestException.ThrowIfMismatched(DigestUtils.GetEncodedValue(entry.UncompressedDigest), uncompressedHash.GetHashAndReset());
            return new(path, descriptor);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDigestException)
        {
            return null;
        }
    }

    internal static void Save(string fingerprint, Layer layer, string contentRoot, CancellationToken cancellationToken)
    {
        Descriptor descriptor = layer.Descriptor;
        SaveText(CachePath(fingerprint, contentRoot),
            JsonSerializer.Serialize(new Entry(fingerprint, descriptor.Digest, descriptor.UncompressedDigest!, descriptor.MediaType, descriptor.Size), LayerCacheJson.Default.Entry),
            contentRoot, cancellationToken);
    }

    private static void SaveText(string path, string content, string contentRoot, CancellationToken cancellationToken)
    {
        string temporary = ContentStore.GetBlobTemporaryPath(contentRoot);
        try
        {
            File.WriteAllText(temporary, content);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cache metadata is optional; a concurrently locked or unwritable entry
            // must not invalidate the successfully generated application layer.
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static string CachePath(string fingerprint, string contentRoot) => Path.Combine(contentRoot, $"layer-input-{fingerprint}.json");

    private static string CandidatePath(string key, string contentRoot) => Path.Combine(contentRoot, $"layer-candidate-{key}.hint");

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(Entry))]
    private partial class LayerCacheJson : JsonSerializerContext { }
}
