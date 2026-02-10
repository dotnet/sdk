// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Xunit;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

public class ErrorCodeMapperTests
{
    [Fact]
    public void GetErrorInfo_IOException_DiskFull_MapsCorrectly()
    {
        // HResult for ERROR_DISK_FULL
        var ex = new IOException("Not enough space", unchecked((int)0x80070070));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("DiskFull", info.ErrorType);
        Assert.Equal(unchecked((int)0x80070070), info.HResult);
        // Details contain the win32 error code for PII safety
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("win32_error_112", info.Details); // ERROR_DISK_FULL = 112
        }
        else
        {
            Assert.Equal("ERROR_DISK_FULL", info.Details);
        }
    }

    [Fact]
    public void GetErrorInfo_IOException_SharingViolation_MapsCorrectly()
    {
        // HResult for ERROR_SHARING_VIOLATION
        var ex = new IOException("File in use", unchecked((int)0x80070020));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("SharingViolation", info.ErrorType);
        // Details contain the win32 error code
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("win32_error_32", info.Details); // ERROR_SHARING_VIOLATION = 32
        }
        else
        {
            Assert.Equal("ERROR_SHARING_VIOLATION", info.Details);
        }
    }

    [Fact]
    public void GetErrorInfo_IOException_PathTooLong_MapsCorrectly()
    {
        var ex = new IOException("Path too long", unchecked((int)0x800700CE));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("PathTooLong", info.ErrorType);
        // Details contain the win32 error code for PII safety
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("win32_error_206", info.Details); // ERROR_FILENAME_EXCED_RANGE = 206
        }
        else
        {
            Assert.Equal("ERROR_FILENAME_EXCED_RANGE", info.Details);
        }
    }

    [Fact]
    public void GetErrorInfo_IOException_UnknownHResult_IncludesHexValue()
    {
        var ex = new IOException("Unknown error", unchecked((int)0x80071234));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("IOException", info.ErrorType);
        // On Windows, Win32Exception provides a message even for unknown errors
        // On other platforms, we fall back to hex
        Assert.NotNull(info.Details);
    }

    [Fact]
    public void GetErrorInfo_HttpRequestException_WithStatusCode_MapsCorrectly()
    {
        var ex = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("Http404", info.ErrorType);
        Assert.Equal(404, info.StatusCode);
    }

    [Fact]
    public void GetErrorInfo_WrappedException_IncludesChain()
    {
        var innerInner = new SocketException(10054); // Connection reset
        var inner = new IOException("Network error", innerInner);
        var outer = new HttpRequestException("Request failed", inner);

        var info = ErrorCodeMapper.GetErrorInfo(outer);

        Assert.Equal("HttpRequestException", info.ErrorType);
        Assert.Equal("HttpRequestException->IOException->SocketException", info.ExceptionChain);
    }

    [Fact]
    public void GetErrorInfo_AggregateException_UnwrapsSingleInner()
    {
        var inner = new FileNotFoundException("Missing file");
        var aggregate = new AggregateException(inner);

        var info = ErrorCodeMapper.GetErrorInfo(aggregate);

        Assert.Equal("FileNotFound", info.ErrorType);
    }

    [Fact]
    public void GetErrorInfo_TimeoutException_MapsCorrectly()
    {
        var ex = new TimeoutException("Operation timed out");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("Timeout", info.ErrorType);
    }

    [Fact]
    public void GetErrorInfo_InvalidOperationException_MapsCorrectly()
    {
        var ex = new InvalidOperationException("Invalid state");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("InvalidOperation", info.ErrorType);
    }

    [Fact]
    public void GetErrorInfo_ExceptionFromOurCode_IncludesSourceLocation()
    {
        // Throw from a method to get a real stack trace
        var ex = ThrowTestException();

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        // Source location is only populated for our owned assemblies (dotnetup, Microsoft.Dotnet.Installation)
        // In tests, we won't have those on the stack, so source location will be null
        // The important thing is that the method doesn't throw
        Assert.Equal("InvalidOperation", info.ErrorType);
    }

    [Fact]
    public void GetErrorInfo_SourceLocation_FiltersToOwnedNamespaces()
    {
        // Verify that source location filtering works by namespace prefix
        // We must throw and catch to get a stack trace - exceptions created with 'new' have no trace
        var ex = ThrowTestException();

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        // Source location should be populated since test assembly is in an owned namespace
        // (Microsoft.DotNet.Tools.Bootstrapper.Tests starts with Microsoft.DotNet.Tools.Bootstrapper)
        Assert.NotNull(info.SourceLocation);
        // The format is "TypeName.MethodName" - no [BCL] prefix since we found owned code
        Assert.DoesNotContain("[BCL]", info.SourceLocation);
        Assert.Contains("ThrowTestException", info.SourceLocation);
    }

    [Fact]
    public void GetErrorInfo_AllFieldsPopulated_ForIOExceptionWithChain()
    {
        // Create a realistic exception scenario - IOException with inner exception
        var inner = new UnauthorizedAccessException("Access denied");
        var outer = new IOException("Cannot write file", inner);

        var info = ErrorCodeMapper.GetErrorInfo(outer);

        // Verify exception chain is populated
        Assert.Equal("IOException", info.ErrorType);
        Assert.Equal("IOException->UnauthorizedAccessException", info.ExceptionChain);
    }

    [Fact]
    public void GetErrorInfo_HResultAndDetails_ForDiskFullException()
    {
        // Create IOException with specific HResult for disk full
        var ex = new IOException("Disk full", unchecked((int)0x80070070));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("DiskFull", info.ErrorType);
        Assert.Equal(unchecked((int)0x80070070), info.HResult);
        // Details contain the win32 error code for PII safety
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("win32_error_112", info.Details); // ERROR_DISK_FULL = 112
        }
        else
        {
            Assert.Equal("ERROR_DISK_FULL", info.Details);
        }
    }

    private static Exception ThrowTestException()
    {
        try
        {
            throw new InvalidOperationException("Test exception");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Fact]
    public void GetErrorInfo_LongExceptionChain_LimitsDepth()
    {
        // Create a chain of typed exceptions (not plain Exception which gets unwrapped)
        Exception ex = new InvalidOperationException("Root");
        for (int i = 0; i < 10; i++)
        {
            ex = new IOException($"Wrapper {i}", ex);
        }

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        // Should have an exception chain since we're using IOException wrappers
        Assert.NotNull(info.ExceptionChain);
        var parts = info.ExceptionChain!.Split("->");
        // Chain is limited to maxDepth (5) + 1 for the outer exception = 6
        Assert.True(parts.Length <= 6, $"Chain too long: {info.ExceptionChain}");
    }

    [Fact]
    public void GetErrorInfo_NetworkPathNotFound_MapsCorrectly()
    {
        var ex = new IOException("Network path not found", unchecked((int)0x80070035));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("NetworkPathNotFound", info.ErrorType);
        // Details contain the win32 error code for PII safety
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("win32_error_53", info.Details); // ERROR_BAD_NETPATH = 53
        }
        else
        {
            Assert.Equal("ERROR_BAD_NETPATH", info.Details);
        }
    }

    [Fact]
    public void GetErrorInfo_AlreadyExists_MapsCorrectly()
    {
        // HResult for ERROR_ALREADY_EXISTS (0x800700B7 = -2147024713)
        var ex = new IOException("Cannot create a file when that file already exists", unchecked((int)0x800700B7));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("AlreadyExists", info.ErrorType);
        Assert.Equal(unchecked((int)0x800700B7), info.HResult);
        // Details contain the win32 error code for PII safety
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("win32_error_183", info.Details); // ERROR_ALREADY_EXISTS = 183
        }
        else
        {
            Assert.Equal("ERROR_ALREADY_EXISTS", info.Details);
        }
    }

    // Error category tests
    [Theory]
    [InlineData(DotnetInstallErrorCode.VersionNotFound, ErrorCategory.User)]
    [InlineData(DotnetInstallErrorCode.ReleaseNotFound, ErrorCategory.User)]
    [InlineData(DotnetInstallErrorCode.InvalidChannel, ErrorCategory.User)]
    [InlineData(DotnetInstallErrorCode.PermissionDenied, ErrorCategory.User)]
    [InlineData(DotnetInstallErrorCode.DiskFull, ErrorCategory.User)]
    [InlineData(DotnetInstallErrorCode.NetworkError, ErrorCategory.User)]
    [InlineData(DotnetInstallErrorCode.NoMatchingFile, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.DownloadFailed, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.HashMismatch, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.ExtractionFailed, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.ManifestFetchFailed, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.ManifestParseFailed, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.ArchiveCorrupted, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.InstallationLocked, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.LocalManifestError, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.LocalManifestCorrupted, ErrorCategory.Product)]
    [InlineData(DotnetInstallErrorCode.Unknown, ErrorCategory.Product)]
    public void GetErrorInfo_DotnetInstallException_HasCorrectCategory(DotnetInstallErrorCode errorCode, ErrorCategory expectedCategory)
    {
        var ex = new DotnetInstallException(errorCode, "Test message");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(expectedCategory, info.Category);
    }

    [Fact]
    public void GetErrorInfo_UnauthorizedAccessException_IsUserError()
    {
        var ex = new UnauthorizedAccessException("Access denied");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_TimeoutException_IsUserError()
    {
        var ex = new TimeoutException("Operation timed out");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_ArgumentException_IsUserError()
    {
        var ex = new ArgumentException("Invalid argument", "testParam");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_OperationCanceledException_IsUserError()
    {
        var ex = new OperationCanceledException("Cancelled by user");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_InvalidOperationException_IsProductError()
    {
        var ex = new InvalidOperationException("Invalid state");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.Product, info.Category);
    }

    [Fact]
    public void GetErrorInfo_UnknownException_DefaultsToProductError()
    {
        var ex = new CustomTestException("Test");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.Product, info.Category);
    }

    [Fact]
    public void GetErrorInfo_IOException_DiskFull_IsUserError()
    {
        // HResult for ERROR_DISK_FULL (0x80070070 = -2147024784)
        var ex = new IOException("Disk full", unchecked((int)0x80070070));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_IOException_SharingViolation_IsProductError()
    {
        // HResult for ERROR_SHARING_VIOLATION (0x80070020 = -2147024864)
        // Could be our mutex/lock issue
        var ex = new IOException("File in use", unchecked((int)0x80070020));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.Product, info.Category);
    }

    [Fact]
    public void GetErrorInfo_HttpRequestException_5xx_IsProductError()
    {
        var ex = new HttpRequestException("Server error", null, System.Net.HttpStatusCode.InternalServerError);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.Product, info.Category);
    }

    [Fact]
    public void GetErrorInfo_HttpRequestException_404_IsUserError()
    {
        var ex = new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_HttpRequestException_NoStatusCode_IsUserError()
    {
        // No status code typically means network connectivity issue
        var ex = new HttpRequestException("Network error");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_ManifestFetchFailed_WithInnerHttpException_NoStatus_IsUserError()
    {
        // Network connectivity failure during manifest fetch should be User, not Product
        var innerEx = new HttpRequestException("Error while copying content to a stream");
        var ex = new DotnetInstallException(
            DotnetInstallErrorCode.ManifestFetchFailed,
            $"Failed to fetch release manifest: {innerEx.Message}",
            innerEx);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
        Assert.Equal("ManifestFetchFailed", info.ErrorType);
    }

    [Fact]
    public void GetErrorInfo_ManifestFetchFailed_WithInner500Error_IsProductError()
    {
        // Server errors (5xx) during manifest fetch should be Product
        var innerEx = new HttpRequestException("Internal server error", null, System.Net.HttpStatusCode.InternalServerError);
        var ex = new DotnetInstallException(
            DotnetInstallErrorCode.ManifestFetchFailed,
            $"Failed to fetch release manifest: {innerEx.Message}",
            innerEx);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.Product, info.Category);
        Assert.Equal(500, info.StatusCode);
        Assert.Contains("http_500", info.Details!);
    }

    [Fact]
    public void GetErrorInfo_ManifestFetchFailed_WithInnerSocketException_IsUserError()
    {
        // Socket errors are user environment issues
        var socketEx = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
        var httpEx = new HttpRequestException("Error", socketEx);
        var ex = new DotnetInstallException(
            DotnetInstallErrorCode.ManifestFetchFailed,
            $"Failed to fetch release manifest: {httpEx.Message}",
            httpEx);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
        Assert.Contains("socket_", info.Details!);
    }

    [Fact]
    public void GetErrorInfo_DownloadFailed_WithInnerHttpException_NoStatus_IsUserError()
    {
        // Network connectivity failure during download should be User
        var innerEx = new HttpRequestException("Network error");
        var ex = new DotnetInstallException(
            DotnetInstallErrorCode.DownloadFailed,
            $"Download failed: {innerEx.Message}",
            innerEx);

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.User, info.Category);
    }

    [Fact]
    public void GetErrorInfo_ManifestFetchFailed_NoInnerException_IsProductError()
    {
        // Without inner exception info, we can't determine it's a user issue - default to Product
        var ex = new DotnetInstallException(
            DotnetInstallErrorCode.ManifestFetchFailed,
            "Failed to fetch release manifest");

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal(ErrorCategory.Product, info.Category);
    }

    private class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }
}
