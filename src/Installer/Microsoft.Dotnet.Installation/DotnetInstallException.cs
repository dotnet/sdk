// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Dotnet.Installation;

/// <summary>
/// Error codes for .NET installation failures.
/// </summary>
public enum DotnetInstallErrorCode
{
    /// <summary>Unknown error.</summary>
    Unknown,

    /// <summary>The requested version was not found in the releases index.</summary>
    VersionNotFound,

    /// <summary>The requested release was not found.</summary>
    ReleaseNotFound,

    /// <summary>No matching file was found for the platform/architecture.</summary>
    NoMatchingFile,

    /// <summary>Failed to download the archive.</summary>
    DownloadFailed,

    /// <summary>Archive hash verification failed.</summary>
    HashMismatch,

    /// <summary>Failed to extract the archive.</summary>
    ExtractionFailed,

    /// <summary>The channel or version format is invalid.</summary>
    InvalidChannel,

    /// <summary>Network connectivity issue.</summary>
    NetworkError,

    /// <summary>Insufficient permissions.</summary>
    PermissionDenied,

    /// <summary>Disk space issue.</summary>
    DiskFull,

    /// <summary>Failed to fetch the releases manifest from Microsoft servers.</summary>
    ManifestFetchFailed,

    /// <summary>Failed to parse the releases manifest (invalid JSON or schema).</summary>
    ManifestParseFailed,

    /// <summary>The archive file is corrupted or truncated.</summary>
    ArchiveCorrupted,

    /// <summary>Another installation process is already running.</summary>
    InstallationLocked,

    /// <summary>Failed to read/write the dotnetup installation manifest.</summary>
    LocalManifestError,

    /// <summary>The dotnetup installation manifest is corrupted.</summary>
    LocalManifestCorrupted,
}

/// <summary>
/// Exception thrown when a .NET installation operation fails.
/// </summary>
public class DotnetInstallException : Exception
{
    /// <summary>
    /// Gets the error code for this exception.
    /// </summary>
    public DotnetInstallErrorCode ErrorCode { get; }

    /// <summary>
    /// Gets the version that was being installed, if applicable.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the component being installed (SDK, Runtime, etc.).
    /// </summary>
    public string? Component { get; }

    public DotnetInstallException(DotnetInstallErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DotnetInstallException(DotnetInstallErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public DotnetInstallException(DotnetInstallErrorCode errorCode, string message, string? version = null, string? component = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Version = version;
        Component = component;
    }

    public DotnetInstallException(DotnetInstallErrorCode errorCode, string message, Exception innerException, string? version = null, string? component = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Version = version;
        Component = component;
    }
}
