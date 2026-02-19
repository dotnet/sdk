// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Maps Win32 HResult codes to short, telemetry-safe error type labels.
/// </summary>
/// <remarks>
/// This custom mapping is necessary because .NET does not expose a public API for converting
/// HResult codes to symbolic names suitable for telemetry. The alternatives were considered:
///
/// - <c>new Win32Exception(errorCode).Message</c> returns localized, human-readable strings
///   (e.g., "Access is denied") that vary by locale, may contain file paths (PII risk),
///   and are not machine-parseable.
///
/// - <c>Marshal.GetExceptionForHR(hr)</c> returns <c>COMException</c> for most HRs, which
///   loses the specific error code information.
///
/// - <c>Marshal.GetPInvokeErrorMessage(errorCode)</c> produces localized messages, same issues.
///
/// - The runtime's <c>Exception::GetHRSymbolicName()</c> (in ex.cpp) does map HRs to symbolic
///   names but is internal C++ code, not exposed to managed code.
///
/// The runtime does throw specific <c>IOException</c> subclasses for some Win32 errors
/// (e.g., <c>FileNotFoundException</c> for ERROR_FILE_NOT_FOUND, <c>DirectoryNotFoundException</c>
/// for ERROR_PATH_NOT_FOUND). These are handled upstream in <see cref="ErrorCodeMapper.GetErrorInfo"/>
/// before reaching the HResult mapper. However, many Win32 errors (sharing violation, disk full,
/// lock violation, etc.) produce a plain <c>IOException</c> with only the HResult set to
/// <c>0x80070000 | win32ErrorCode</c>, not <c>COR_E_IO</c> (0x80131620). For these, the HResult
/// is the only way to determine the specific failure.
/// </remarks>
internal static class HResultMapper
{
    /// <summary>
    /// Gets a short, telemetry-safe error type label and optional Win32 symbolic name from an HResult.
    /// </summary>
    /// <param name="hResult">The HResult from an <see cref="System.IO.IOException"/>.</param>
    /// <returns>
    /// A tuple of (errorType, details) where errorType is a short label like "DiskFull"
    /// and details is the Win32 symbolic name like "ERROR_DISK_FULL" (or a hex string for unknown codes).
    /// </returns>
    internal static (string errorType, string? details) GetErrorTypeFromHResult(int hResult)
    {
        return hResult switch
        {
            // Disk/storage errors
            unchecked((int)0x80070070) => ("DiskFull", "ERROR_DISK_FULL"),
            unchecked((int)0x80070027) => ("DiskFull", "ERROR_HANDLE_DISK_FULL"),

            // Semaphore/concurrency errors
            unchecked((int)0x80070079) => ("SemaphoreTimeout", "ERROR_SEM_TIMEOUT"),

            // Permission errors
            unchecked((int)0x80070005) => ("PermissionDenied", "ERROR_ACCESS_DENIED"),
            unchecked((int)0x80070020) => ("SharingViolation", "ERROR_SHARING_VIOLATION"),
            unchecked((int)0x80070021) => ("LockViolation", "ERROR_LOCK_VIOLATION"),

            // Path errors
            unchecked((int)0x800700CE) => ("PathTooLong", "ERROR_FILENAME_EXCED_RANGE"),
            unchecked((int)0x8007007B) => ("InvalidPath", "ERROR_INVALID_NAME"),
            unchecked((int)0x80070003) => ("PathNotFound", "ERROR_PATH_NOT_FOUND"),
            unchecked((int)0x80070002) => ("FileNotFound", "ERROR_FILE_NOT_FOUND"),

            // File/directory existence errors
            unchecked((int)0x800700B7) => ("AlreadyExists", "ERROR_ALREADY_EXISTS"),
            unchecked((int)0x80070050) => ("FileExists", "ERROR_FILE_EXISTS"),

            // Network errors
            unchecked((int)0x80070035) => ("NetworkPathNotFound", "ERROR_BAD_NETPATH"),
            unchecked((int)0x80070033) => ("NetworkNameDeleted", "ERROR_NETNAME_DELETED"),
            unchecked((int)0x80004005) => ("GeneralFailure", "E_FAIL"),

            // Device/hardware errors
            unchecked((int)0x8007001F) => ("DeviceFailure", "ERROR_GEN_FAILURE"),
            unchecked((int)0x80070057) => ("InvalidParameter", "ERROR_INVALID_PARAMETER"),

            // Default: include raw HResult for debugging
            _ => ("IOException", hResult != 0 ? $"0x{hResult:X8}" : null)
        };
    }
}
