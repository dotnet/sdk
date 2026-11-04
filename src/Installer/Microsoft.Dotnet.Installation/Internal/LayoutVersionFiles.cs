// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Reads the <c>.version</c> files that identify the build in each SDK (<c>sdk/*</c>) and shared framework
/// (<c>shared/*/*</c>) folder. SDK files hold the commit, version, RID, build version, and feature band;
/// shared framework files hold the commit and version.
/// </summary>
internal static class LayoutVersionFiles
{
    private const string VersionFileName = ".version";

    /// <summary>Returns the normalized <c>.version</c> content of each SDK and shared framework folder in the archive, keyed by subcomponent.</summary>
    public static Dictionary<string, string> ReadFromArchive(string archivePath)
    {
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                if (GetSubcomponent(entry.FullName) is { } subcomponent)
                {
                    using var stream = entry.Open();
                    Add(versions, subcomponent, stream);
                }
            }

            return versions;
        }

        using var archiveStream = File.OpenRead(archivePath);
        using Stream tarStream = archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(archiveStream, CompressionMode.Decompress)
            : archiveStream;
        using var tarReader = new TarReader(tarStream);
        while (tarReader.GetNextEntry() is { } entry)
        {
            if (entry.DataStream is not null && GetSubcomponent(entry.Name) is { } subcomponent)
            {
                Add(versions, subcomponent, entry.DataStream);
            }
        }

        return versions;
    }

    /// <summary>Returns the normalized <c>.version</c> content of a folder on disk, or null when it has none.</summary>
    public static string? ReadFromDirectory(string directory)
    {
        string path = Path.Combine(directory, VersionFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        using var reader = new StreamReader(path);
        return Normalize(reader);
    }

    /// <summary>Returns true if the root may hold folders that an archive would install into.</summary>
    public static bool MayOverlap(string installRoot) =>
        Directory.Exists(Path.Combine(installRoot, "sdk")) || Directory.Exists(Path.Combine(installRoot, "shared"));

    private static string? GetSubcomponent(string entryName)
    {
        string name = entryName.Replace('\\', '/');
        name = name.StartsWith("./", StringComparison.Ordinal) ? name[2..] : name;
        if (!name.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("shared/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? subcomponent = SubcomponentResolver.Resolve(name);
        return subcomponent is not null && string.Equals(name, $"{subcomponent}/{VersionFileName}", StringComparison.OrdinalIgnoreCase)
            ? subcomponent
            : null;
    }

    private static void Add(Dictionary<string, string> versions, string subcomponent, Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        if (Normalize(reader) is { } content)
        {
            versions[subcomponent] = content;
        }
    }

    private static string? Normalize(TextReader reader)
    {
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim() is { Length: > 0 } trimmed)
            {
                lines.Add(trimmed);
            }
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }
}
