// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Xunit;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

public class ClassifyInstallPathTests
{
    [Fact]
    public void ClassifyInstallPath_DefaultInstallPath_ReturnsLocalAppData()
    {
        // The default install path is LocalApplicationData\dotnet
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            return; // Skip on platforms where LocalApplicationData is not set
        }

        var path = Path.Combine(localAppData, "dotnet");

        var result = InstallExecutor.ClassifyInstallPath(path);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("local_appdata", result);
        }
    }

    [Fact]
    public void ClassifyInstallPath_UserProfilePath_ReturnsUserProfile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            return;
        }

        // A path under user profile but NOT under LocalApplicationData
        var path = Path.Combine(userProfile, "my-dotnet");

        var result = InstallExecutor.ClassifyInstallPath(path);

        Assert.Equal("user_profile", result);
    }

    [Fact]
    public void ClassifyInstallPath_LocalAppData_IsMoreSpecificThanUserProfile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrEmpty(localAppData) || string.IsNullOrEmpty(userProfile))
        {
            return;
        }

        // LocalAppData is under UserProfile — verify the more specific match wins
        Assert.StartsWith(userProfile, localAppData, StringComparison.OrdinalIgnoreCase);

        var path = Path.Combine(localAppData, "dotnet");
        var result = InstallExecutor.ClassifyInstallPath(path);

        // Should be local_appdata, not user_profile
        Assert.Equal("local_appdata", result);
    }

    [Fact]
    public void ClassifyInstallPath_ProgramFiles_ReturnsSystemProgramfiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles))
        {
            return;
        }

        var path = Path.Combine(programFiles, "dotnet");

        var result = InstallExecutor.ClassifyInstallPath(path);

        Assert.Equal("system_programfiles", result);
    }

    [Fact]
    public void ClassifyInstallPath_UnknownPath_ReturnsOther()
    {
        // A path that doesn't match any known category
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        var result = InstallExecutor.ClassifyInstallPath(path);

        Assert.Equal("other", result);
    }

    [Fact]
    public void ClassifyInstallPath_GlobalJsonSource_UnknownPath_ReturnsGlobalJson()
    {
        // When pathSource is global_json and the path doesn't match a known location,
        // the result should be "global_json" instead of "other"
        var path = OperatingSystem.IsWindows()
            ? @"D:\repo\.dotnet"
            : "/tmp/repo/.dotnet";

        var result = InstallExecutor.ClassifyInstallPath(path, pathSource: "global_json");

        Assert.Equal("global_json", result);
    }

    [Fact]
    public void ClassifyInstallPath_GlobalJsonSource_KnownPath_ReturnsKnownType()
    {
        // When pathSource is global_json but the path is a well-known location,
        // the well-known classification should still win
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            return;
        }

        var path = Path.Combine(localAppData, "dotnet");

        var result = InstallExecutor.ClassifyInstallPath(path, pathSource: "global_json");

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("local_appdata", result);
        }
        else
        {
            // On non-Windows, LocalApplicationData may match user_home
            Assert.NotEqual("other", result);
        }
    }

    [Fact]
    public void ClassifyInstallPath_ExplicitSource_UnknownPath_ReturnsOther()
    {
        // Non-global_json source should still return "other" for unknown paths
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        var result = InstallExecutor.ClassifyInstallPath(path, pathSource: "explicit");

        Assert.Equal("other", result);
    }

    [Fact]
    public void ClassifyInstallPath_NullPathSource_UnknownPath_ReturnsOther()
    {
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        var result = InstallExecutor.ClassifyInstallPath(path, pathSource: null);

        Assert.Equal("other", result);
    }

    [Fact]
    public void ClassifyInstallPath_UsrPath_ReturnsSystemPath()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = InstallExecutor.ClassifyInstallPath("/usr/share/dotnet");

        Assert.Equal("system_path", result);
    }

    [Fact]
    public void ClassifyInstallPath_OptPath_ReturnsSystemPath()
    {
        if (OperatingSystem.IsWindows()) return;

        var result = InstallExecutor.ClassifyInstallPath("/opt/dotnet");

        Assert.Equal("system_path", result);
    }

    [Fact]
    public void ClassifyInstallPath_HomePath_ReturnsUserHome()
    {
        if (OperatingSystem.IsWindows()) return;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            return;
        }

        var path = Path.Combine(home, ".dotnet");

        var result = InstallExecutor.ClassifyInstallPath(path);

        Assert.Equal("user_home", result);
    }
}

public class ApplyErrorTagsTests
{
    [Fact]
    public void ApplyErrorTags_SetsAllRequiredTags()
    {
        using var listener = CreateTestListener(out var captured);

        using var source = new ActivitySource("ApplyErrorTags.Test.1");
        using (var activity = source.StartActivity("test"))
        {
            Assert.NotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "DiskFull",
                HResult: unchecked((int)0x80070070),
                StatusCode: null,
                Details: "win32_error_112",
                SourceLocation: "InstallExecutor.cs:42",
                ExceptionChain: "IOException",
                Category: ErrorCategory.User);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);
        }

        var a = Assert.Single(captured);
        Assert.Equal("DiskFull", a.GetTagItem("error.type"));
        Assert.Equal("user", a.GetTagItem("error.category"));
        Assert.Equal(unchecked((int)0x80070070), a.GetTagItem("error.hresult"));
        Assert.Equal("win32_error_112", a.GetTagItem("error.details"));
        Assert.Equal("InstallExecutor.cs:42", a.GetTagItem("error.source_location"));
        Assert.Equal("IOException", a.GetTagItem("error.exception_chain"));
    }

    [Fact]
    public void ApplyErrorTags_WithErrorCode_SetsErrorCodeTag()
    {
        using var listener = CreateTestListener(out var captured);

        using var source = new ActivitySource("ApplyErrorTags.Test.2");
        using (var activity = source.StartActivity("test"))
        {
            Assert.NotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "HttpError",
                HResult: null,
                StatusCode: 404,
                Details: null,
                SourceLocation: null,
                ExceptionChain: "HttpRequestException",
                Category: ErrorCategory.Product);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo, errorCode: "Http404");
        }

        var a = Assert.Single(captured);
        Assert.Equal("HttpError", a.GetTagItem("error.type"));
        Assert.Equal("Http404", a.GetTagItem("error.code"));
        Assert.Equal("product", a.GetTagItem("error.category"));
        Assert.Equal(404, a.GetTagItem("error.http_status"));
        Assert.Null(a.GetTagItem("error.hresult"));
        Assert.Null(a.GetTagItem("error.details"));
    }

    [Fact]
    public void ApplyErrorTags_WithNullActivity_DoesNotThrow()
    {
        var errorInfo = new ExceptionErrorInfo(
            ErrorType: "Test",
            HResult: null,
            StatusCode: null,
            Details: null,
            SourceLocation: null,
            ExceptionChain: null,
            Category: ErrorCategory.Product);

        var ex = Record.Exception(() => ErrorCodeMapper.ApplyErrorTags(null, errorInfo));

        Assert.Null(ex);
    }

    [Fact]
    public void ApplyErrorTags_NullOptionalFields_DoesNotSetOptionalTags()
    {
        using var listener = CreateTestListener(out var captured);

        using var source = new ActivitySource("ApplyErrorTags.Test.3");
        using (var activity = source.StartActivity("test"))
        {
            Assert.NotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "GenericError",
                HResult: null,
                StatusCode: null,
                Details: null,
                SourceLocation: null,
                ExceptionChain: null,
                Category: ErrorCategory.Product);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);
        }

        var a = Assert.Single(captured);
        Assert.Equal("GenericError", a.GetTagItem("error.type"));
        Assert.Equal("product", a.GetTagItem("error.category"));
        // Optional tags should not be set
        Assert.Null(a.GetTagItem("error.hresult"));
        Assert.Null(a.GetTagItem("error.http_status"));
        Assert.Null(a.GetTagItem("error.details"));
        Assert.Null(a.GetTagItem("error.source_location"));
        Assert.Null(a.GetTagItem("error.exception_chain"));
        Assert.Null(a.GetTagItem("error.code"));
    }

    [Fact]
    public void ApplyErrorTags_SetsActivityStatusToError()
    {
        using var listener = CreateTestListener(out var captured);

        using var source = new ActivitySource("ApplyErrorTags.Test.4");
        using (var activity = source.StartActivity("test"))
        {
            Assert.NotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "TestError",
                HResult: null,
                StatusCode: null,
                Details: null,
                SourceLocation: null,
                ExceptionChain: null,
                Category: ErrorCategory.Product);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);
        }

        var a = Assert.Single(captured);
        Assert.Equal(ActivityStatusCode.Error, a.Status);
        Assert.Equal("TestError", a.StatusDescription);
    }

    private static ActivityListener CreateTestListener(out List<Activity> captured)
    {
        var list = new List<Activity>();
        captured = list;
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => list.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
