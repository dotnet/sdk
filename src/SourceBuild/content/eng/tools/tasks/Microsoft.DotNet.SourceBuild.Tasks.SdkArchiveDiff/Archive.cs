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
            using (var gzStream = File.OpenRead (path))
            using (var gzipStream = new GZipStream (gzStream, CompressionMode.Decompress))
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

    public static (string Version, string Rid, string extension) GetInfoFromArchivePath(string path)
    {
        string extension;
        if (path.EndsWith(".tar.gz"))
        {
            extension = ".tar.gz";
        }
        else if (path.EndsWith(".zip"))
        {
            extension = ".zip";
        }
        else
        {
            throw new ArgumentException($"Invalid archive extension '{path}': must end with .tar.gz or .zip");
        }

        string filename = Path.GetFileName(path)[..^extension.Length];
        var dashDelimitedParts = filename.Split('-');
        var (rid, versionString) = dashDelimitedParts switch
        {
            ["dotnet", "sdk", var first, var second, var third, var fourth] when PathWithVersions.IsVersionString(first) => (third + '-' + fourth, first + '-' + second),
            ["dotnet", "sdk", var first, var second, var third, var fourth] when PathWithVersions.IsVersionString(third) => (first + '-' + second, third + '-' + fourth),
            _ => throw new ArgumentException($"Invalid archive file name '{filename}': file name should include full build version and rid in the format dotnet-sdk-<version>-<rid>{extension} or dotnet-sdk-<rid>-<version>{extension}")
        };

        return (versionString, rid, extension);
    }
}
