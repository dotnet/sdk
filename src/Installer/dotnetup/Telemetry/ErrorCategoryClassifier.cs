// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Dotnet.Installation;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Classifies errors as <see cref="ErrorCategory.Product"/> or <see cref="ErrorCategory.User"/>.
/// </summary>
/// <remarks>
/// Product errors represent issues we can take action on (bugs, server problems, logic errors).
/// User errors represent issues outside our control (invalid input, disk full, network down).
/// This distinction drives quality metrics — product errors count against our success rate,
/// while user errors are tracked separately.
/// </remarks>
internal static class ErrorCategoryClassifier
{
    /// <summary>
    /// Classifies an IO error by its HResult, returning the error type, category, and optional details
    /// in a single lookup. This avoids a two-step HResult→errorType→category pipeline that could
    /// get out of sync.
    /// </summary>
    internal static (string ErrorType, ErrorCategory Category, string? Details) ClassifyIOErrorByHResult(int hResult)
    {
        return hResult switch
        {
            // Disk/storage errors — user environment
            unchecked((int)0x80070070) => ("DiskFull", ErrorCategory.User, "ERROR_DISK_FULL"),
            unchecked((int)0x80070027) => ("DiskFull", ErrorCategory.User, "ERROR_HANDLE_DISK_FULL"),

            // Permission errors — user environment
            unchecked((int)0x80070005) => ("PermissionDenied", ErrorCategory.User, "ERROR_ACCESS_DENIED"),

            // Path errors — user environment
            unchecked((int)0x8007007B) => ("InvalidPath", ErrorCategory.User, "ERROR_INVALID_NAME"),
            unchecked((int)0x80070003) => ("PathNotFound", ErrorCategory.User, "ERROR_PATH_NOT_FOUND"),

            // Network errors — user environment
            unchecked((int)0x80070035) => ("NetworkPathNotFound", ErrorCategory.User, "ERROR_BAD_NETPATH"),
            unchecked((int)0x80070033) => ("NetworkNameDeleted", ErrorCategory.User, "ERROR_NETNAME_DELETED"),

            // Device/hardware errors — user environment
            unchecked((int)0x8007001F) => ("DeviceFailure", ErrorCategory.User, "ERROR_GEN_FAILURE"),

            // Sharing/lock violations — product issues (our mutex/lock logic)
            unchecked((int)0x80070020) => ("SharingViolation", ErrorCategory.Product, "ERROR_SHARING_VIOLATION"),
            unchecked((int)0x80070021) => ("LockViolation", ErrorCategory.Product, "ERROR_LOCK_VIOLATION"),

            // Semaphore timeout — product issue (our concurrency logic)
            unchecked((int)0x80070079) => ("SemaphoreTimeout", ErrorCategory.Product, "ERROR_SEM_TIMEOUT"),

            // Path too long — product issue (we control install paths)
            unchecked((int)0x800700CE) => ("PathTooLong", ErrorCategory.Product, "ERROR_FILENAME_EXCED_RANGE"),

            // File existence — product issue (we should handle gracefully)
            unchecked((int)0x80070002) => ("FileNotFound", ErrorCategory.Product, "ERROR_FILE_NOT_FOUND"),
            unchecked((int)0x800700B7) => ("AlreadyExists", ErrorCategory.Product, "ERROR_ALREADY_EXISTS"),
            unchecked((int)0x80070050) => ("FileExists", ErrorCategory.Product, "ERROR_FILE_EXISTS"),

            // General failures — product issue
            unchecked((int)0x80004005) => ("GeneralFailure", ErrorCategory.Product, "E_FAIL"),
            unchecked((int)0x80070057) => ("InvalidParameter", ErrorCategory.Product, "ERROR_INVALID_PARAMETER"),

            // Unknown — include raw HResult, assume product
            _ => ("IOException", ErrorCategory.Product, hResult != 0 ? $"0x{hResult:X8}" : null)
        };
    }

    /// <summary>
    /// Classifies a <see cref="DotnetInstallErrorCode"/> as product or user error.
    /// </summary>
    internal static ErrorCategory ClassifyInstallError(DotnetInstallErrorCode errorCode)
    {
        return errorCode switch
        {
            // User errors - bad input or environment issues
            DotnetInstallErrorCode.VersionNotFound => ErrorCategory.User,      // User typed invalid version
            DotnetInstallErrorCode.ReleaseNotFound => ErrorCategory.User,      // User requested non-existent release
            DotnetInstallErrorCode.InvalidChannel => ErrorCategory.User,       // User provided bad channel format
            DotnetInstallErrorCode.PermissionDenied => ErrorCategory.User,     // User needs to elevate/fix permissions
            DotnetInstallErrorCode.DiskFull => ErrorCategory.User,             // User's disk is full
            DotnetInstallErrorCode.NetworkError => ErrorCategory.User,         // User's network issue

            // Product errors - issues we can take action on
            DotnetInstallErrorCode.NoMatchingReleaseFileForPlatform => ErrorCategory.Product,    // Our manifest/logic issue
            DotnetInstallErrorCode.DownloadFailed => ErrorCategory.Product,    // Server or download logic issue
            DotnetInstallErrorCode.HashMismatch => ErrorCategory.Product,      // Corrupted download or server issue
            DotnetInstallErrorCode.ExtractionFailed => ErrorCategory.Product,  // Our extraction code issue
            DotnetInstallErrorCode.ManifestFetchFailed => ErrorCategory.Product, // Server unreachable or CDN issue
            DotnetInstallErrorCode.ManifestParseFailed => ErrorCategory.Product, // Bad manifest or our parsing bug
            DotnetInstallErrorCode.ArchiveCorrupted => ErrorCategory.Product,  // Bad archive from server or download
            DotnetInstallErrorCode.InstallationLocked => ErrorCategory.Product, // Our locking mechanism issue
            DotnetInstallErrorCode.LocalManifestError => ErrorCategory.Product, // File system issue with our manifest
            DotnetInstallErrorCode.LocalManifestCorrupted => ErrorCategory.Product, // Our manifest is corrupt - we should handle
            DotnetInstallErrorCode.Unknown => ErrorCategory.Product,           // Unknown = assume product issue

            _ => ErrorCategory.Product  // Default to product for new codes
        };
    }

    /// <summary>
    /// Classifies an HTTP status code as product or user error.
    /// </summary>
    internal static ErrorCategory ClassifyHttpError(HttpStatusCode? statusCode)
    {
        if (!statusCode.HasValue)
        {
            // No status code usually means network failure - user environment
            return ErrorCategory.User;
        }

        var code = (int)statusCode.Value;
        return code switch
        {
            >= 500 => ErrorCategory.Product,  // 5xx server errors - our infrastructure
            404 => ErrorCategory.User,        // Not found - likely user requested invalid resource
            403 => ErrorCategory.User,        // Forbidden - user environment/permission issue
            401 => ErrorCategory.User,        // Unauthorized - user auth issue
            408 => ErrorCategory.User,        // Request timeout - user network
            429 => ErrorCategory.User,        // Too many requests - user hitting rate limits
            _ => ErrorCategory.Product        // Other 4xx - likely our bug (bad request format, etc.)
        };
    }
}
