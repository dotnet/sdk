// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Formats.Tar;
using System.IO.Compression;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// The <c>.version</c> file that identifies the build in an SDK (<c>sdk/*</c>) or shared framework
/// (<c>shared/*/*</c>) folder. This is the only code that locates, reads, or interprets <c>.version</c> files.
/// SDK files hold the commit, product version, RID, build version, and feature band; shared framework files
/// hold only the commit and product version, so a runtime's build identity cannot be recovered from disk.
/// </summary>
internal sealed class LayoutVersionFile
{
    private const string FileName = ".version";
    private const int ProductVersionLine = 1;
    private const int BuildVersionLine = 3;

    private LayoutVersionFile(List<string> lines)
    {
        Content = string.Join('\n', lines);
        ProductVersion = ParseVersionLine(lines, ProductVersionLine);
        BuildVersion = ParseVersionLine(lines, BuildVersionLine);
    }

    /// <summary>The file's non-blank lines, trimmed and joined with <c>\n</c>.</summary>
    public string Content { get; }

    /// <summary>The version the folder is named after, which <c>dotnet --version</c> reports; null if unparseable.</summary>
    public ReleaseVersion? ProductVersion { get; }

    /// <summary>
    /// The SDK's build identity, such as <c>10.0.403-servicing.26523.112</c> for <c>10.0.403</c>; it equals
    /// <see cref="ProductVersion"/> for previews. Null for shared frameworks and unparseable files.
    /// </summary>
    public ReleaseVersion? BuildVersion { get; }

    /// <summary>Returns true if both files describe the same build. Content is compared case-insensitively.</summary>
    public bool IsSameBuild(LayoutVersionFile? other) =>
        other is not null && string.Equals(Content, other.Content, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the <c>.version</c> file of each SDK and shared framework folder in the archive, keyed by subcomponent.</summary>
    public static Dictionary<string, LayoutVersionFile> ReadFromArchive(string archivePath)
    {
        var versions = new Dictionary<string, LayoutVersionFile>(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>Returns the <c>.version</c> file of a folder on disk, or null when it has none or it is blank.</summary>
    public static LayoutVersionFile? ReadFromDirectory(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        using var reader = new StreamReader(path);
        return Read(reader);
    }

    /// <summary>Returns true if the root may hold folders that an archive would install into.</summary>
    public static bool MayOverlap(string installRoot) =>
        Directory.Exists(Path.Combine(installRoot, "sdk")) || Directory.Exists(Path.Combine(installRoot, "shared"));

    /// <summary>Reads <c>.version</c> content, returning null when it has no non-blank lines.</summary>
    internal static LayoutVersionFile? Read(TextReader reader)
    {
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim() is { Length: > 0 } trimmed)
            {
                lines.Add(trimmed);
            }
        }

        return lines.Count == 0 ? null : new LayoutVersionFile(lines);
    }

    private static ReleaseVersion? ParseVersionLine(List<string> lines, int index) =>
        index < lines.Count && ReleaseVersion.TryParse(lines[index], out ReleaseVersion? version) ? version : null;

    private static string? GetSubcomponent(string entryName)
    {
        string name = entryName.Replace('\\', '/');
        name = name.StartsWith("./", StringComparison.Ordinal) ? name[2..] : name;
        if (!name.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("shared/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? subcomponent = SubcomponentResolver.Resolve(name);
        return subcomponent is not null && string.Equals(name, $"{subcomponent}/{FileName}", StringComparison.OrdinalIgnoreCase)
            ? subcomponent
            : null;
    }

    private static void Add(Dictionary<string, LayoutVersionFile> versions, string subcomponent, Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        if (Read(reader) is { } versionFile)
        {
            versions[subcomponent] = versionFile;
        }
    }
}
