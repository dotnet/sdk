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
    /// Classifies an IO error type (from <see cref="HResultMapper"/>) as product or user error.
    /// </summary>
    internal static ErrorCategory ClassifyIOError(string errorType)
    {
        return errorType switch
        {
            // User environment issues - we can't control these
            "DiskFull" => ErrorCategory.User,
            "PermissionDenied" => ErrorCategory.User,
            "InvalidPath" => ErrorCategory.User,            // User specified invalid path
            "PathNotFound" => ErrorCategory.User,           // User's directory doesn't exist
            "NetworkPathNotFound" => ErrorCategory.User,    // Network issue
            "NetworkNameDeleted" => ErrorCategory.User,     // Network issue
            "DeviceFailure" => ErrorCategory.User,          // Hardware issue

            // Product issues - we should handle these gracefully
            "SharingViolation" => ErrorCategory.Product,    // Could be our mutex/lock issue
            "LockViolation" => ErrorCategory.Product,       // Could be our mutex/lock issue
            "PathTooLong" => ErrorCategory.Product,         // We control the install path
            "SemaphoreTimeout" => ErrorCategory.Product,    // Could be our concurrency issue
            "AlreadyExists" => ErrorCategory.Product,       // We should handle existing files gracefully
            "FileExists" => ErrorCategory.Product,          // We should handle existing files gracefully
            "FileNotFound" => ErrorCategory.Product,        // Our code referenced missing file
            "GeneralFailure" => ErrorCategory.Product,      // Unknown IO error
            "InvalidParameter" => ErrorCategory.Product,    // Our code passed bad params
            "IOException" => ErrorCategory.Product,         // Generic IO - assume product

            _ => ErrorCategory.Product                      // Unknown - assume product
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
