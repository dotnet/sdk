// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch;

/// <summary>
/// Creates temporary files under a private directory in the process temp path.
/// The directory is owner-only, so another user cannot write to files created there or squat on their names.
/// </summary>
internal sealed class TemporaryFileManager : IDisposable
{
    internal readonly struct TemporaryFile(FileStream stream) : IDisposable
    {
        public string Path => stream.Name;

        public void Dispose()
        {
            try
            {
                stream.Dispose();
            }
            catch
            {
                // Ignore exceptions.
            }

            try
            {
                File.Delete(stream.Name);
            }
            catch
            {
                // Ignore exceptions.
            }
        }
    }

    private const string TempDirectoryPrefix = "dotnet-watch-";

    /// <summary>
    /// Private directory in which the manager creates temporary files.
    /// Assumes that the directory returned by <see cref="Path.GetTempPath"/> is user-private on Windows.
    /// </summary>
    private readonly string _directory = Directory.CreateTempSubdirectory(TempDirectoryPrefix).FullName;

    private int _uniqueFileId;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Ignore exceptions.
        }
    }

    /// <summary>
    /// Creates a new temporary file with content produced by <paramref name="writeContent"/>.
    /// The returned file keeps a read-only handle open until disposed, so other processes can read the file
    /// (e.g. using <see cref="File.OpenRead(string)"/>) but cannot modify or delete it.
    /// </summary>
    public TemporaryFile Create(string extension, Action<Stream> writeContent)
    {
        var path = Path.Combine(_directory, Interlocked.Increment(ref _uniqueFileId) + extension);

        try
        {
            using (var writeStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writeContent(writeStream);
            }

            // Readers that request FileShare.Read (such as File.OpenRead) fail if any handle has write access,
            // so the write handle must be closed before the file is handed out.
            return new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }
        catch
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Ignore exceptions.
            }

            throw;
        }
    }
}
