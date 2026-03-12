// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
        // Details contain the human-readable error constant name
        Assert.Equal("ERROR_DISK_FULL", info.Details);
    }

    [Fact]
    public void GetErrorInfo_IOException_SharingViolation_MapsCorrectly()
    {
        // HResult for ERROR_SHARING_VIOLATION
        var ex = new IOException("File in use", unchecked((int)0x80070020));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("SharingViolation", info.ErrorType);
        // Details contain the human-readable error constant name
        Assert.Equal("ERROR_SHARING_VIOLATION", info.Details);
    }

    [Fact]
    public void GetErrorInfo_IOException_PathTooLong_MapsCorrectly()
    {
        var ex = new IOException("Path too long", unchecked((int)0x800700CE));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("PathTooLong", info.ErrorType);
        // Details contain the human-readable error constant name
        Assert.Equal("ERROR_FILENAME_EXCED_RANGE", info.Details);
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
    public void GetErrorInfo_WrappedException_IncludesInnerExceptionInStackTrace()
    {
        var innerInner = new SocketException(10054); // Connection reset
        var inner = new IOException("Network error", innerInner);
        var outer = new HttpRequestException("Request failed", inner);

        var info = ErrorCodeMapper.GetErrorInfo(outer);

        Assert.Equal("HttpRequestException", info.ErrorType);
        // Inner exception types should be included in the stack trace
        Assert.NotNull(info.StackTrace);
        Assert.Contains("System.IO.IOException", info.StackTrace);
        Assert.Contains("System.Net.Sockets.SocketException", info.StackTrace);
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
    public void GetErrorInfo_ThrownException_HasStackTrace()
    {
        // Throw from a method to get a real stack trace
        var ex = ThrowTestException();

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("InvalidOperation", info.ErrorType);
        Assert.NotNull(info.StackTrace);
        Assert.Contains("ThrowTestException", info.StackTrace);
    }

    [Fact]
    public void GetErrorInfo_AllFieldsPopulated_ForIOExceptionWithInnerException()
    {
        // Create a realistic exception scenario - IOException with inner exception
        var inner = new UnauthorizedAccessException("Access denied");
        var outer = new IOException("Cannot write file", inner);

        var info = ErrorCodeMapper.GetErrorInfo(outer);

        // Verify inner exception type is included in stack trace
        Assert.Equal("IOException", info.ErrorType);
        Assert.NotNull(info.StackTrace);
        Assert.Contains("System.UnauthorizedAccessException", info.StackTrace);
    }

    [Fact]
    public void GetErrorInfo_HResultAndDetails_ForDiskFullException()
    {
        // Create IOException with specific HResult for disk full
        var ex = new IOException("Disk full", unchecked((int)0x80070070));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("DiskFull", info.ErrorType);
        Assert.Equal(unchecked((int)0x80070070), info.HResult);
        // Details contain the human-readable error constant name
        Assert.Equal("ERROR_DISK_FULL", info.Details);
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
    public void GetErrorInfo_LongExceptionChain_IncludesInnerExceptionsInStackTrace()
    {
        // Create a chain of typed exceptions
        Exception ex = new InvalidOperationException("Root");
        for (int i = 0; i < 10; i++)
        {
            ex = new IOException($"Wrapper {i}", ex);
        }

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        // Stack trace should include inner exception types
        Assert.NotNull(info.StackTrace);
        Assert.Contains("System.IO.IOException", info.StackTrace);
        Assert.Contains("System.InvalidOperationException", info.StackTrace);
    }

    [Fact]
    public void GetErrorInfo_NetworkPathNotFound_MapsCorrectly()
    {
        var ex = new IOException("Network path not found", unchecked((int)0x80070035));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("NetworkPathNotFound", info.ErrorType);
        // Details contain the human-readable error constant name
        Assert.Equal("ERROR_BAD_NETPATH", info.Details);
    }

    [Fact]
    public void GetErrorInfo_AlreadyExists_MapsCorrectly()
    {
        // HResult for ERROR_ALREADY_EXISTS (0x800700B7 = -2147024713)
        var ex = new IOException("Cannot create a file when that file already exists", unchecked((int)0x800700B7));

        var info = ErrorCodeMapper.GetErrorInfo(ex);

        Assert.Equal("AlreadyExists", info.ErrorType);
        Assert.Equal(unchecked((int)0x800700B7), info.HResult);
        // Details contain the human-readable error constant name
        Assert.Equal("ERROR_ALREADY_EXISTS", info.Details);
    }

    private class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }
}

/// <summary>
/// Tests that error info is correctly propagated from command-level spans
/// to root-level spans, ensuring the telemetry workbook can see error.type
/// regardless of which span it queries.
/// </summary>
public class ErrorPropagationTests : IDisposable
{
    private readonly ActivitySource _testSource = new("Test.ErrorPropagation");
    private readonly ActivityListener _listener;
    private readonly List<Activity> _capturedActivities = new();

    public ErrorPropagationTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Test.ErrorPropagation",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _testSource.Dispose();
    }

    [Fact]
    public void ErrorInfo_CanBePropagated_FromCommandSpanToRootSpan()
    {
        // Simulate the two-span architecture: root "dotnetup" + child "command/sdk/install"
        using var rootActivity = _testSource.StartActivity("dotnetup", ActivityKind.Internal)!;
        using var commandActivity = _testSource.StartActivity("command/sdk/install", ActivityKind.Internal)!;

        // Exception occurs during command execution
        var ex = new IOException("Disk full", unchecked((int)0x80070070));
        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);

        // Step 1: Error tags applied to command span (as CommandBase does)
        ErrorCodeMapper.ApplyErrorTags(commandActivity, errorInfo);

        // Step 2: Same error info propagated to root span (as Program.Main now does)
        ErrorCodeMapper.ApplyErrorTags(rootActivity, errorInfo);

        // Both spans should now carry identical error tags
        Assert.Equal("DiskFull", commandActivity.GetTagItem("error.type"));
        Assert.Equal("DiskFull", rootActivity.GetTagItem("error.type"));

        Assert.Equal("user", commandActivity.GetTagItem("error.category"));
        Assert.Equal("user", rootActivity.GetTagItem("error.category"));

        Assert.Equal(unchecked((int)0x80070070), commandActivity.GetTagItem("error.hresult"));
        Assert.Equal(unchecked((int)0x80070070), rootActivity.GetTagItem("error.hresult"));

        Assert.Equal("ERROR_DISK_FULL", commandActivity.GetTagItem("error.details"));
        Assert.Equal("ERROR_DISK_FULL", rootActivity.GetTagItem("error.details"));
    }

    [Fact]
    public void ErrorInfo_PropagatedToRootSpan_IncludesStackTrace()
    {
        using var rootActivity = _testSource.StartActivity("dotnetup")!;

        // Create an exception with a real stack trace
        Exception thrownEx;
        try
        {
            throw new InvalidOperationException("assertion failed in mutex");
        }
        catch (Exception ex)
        {
            thrownEx = ex;
        }

        var errorInfo = ErrorCodeMapper.GetErrorInfo(thrownEx);
        ErrorCodeMapper.ApplyErrorTags(rootActivity, errorInfo);

        Assert.Equal("InvalidOperation", rootActivity.GetTagItem("error.type"));
        Assert.Equal("product", rootActivity.GetTagItem("error.category"));

        var stackTrace = rootActivity.GetTagItem("error.stack_trace") as string;
        Assert.NotNull(stackTrace);
        Assert.Contains("ErrorInfo_PropagatedToRootSpan_IncludesStackTrace", stackTrace);
    }

    [Fact]
    public void ErrorInfo_HttpError_PropagatedToRootSpan_IncludesStatusCode()
    {
        using var rootActivity = _testSource.StartActivity("dotnetup")!;
        using var commandActivity = _testSource.StartActivity("command/sdk/install")!;

        var ex = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);
        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);

        ErrorCodeMapper.ApplyErrorTags(commandActivity, errorInfo);
        ErrorCodeMapper.ApplyErrorTags(rootActivity, errorInfo);

        // Both spans carry the HTTP status
        Assert.Equal(404, commandActivity.GetTagItem("error.http_status"));
        Assert.Equal(404, rootActivity.GetTagItem("error.http_status"));

        Assert.Equal("Http404", commandActivity.GetTagItem("error.type"));
        Assert.Equal("Http404", rootActivity.GetTagItem("error.type"));
    }

    [Fact]
    public void ErrorInfo_UserError_PropagatedToRootSpan_PreservesCategory()
    {
        using var rootActivity = _testSource.StartActivity("dotnetup")!;

        var errorInfo = new ExceptionErrorInfo("InvalidVersion", ErrorCategory.User, Details: "user typed garbage");

        ErrorCodeMapper.ApplyErrorTags(rootActivity, errorInfo);

        Assert.Equal("user", rootActivity.GetTagItem("error.category"));
        Assert.Equal("InvalidVersion", rootActivity.GetTagItem("error.type"));
    }

    [Fact]
    public void RootSpan_WithoutPropagation_HasNoErrorTags()
    {
        // Verifies the problem scenario: if error is only on command span,
        // root span has no error tags.
        using var rootActivity = _testSource.StartActivity("dotnetup")!;
        using var commandActivity = _testSource.StartActivity("command/sdk/install")!;

        var ex = new IOException("Disk full", unchecked((int)0x80070070));
        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);

        // Only apply to command span
        ErrorCodeMapper.ApplyErrorTags(commandActivity, errorInfo);

        // Root span should NOT have error tags
        Assert.Null(rootActivity.GetTagItem("error.type"));
        Assert.Null(rootActivity.GetTagItem("error.category"));

        // Command span should have them
        Assert.Equal("DiskFull", commandActivity.GetTagItem("error.type"));
    }

    [Fact]
    public void GetErrorInfo_IsReusable_AcrossMultipleActivities()
    {
        // Verifies that the same ExceptionErrorInfo can be applied to multiple spans
        using var activity1 = _testSource.StartActivity("span1")!;
        using var activity2 = _testSource.StartActivity("span2")!;
        using var activity3 = _testSource.StartActivity("span3")!;

        var errorInfo = new ExceptionErrorInfo("TestError", ErrorCategory.Product, StatusCode: 500, Details: "server error");

        ErrorCodeMapper.ApplyErrorTags(activity1, errorInfo);
        ErrorCodeMapper.ApplyErrorTags(activity2, errorInfo);
        ErrorCodeMapper.ApplyErrorTags(activity3, errorInfo);

        // All three should have identical tags
        foreach (var activity in new[] { activity1, activity2, activity3 })
        {
            Assert.Equal("TestError", activity.GetTagItem("error.type"));
            Assert.Equal("product", activity.GetTagItem("error.category"));
            Assert.Equal(500, activity.GetTagItem("error.http_status"));
        }
    }
}
