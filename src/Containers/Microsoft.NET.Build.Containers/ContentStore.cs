// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    public static string PathForDescriptor(Descriptor descriptor)
    {
        string digestValue = DigestUtils.GetEncoded(descriptor.Digest);

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

        return GetPathForHash(digestValue) + extension;
    }


    public static string GetPathForHash(string contentHash)
    {
        return Path.Combine(ContentRoot, contentHash);
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
