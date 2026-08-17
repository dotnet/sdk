// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.LocalDaemons;

namespace Microsoft.NET.Build.Containers.Tasks;

internal static class ContainerArchiveCache
{
    private const int FormatVersion = 1;

    public static string ComputeFingerprint(
        CreateNewImage task,
        string baseImageManifestDigest,
        bool baseImageIsResolved,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        Append(hash, FormatVersion.ToString());
        Append(hash, Constants.Version);
        Append(hash, baseImageManifestDigest);
        Append(hash, task.Repository);
        Append(hash, task.WorkingDirectory);
        Append(hash, task.ContainerUser);
        Append(hash, task.ImageFormat);
        Append(hash, task.AppCommandInstruction);
        Append(hash, task.SourceDateEpoch);
        Append(hash, task.GenerateLabels.ToString());
        if (task.GenerateLabels)
        {
            Append(hash, task.GenerateCreatedLabels.ToString());
            Append(hash, task.GenerateDigestLabel.ToString());
            AppendLabels(hash, task.Labels);
        }
        Append(hash, task.SkipPublishing.ToString());
        Append(hash, "ImageTags", task.ImageTags);
        Append(hash, "Entrypoint", task.Entrypoint);
        Append(hash, "EntrypointArgs", task.EntrypointArgs);
        Append(hash, "DefaultArgs", task.DefaultArgs);
        Append(hash, "AppCommand", task.AppCommand);
        Append(hash, "AppCommandArgs", task.AppCommandArgs);
        Append(hash, "ExposedPorts", task.ExposedPorts, "Type");
        Append(hash, "EnvironmentVariables", task.ContainerEnvironmentVariables, "Value");
        if (!baseImageIsResolved)
        {
            Append(hash, task.ContainerRuntimeIdentifier);
            AppendFile(hash, task.RuntimeIdentifierGraphPath, "RuntimeIdentifierGraph", includeMode: false, cancellationToken);
        }

        string publishDirectory = Path.GetFullPath(task.PublishDirectory);
        AppendFile(hash, publishDirectory, ".", includeMode: true, cancellationToken);
        foreach (string path in Directory.EnumerateFileSystemEntries(publishDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(publishDirectory, path), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = Path.GetRelativePath(publishDirectory, path).Replace('\\', '/');
            AppendFile(hash, path, relativePath, includeMode: true, cancellationToken);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static bool TryRestore(CreateNewImage task, string fingerprint)
    {
        if (string.IsNullOrEmpty(task.ArchiveOutputPath)
            || string.IsNullOrEmpty(task.ArchiveIncrementalCachePath)
            || !File.Exists(GetArchiveOutputPath(task))
            || !File.Exists(task.ArchiveIncrementalCachePath))
        {
            return false;
        }

        ArchiveCacheEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<ArchiveCacheEntry>(File.ReadAllText(task.ArchiveIncrementalCachePath));
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        if (entry is null || entry.FormatVersion != FormatVersion || entry.Fingerprint != fingerprint)
        {
            return false;
        }

        try
        {
            if (entry.ArchiveDigest != ComputeFileDigest(GetArchiveOutputPath(task)))
            {
                return false;
            }
        }
        catch (IOException)
        {
            return false;
        }

        task.GeneratedContainerManifest = entry.Manifest;
        task.GeneratedContainerConfiguration = entry.Configuration;
        task.GeneratedContainerDigest = entry.ManifestDigest;
        task.GeneratedArchiveOutputPath = task.ArchiveOutputPath;
        task.GeneratedContainerMediaType = entry.ManifestMediaType;
        task.GeneratedContainerNames = entry.ContainerNames.Select(name => new TaskItem(name)).ToArray();
        if (entry.DigestLabel is not null)
        {
            TaskItem label = new(entry.DigestLabel.Name);
            label.SetMetadata("Value", entry.DigestLabel.Value);
            task.GeneratedDigestLabel = label;
        }

        return true;
    }

    public static void Save(CreateNewImage task, string fingerprint)
    {
        if (string.IsNullOrEmpty(task.ArchiveOutputPath)
            || string.IsNullOrEmpty(task.ArchiveIncrementalCachePath)
            || !File.Exists(GetArchiveOutputPath(task)))
        {
            return;
        }

        string? cacheDirectory = Path.GetDirectoryName(task.ArchiveIncrementalCachePath);
        if (!string.IsNullOrEmpty(cacheDirectory))
        {
            Directory.CreateDirectory(cacheDirectory);
        }

        DigestLabel? digestLabel = task.GeneratedDigestLabel is null
            ? null
            : new(task.GeneratedDigestLabel.ItemSpec, task.GeneratedDigestLabel.GetMetadata("Value"));
        ArchiveCacheEntry entry = new(
            FormatVersion,
            fingerprint,
            ComputeFileDigest(GetArchiveOutputPath(task)),
            task.GeneratedContainerManifest,
            task.GeneratedContainerConfiguration,
            task.GeneratedContainerDigest,
            task.GeneratedContainerMediaType,
            task.GeneratedContainerNames.Select(item => item.ItemSpec).ToArray(),
            digestLabel);
        string temporaryPath = $"{task.ArchiveIncrementalCachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entry));
            File.Move(temporaryPath, task.ArchiveIncrementalCachePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static string GetArchiveOutputPath(CreateNewImage task)
        => ArchiveFileRegistry.GetArchiveOutputPath(task.ArchiveOutputPath, task.Repository);

    private static void AppendLabels(IncrementalHash hash, IEnumerable<ITaskItem> labels)
    {
        ITaskItem[] labelArray = labels.ToArray();
        Append(hash, "Labels");
        Append(hash, labelArray.Length.ToString());
        foreach (ITaskItem label in labelArray)
        {
            Append(hash, label.ItemSpec);
            Append(hash, label.GetMetadata("Value"));
        }
    }

    private static void Append(IncrementalHash hash, string name, IEnumerable<ITaskItem> items, string? metadataName = null)
    {
        ITaskItem[] itemArray = items.ToArray();
        Append(hash, name);
        Append(hash, itemArray.Length.ToString());
        foreach (ITaskItem item in itemArray)
        {
            Append(hash, item.ItemSpec);
            if (metadataName is not null)
            {
                Append(hash, item.GetMetadata(metadataName));
            }
        }
    }

    private static void Append(IncrementalHash hash, string name, IEnumerable<string> values)
    {
        string[] valueArray = values.ToArray();
        Append(hash, name);
        Append(hash, valueArray.Length.ToString());
        foreach (string value in valueArray)
        {
            Append(hash, value);
        }
    }

    private static void AppendFile(
        IncrementalHash hash,
        string path,
        string identity,
        bool includeMode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Append(hash, identity);
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        Append(hash, info is DirectoryInfo ? "directory" : "file");
        if (includeMode)
        {
            Append(hash, ((int)Layer.DetermineFileMode(info)).ToString());
        }

        if (info is FileInfo)
        {
            using FileStream stream = File.OpenRead(path);
            Append(hash, stream.Length.ToString());
            byte[] buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
            }
        }
    }

    private static void Append(IncrementalHash hash, string? value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string ComputeFileDigest(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private sealed record ArchiveCacheEntry(
        int FormatVersion,
        string Fingerprint,
        string ArchiveDigest,
        string Manifest,
        string Configuration,
        string ManifestDigest,
        string ManifestMediaType,
        string[] ContainerNames,
        DigestLabel? DigestLabel);

    private sealed record DigestLabel(string Name, string Value);
}
