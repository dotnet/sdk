// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using static ArchiveExtensions;

public abstract class Archive : IDisposable
{
    public static async Task<Archive> Create(string path)
    {
        if (path.EndsWith(".tar.gz"))
            return await TarArchive.Create(path);
        else if (path.EndsWith(".zip"))
            return ZipFileArchive.Create(path);
        else
            throw new NotSupportedException("Unsupported archive type");
    }

    public abstract bool Contains(string relativePath);

    public abstract string[] GetFileNames();

    public abstract string[] GetFileLines(string relativePath);

    public abstract Task<byte[]> GetFileBytesAsync(string relativePath);

    public abstract void Dispose();

    public class TarArchive : Archive
    {
        private string _extractedFolder;

        private TarArchive(string extractedFolder)
        {
            _extractedFolder = extractedFolder;
        }

        public static async Task<TarArchive> Create(string path, CancellationToken cancellationToken = default)
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

        public override string[] GetFileLines(string relativePath)
        {
            return File.ReadAllLines(Path.Combine(_extractedFolder, relativePath));
        }

        public override Task<byte[]> GetFileBytesAsync(string relativePath)
        {
            var filePath = Path.Combine(_extractedFolder, relativePath);
            if (!File.Exists(filePath))
                return Task.FromResult<byte[]>([]);
            return File.ReadAllBytesAsync(Path.Combine(_extractedFolder, relativePath));
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

        public static new ZipFileArchive Create(string path)
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

        public override string[] GetFileLines(string relativePath)
        {
            var entry = _archive.GetEntry(relativePath);
            if (entry == null)
                throw new ArgumentException("File not found");
            return entry.Lines();
        }
        public override Task<byte[]> GetFileBytesAsync(string relativePath)
        {
            using (var entry = _archive.GetEntry(relativePath)?.Open())
            {
                if (entry == null)
                {
                    return Task.FromResult<byte[]>([]);
                }
                return entry.ReadToEndAsync();
            }
        }

        public override void Dispose()
        {
            _archive.Dispose();
        }
    }
}
