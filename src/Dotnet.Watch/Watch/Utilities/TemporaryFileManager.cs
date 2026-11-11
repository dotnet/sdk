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
        public FileStream Stream => stream;

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

    public TemporaryFile Create(string extension, FileShare share)
        => new(new FileStream(
            path: Path.Combine(_directory, Interlocked.Increment(ref _uniqueFileId) + extension),
            mode: FileMode.CreateNew,
            access: FileAccess.ReadWrite,
            share: share));
}
