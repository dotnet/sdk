// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Microsoft.DotNet.Tools.Dotnetup.Tests;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

[TestClass]
public class ClassifyInstallPathTests
{
    [TestMethod]
    public void ClassifyInstallPath_DefaultInstallPath_ReturnsLocalAppData()
    {
        // The default install path is LocalApplicationData\dotnet
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            return; // Skip on platforms where LocalApplicationData is not set
        }

        var path = Path.Combine(localAppData, "dotnet");

        var result = InstallPathClassifier.ClassifyInstallPath(path);

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual("local_appdata", result);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ClassifyInstallPath_UserProfilePath_ReturnsUserProfile()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            return;
        }

        // A path under user profile but NOT under LocalApplicationData
        var path = Path.Combine(userProfile, "my-dotnet");

        var result = InstallPathClassifier.ClassifyInstallPath(path);

        Assert.AreEqual("user_profile", result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ClassifyInstallPath_LocalAppData_IsMoreSpecificThanUserProfile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrEmpty(localAppData) || string.IsNullOrEmpty(userProfile))
        {
            return;
        }

        // LocalAppData is under UserProfile — verify the more specific match wins
        Assert.StartsWith(userProfile, localAppData, StringComparison.OrdinalIgnoreCase);

        var path = Path.Combine(localAppData, "dotnet");
        var result = InstallPathClassifier.ClassifyInstallPath(path);

        // Should be local_appdata, not user_profile
        Assert.AreEqual("local_appdata", result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ClassifyInstallPath_ProgramFilesDotnet_ReturnsSystem()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles))
        {
            return;
        }

        var path = Path.Combine(programFiles, "dotnet");

        var result = InstallPathClassifier.ClassifyInstallPath(path);

        Assert.AreEqual("system", result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ClassifyInstallPath_ProgramFilesNonDotnet_ReturnsSystemProgramfiles()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles))
        {
            return;
        }

        // A path under Program Files that is NOT dotnet — still system_programfiles
        var path = Path.Combine(programFiles, "SomeOtherTool");

        var result = InstallPathClassifier.ClassifyInstallPath(path);

        Assert.AreEqual("system_programfiles", result);
    }

    [TestMethod]
    public void ClassifyInstallPath_UnknownPath_ReturnsOther()
    {
        // A path that doesn't match any known category
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        var result = InstallPathClassifier.ClassifyInstallPath(path);

        Assert.AreEqual("other", result);
    }

    [TestMethod]
    public void ClassifyInstallPath_GlobalJsonSource_UnknownPath_ReturnsGlobalJson()
    {
        // When pathSource is global_json and the path doesn't match a known location,
        // the result should be "global_json" instead of "other"
        var path = OperatingSystem.IsWindows()
            ? @"D:\repo\.dotnet"
            : "/tmp/repo/.dotnet";

        var result = InstallPathClassifier.ClassifyInstallPath(path, pathSource: PathSource.GlobalJson);

        Assert.AreEqual("global_json", result);
    }

    [TestMethod]
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

        var result = InstallPathClassifier.ClassifyInstallPath(path, pathSource: PathSource.GlobalJson);

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual("local_appdata", result);
        }
        else
        {
            // On non-Windows, LocalApplicationData may match user_home
            Assert.AreNotEqual("other", result);
        }
    }

    [TestMethod]
    public void ClassifyInstallPath_ExplicitSource_UnknownPath_ReturnsOther()
    {
        // Non-global_json source should still return "other" for unknown paths
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        var result = InstallPathClassifier.ClassifyInstallPath(path, pathSource: PathSource.Explicit);

        Assert.AreEqual("other", result);
    }

    [TestMethod]
    public void ClassifyInstallPath_NullPathSource_UnknownPath_ReturnsOther()
    {
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        var result = InstallPathClassifier.ClassifyInstallPath(path, pathSource: null);

        Assert.AreEqual("other", result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ClassifyInstallPath_UsrShareDotnet_ReturnsSystem()
    {
        var result = InstallPathClassifier.ClassifyInstallPath("/usr/share/dotnet");

        Assert.AreEqual("system", result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ClassifyInstallPath_UsrLocalBin_ReturnsSystemPath()
    {
        // /usr/local/bin is a system path but NOT an admin dotnet location
        var result = InstallPathClassifier.ClassifyInstallPath("/usr/local/bin/something");

        Assert.AreEqual("system_path", result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ClassifyInstallPath_OptPath_ReturnsSystemPath()
    {
        var result = InstallPathClassifier.ClassifyInstallPath("/opt/dotnet");

        Assert.AreEqual("system_path", result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void ClassifyInstallPath_HomePath_ReturnsUserHome()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            return;
        }

        var path = Path.Combine(home, ".dotnet");

        var result = InstallPathClassifier.ClassifyInstallPath(path);

        Assert.AreEqual("user_home", result);
    }
}

[TestClass]
public class ApplyErrorTagsTests
{
    [TestMethod]
    public void ApplyErrorTags_SetsAllRequiredTags()
    {
        using var listener = CreateTestListener(out var captured, "ApplyErrorTags.Test.1");

        using var source = new ActivitySource("ApplyErrorTags.Test.1");
        using (var activity = source.StartActivity("test"))
        {
            Assert.IsNotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "DiskFull",
                HResult: unchecked((int)0x80070070),
                StatusCode: null,
                Details: "ERROR_DISK_FULL",
                StackTrace: "at SomeMethod() in InstallExecutor.cs:line 42",
                Category: ErrorCategory.User);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);
        }

        var a = Assert.ContainsSingle(captured);
        Assert.AreEqual("DiskFull", a.GetTagItem("error.type"));
        Assert.AreEqual("user", a.GetTagItem("error.category"));
        Assert.AreEqual(unchecked((int)0x80070070), a.GetTagItem("error.hresult"));
        Assert.AreEqual("ERROR_DISK_FULL", a.GetTagItem("error.details"));
        Assert.AreEqual("at SomeMethod() in InstallExecutor.cs:line 42", a.GetTagItem("error.stack_trace"));
    }

    [TestMethod]
    public void ApplyErrorTags_WithErrorCode_SetsErrorCodeTag()
    {
        using var listener = CreateTestListener(out var captured, "ApplyErrorTags.Test.2");

        using var source = new ActivitySource("ApplyErrorTags.Test.2");
        using (var activity = source.StartActivity("test"))
        {
            Assert.IsNotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "HttpError",
                HResult: null,
                StatusCode: 404,
                Details: null,
                StackTrace: null,
                Category: ErrorCategory.Product);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo, errorCode: "Http404");
        }

        var a = Assert.ContainsSingle(captured);
        Assert.AreEqual("HttpError", a.GetTagItem("error.type"));
        Assert.AreEqual("Http404", a.GetTagItem("error.code"));
        Assert.AreEqual("product", a.GetTagItem("error.category"));
        Assert.AreEqual(404, a.GetTagItem("error.http_status"));
        Assert.IsNull(a.GetTagItem("error.hresult"));
        Assert.IsNull(a.GetTagItem("error.details"));
    }

    [TestMethod]
    public void ApplyErrorTags_WithNullActivity_DoesNotThrow()
    {
        var errorInfo = new ExceptionErrorInfo(
            ErrorType: "Test",
            HResult: null,
            StatusCode: null,
            Details: null,
            StackTrace: null,
            Category: ErrorCategory.Product);

        var ex = Record.Exception(() => ErrorCodeMapper.ApplyErrorTags(null, errorInfo));

        Assert.IsNull(ex);
    }

    [TestMethod]
    public void ApplyErrorTags_NullOptionalFields_DoesNotSetOptionalTags()
    {
        using var listener = CreateTestListener(out var captured, "ApplyErrorTags.Test.3");

        using var source = new ActivitySource("ApplyErrorTags.Test.3");
        using (var activity = source.StartActivity("test"))
        {
            Assert.IsNotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "GenericError",
                HResult: null,
                StatusCode: null,
                Details: null,
                StackTrace: null,
                Category: ErrorCategory.Product);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);
        }

        var a = Assert.ContainsSingle(captured);
        Assert.AreEqual("GenericError", a.GetTagItem("error.type"));
        Assert.AreEqual("product", a.GetTagItem("error.category"));
        // Optional tags should not be set
        Assert.IsNull(a.GetTagItem("error.hresult"));
        Assert.IsNull(a.GetTagItem("error.http_status"));
        Assert.IsNull(a.GetTagItem("error.details"));
        Assert.IsNull(a.GetTagItem("error.stack_trace"));
        Assert.IsNull(a.GetTagItem("error.code"));
    }

    [TestMethod]
    public void ApplyErrorTags_SetsActivityStatusToError()
    {
        using var listener = CreateTestListener(out var captured, "ApplyErrorTags.Test.4");

        using var source = new ActivitySource("ApplyErrorTags.Test.4");
        using (var activity = source.StartActivity("test"))
        {
            Assert.IsNotNull(activity);

            var errorInfo = new ExceptionErrorInfo(
                ErrorType: "TestError",
                HResult: null,
                StatusCode: null,
                Details: null,
                StackTrace: null,
                Category: ErrorCategory.Product);

            ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);
        }

        var a = Assert.ContainsSingle(captured);
        Assert.AreEqual(ActivityStatusCode.Error, a.Status);
        Assert.AreEqual("TestError", a.StatusDescription);
    }

    private static ActivityListener CreateTestListener(out List<Activity> captured, string? sourceName = null)
    {
        var list = new List<Activity>();
        captured = list;
        var listener = new ActivityListener
        {
            ShouldListenTo = source => sourceName is null || source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => list.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}

[TestClass]
public class IsAdminInstallPathTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void IsAdminInstallPath_ProgramFilesDotnet_ReturnsTrue()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles)) return;

        Assert.IsTrue(InstallPathClassifier.IsAdminInstallPath(Path.Combine(programFiles, "dotnet")));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void IsAdminInstallPath_ProgramFilesX86Dotnet_ReturnsTrue()
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrEmpty(programFilesX86)) return;

        Assert.IsTrue(InstallPathClassifier.IsAdminInstallPath(Path.Combine(programFilesX86, "dotnet")));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void IsAdminInstallPath_ProgramFilesSubfolder_ReturnsTrue()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles)) return;

        // Subfolders of Program Files\dotnet should also be blocked
        Assert.IsTrue(InstallPathClassifier.IsAdminInstallPath(Path.Combine(programFiles, "dotnet", "sdk")));
    }

    [TestMethod]
    public void IsAdminInstallPath_UserPath_ReturnsFalse()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData)) return;

        Assert.IsFalse(InstallPathClassifier.IsAdminInstallPath(Path.Combine(localAppData, "dotnet")));
    }

    [TestMethod]
    public void IsAdminInstallPath_CustomPath_ReturnsFalse()
    {
        var path = OperatingSystem.IsWindows()
            ? @"D:\custom\dotnet"
            : "/tmp/custom/dotnet";

        Assert.IsFalse(InstallPathClassifier.IsAdminInstallPath(path));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminInstallPath_UsrShareDotnet_ReturnsTrue()
    {
        Assert.IsTrue(InstallPathClassifier.IsAdminInstallPath("/usr/share/dotnet"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminInstallPath_UsrLibDotnet_ReturnsTrue()
    {
        Assert.IsTrue(InstallPathClassifier.IsAdminInstallPath("/usr/lib/dotnet"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminInstallPath_UsrLocalShareDotnet_ReturnsTrue()
    {
        Assert.IsTrue(InstallPathClassifier.IsAdminInstallPath("/usr/local/share/dotnet"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminInstallPath_OptPath_ReturnsFalse()
    {
        // /opt/dotnet is a system path but not an admin dotnet location
        Assert.IsFalse(InstallPathClassifier.IsAdminInstallPath("/opt/dotnet"));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminInstallPath_HomePath_ReturnsFalse()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return;

        Assert.IsFalse(InstallPathClassifier.IsAdminInstallPath(Path.Combine(home, ".dotnet")));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void IsAdminInstallPath_ProgramFilesNonDotnet_ReturnsFalse()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles)) return;

        // Program Files\SomeOtherTool is NOT an admin dotnet path
        Assert.IsFalse(InstallPathClassifier.IsAdminInstallPath(Path.Combine(programFiles, "SomeOtherTool")));
    }
}

[TestClass]
public class GetVersionTests
{
    [TestMethod]
    public void GetVersion_ReturnsNonEmptyVersion()
    {
        var version = TelemetryCommonProperties.GetVersion();

        Assert.IsFalse(string.IsNullOrEmpty(version));
    }

    [TestMethod]
    public void GetVersion_DevBuild_ContainsAtSymbol()
    {
        // In test builds (compiled as Debug), GetVersion should include @commitsha
        // since DetectDevBuild returns true for DEBUG builds
        var version = TelemetryCommonProperties.GetVersion();

        // The version should contain @ if the commit SHA is known
        if (BuildInfo.CommitSha != "unknown")
        {
            Assert.Contains("@", version);
            // The part after @ should be the commit SHA
            var parts = version.Split('@');
            Assert.HasCount(2, parts);
            Assert.AreEqual(BuildInfo.CommitSha, parts[1]);
        }
    }

    [TestMethod]
    public void GetVersion_VersionPartMatchesBuildInfoVersion()
    {
        var version = TelemetryCommonProperties.GetVersion();

        // The version part (before @) should match BuildInfo.Version
        var versionPart = version.Split('@')[0];
        Assert.AreEqual(BuildInfo.Version, versionPart);
    }
}
