// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public abstract class Archive : IDisposable
{
    public static async Task<Archive> Create(string path, CancellationToken cancellationToken = default)
    {
        if (path.EndsWith(".tar.gz"))
            return await TarArchive.Create(path, cancellationToken);
        else if (path.EndsWith(".zip"))
            return ZipFileArchive.Create(path);
        else
            throw new NotSupportedException("Unsupported archive type");
    }

    public abstract string[] GetFileNames();

    public abstract bool Contains(string relativePath);

    public abstract void Dispose();

    public class TarArchive : Archive
    {
        private string _extractedFolder;

        private TarArchive(string extractedFolder)
        {
            _extractedFolder = extractedFolder;
        }

        public static new async Task<TarArchive> Create(string path, CancellationToken cancellationToken = default)
        {
            var tmpFolder = Directory.CreateTempSubdirectory(nameof(FindArchiveDiffs));
            using (var gzStream = File.OpenRead(path))
            using (var gzipStream = new GZipStream(gzStream, CompressionMode.Decompress))
            {
                await TarFile.ExtractToDirectoryAsync(gzipStream, tmpFolder.FullName, true, cancellationToken);
            }
            return new TarArchive(tmpFolder.FullName);
        }

        public override bool Contains(string relativePath)
        {
            return File.Exists(Path.Combine(_extractedFolder, relativePath));
        }

        public override string[] GetFileNames()
        {
            return Directory.GetFiles(_extractedFolder, "*", SearchOption.AllDirectories).Select(f => f.Substring(_extractedFolder.Length + 1)).ToArray();
        }

        public override void Dispose()
        {
            if (Directory.Exists(_extractedFolder))
                Directory.Delete(_extractedFolder, true);
        }
    }

    public class ZipFileArchive : Archive
    {
        private ZipArchive _archive;

        private ZipFileArchive(ZipArchive archive)
        {
            _archive = archive;
        }

        public static ZipFileArchive Create(string path)
        {
            return new ZipFileArchive(new ZipArchive(File.OpenRead(path)));
        }

        public override bool Contains(string relativePath)
        {
            return _archive.GetEntry(relativePath) != null;
        }

        public override string[] GetFileNames()
        {
            return _archive.Entries.Select(e => e.FullName).ToArray();
        }

        public override void Dispose()
        {
            _archive.Dispose();
        }
    }

    private static string GetArchiveExtension(string path)
    {
        if (path.EndsWith(".tar.gz"))
        {
            return ".tar.gz";
        }
        else if (path.EndsWith(".zip"))
        {
            return ".zip";
        }
        else
        {
            throw new ArgumentException($"Invalid archive extension '{path}': must end with .tar.gz or .zip");
        }
    }

    public static (string Version, string Rid, string extension) GetInfoFromFileName(string filename, string packageName)
    {
        var extension = GetArchiveExtension(filename);
        var Version = VersionIdentifier.GetVersion(filename);
        if (Version is null)
            throw new ArgumentException("Invalid archive file name '{filename}': No valid version found in file name.");
        // Once we've removed the version, package name, and extension, we should be left with the RID
        var Rid = filename
            .Replace(extension, "")
            .Replace(Version, "")
            .Replace(packageName, "")
            .Trim('-', '.', '_');

        // A RID with '.' must have a version number after the first '.' in each part of the RID. For example, alpine.3.10-arm64.
        // Otherwise, it's likely an archive of another type of file that we don't handle here, for example, .msi.wixpack.zip.
        var ridParts = Rid.Split('-');
        foreach(var item in ridParts.SelectMany(p => p.Split('.').Skip(1)))
        {
            if (!int.TryParse(item, out _))
                throw new ArgumentException($"Invalid Rid '{Rid}' in archive file name '{filename}'. Expected RID with '.' to be part of a version. This likely means the file is an archive of a different file type.");
        }
        return (Version, Rid, extension);
    }
}
