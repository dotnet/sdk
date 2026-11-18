// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal static class ContentStore
{
    public static string ArtifactRoot { get; set; } = EnsureCacheDirectory();
    public static string ContentRoot
    {
        get
        {
            string contentPath = Path.Join(ArtifactRoot, "Content");
            CreateCacheDirectory(contentPath);
            return contentPath;
        }
    }

    public static string TempPath { get; } = Directory.CreateTempSubdirectory().FullName;

    public static string PathForDescriptor(Descriptor descriptor, string? contentRoot = null)
    {
        string digestString = descriptor.Digest;
        string digestValue = DigestUtils.GetEncoded(digestString);

        string extension = descriptor.MediaType switch
        {
            "application/vnd.docker.image.rootfs.diff.tar.gzip"
            or "application/vnd.oci.image.layer.v1.tar+gzip"
            or "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip"
                => ".tar.gz",
            "application/vnd.docker.image.rootfs.diff.tar"
            or "application/vnd.oci.image.layer.v1.tar"
                => ".tar",
            _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnrecognizedMediaType), descriptor.MediaType))
        };

        string descriptorPath = Path.Combine(contentRoot ?? ContentRoot, digestValue) + extension;
        return descriptorPath;
    }

    internal static void CommitBlob(string temporaryPath, string destinationPath, string digest, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && File.Exists(destinationPath))
        {
            // Another publisher may have committed this content while a Windows reader holds it open.
            // Only accept that winner after verifying the complete content.
            using FileStream stream = OpenCommittedBlob(destinationPath, cancellationToken);
            using System.Security.Cryptography.IncrementalHash hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            byte[] buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(buffer, 0, read);
            }
            cancellationToken.ThrowIfCancellationRequested();
            byte[] actual = hash.GetHashAndReset();
            InvalidDigestException.ThrowIfMismatched(DigestUtils.GetEncodedValue(digest), actual);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static FileStream OpenCommittedBlob(string path, CancellationToken cancellationToken)
    {
        long started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return File.OpenRead(path);
            }
            catch (IOException error) when (OperatingSystem.IsWindows()
                && error.HResult == unchecked((int)0x80070020)
                && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(1))
            {
                // A competing atomic replacement can briefly deny read handles too.
                // Retry only that sharing violation; a permanently locked file still fails.
                cancellationToken.WaitHandle.WaitOne(10);
            }
        }
    }

    internal static string GetBlobTemporaryPath(string? contentRoot = null)
    {
        string directory = contentRoot ?? ContentRoot;
        CreateCacheDirectory(directory);
        return Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
    }

    public static string GetTempFile() => Path.Join(TempPath, Path.GetRandomFileName());

    /// <summary>
    /// Gets the path to the user cache directory for SDK container builds, creating it if it does
    /// not already exist.
    /// </summary>
    private static string EnsureCacheDirectory()
    {
        string userCacheDir = GetUserCacheDirectoryPath();
        CreateCacheDirectory(userCacheDir);

        string dotnetCacheDir = Path.Join(userCacheDir, "dotnet");
        CreateCacheDirectory(dotnetCacheDir);

        string containersCacheDir = Path.Join(dotnetCacheDir, "Containers");
        CreateCacheDirectory(containersCacheDir);

        return containersCacheDir;
    }

    /// <summary>
    /// Creates a cache directory with user-scoped permissions based on the OS.
    /// </summary>
    /// <remarks>
    /// Permissions are only set for the leaf directory. The caller is expected to handle
    /// permissions for intermediate directories.
    /// </remarks>
    private static void CreateCacheDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            // Per XDG_CACHE_HOME spec: if the cache directory doesn't exist, try to create it with
            // permissions 0700. If it already exists, don't try to change the permissions.
            // See https://specifications.freedesktop.org/basedir/latest/#referencing
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    /// Gets the user-scoped cache directory, accounting for OS differences.
    /// </summary>
    private static string GetUserCacheDirectoryPath()
    {
        if (OperatingSystem.IsLinux())
        {
            // See "XDG Base Directory Specification":
            // https://specifications.freedesktop.org/basedir/latest/
            string? xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrEmpty(xdgCacheHome) && Path.IsPathFullyQualified(xdgCacheHome))
            {
                return xdgCacheHome;
            }

            // From the spec, if XDG_CACHE_HOME is not set then fall back to ~/.cache
            string userHomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Join(userHomeDirectory, ".cache");
        }

        if (OperatingSystem.IsMacOS())
        {
            // See "macOS Library Directory Details":
            // https://developer.apple.com/library/archive/documentation/FileManagement/Conceptual/FileSystemProgrammingGuide/MacOSXDirectories/MacOSXDirectories.html
            string userHomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Join(userHomeDirectory, "Library", "Caches");
        }

        // On other platforms, use the local application data folder.
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);

        return localAppData;
    }
}
