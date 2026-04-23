// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Xunit;
using UrlSanitizer = Microsoft.Dotnet.Installation.Internal.UrlSanitizer;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

public class TelemetryCommonPropertiesTests
{
    [Fact]
    public void Hash_SameInput_ProducesSameOutput()
    {
        var input = "test-string";
        var hash1 = TelemetryCommonProperties.Hash(input);
        var hash2 = TelemetryCommonProperties.Hash(input);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentOutputs()
    {
        var hash1 = TelemetryCommonProperties.Hash("input1");
        var hash2 = TelemetryCommonProperties.Hash("input2");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_ReturnsValidSha256_64CharHex()
    {
        var hash = TelemetryCommonProperties.Hash("test");

        // SHA256 produces 32 bytes = 64 hex characters
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[a-f0-9]+$", hash);
    }

    [Fact]
    public void Hash_IsLowercase()
    {
        var hash = TelemetryCommonProperties.Hash("TEST");

        Assert.Equal(hash.ToLowerInvariant(), hash);
    }

    [Fact]
    public void HashPath_NullPath_ReturnsEmpty()
    {
        var result = TelemetryCommonProperties.HashPath(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void HashPath_EmptyPath_ReturnsEmpty()
    {
        var result = TelemetryCommonProperties.HashPath(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void HashPath_ValidPath_ReturnsHash()
    {
        var result = TelemetryCommonProperties.HashPath(@"C:\Users\test\path");

        Assert.NotEmpty(result);
        Assert.Equal(64, result.Length);
    }

    [Fact]
    public void GetCommonAttributes_ContainsRequiredKeys()
    {
        var sessionId = Guid.NewGuid().ToString();
        var attributes = TelemetryCommonProperties.GetCommonAttributes(sessionId)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Contains("session.id", attributes.Keys);
        Assert.Contains("device.id", attributes.Keys);
        Assert.Contains("os.platform", attributes.Keys);
        Assert.Contains("os.version", attributes.Keys);
        Assert.Contains("process.arch", attributes.Keys);
        Assert.Contains("ci.detected", attributes.Keys);
        Assert.Contains("dotnetup.version", attributes.Keys);
        Assert.Contains("dev.build", attributes.Keys);
    }

    [Fact]
    public void GetCommonAttributes_SessionIdMatchesInput()
    {
        var sessionId = Guid.NewGuid().ToString();
        var attributes = TelemetryCommonProperties.GetCommonAttributes(sessionId)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(sessionId, attributes["session.id"]);
    }

    [Fact]
    public void GetCommonAttributes_OsPlatformIsValid()
    {
        var attributes = TelemetryCommonProperties.GetCommonAttributes("test-session")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var osPlatform = attributes["os.platform"] as string;
        // OSDescription returns the full OS description (e.g., "Microsoft Windows 10.0.26200")
        Assert.False(string.IsNullOrEmpty(osPlatform));
    }

    [Fact]
    public void GetCommonAttributes_ProcessArchIsValid()
    {
        var attributes = TelemetryCommonProperties.GetCommonAttributes("test-session")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var arch = attributes["process.arch"] as string;
        Assert.Contains(arch, new[] { "X86", "X64", "Arm", "Arm64", "Wasm", "S390x", "LoongArch64", "Armv6", "Ppc64le", "RiscV64" });
    }

    [Fact]
    public void GetCommonAttributes_DeviceIdIsNotEmpty()
    {
        var attributes = TelemetryCommonProperties.GetCommonAttributes("test-session")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var deviceId = attributes["device.id"] as string;
        Assert.False(string.IsNullOrEmpty(deviceId));
    }

    [Fact]
    public void GetCommonAttributes_VersionIsNotEmpty()
    {
        var attributes = TelemetryCommonProperties.GetCommonAttributes("test-session")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var version = attributes["dotnetup.version"] as string;
        Assert.False(string.IsNullOrEmpty(version));
    }
}

public class VersionSanitizerTelemetryTests
{
    [Theory]
    [InlineData("9.0", "9.0")]
    [InlineData("9.0.100", "9.0.100")]
    [InlineData("10.0.1xx", "10.0.1xx")]
    [InlineData("latest", "latest")]
    [InlineData("preview", "preview")]
    [InlineData("lts", "lts")]
    public void Sanitize_ValidVersions_PassThrough(string input, string expected)
    {
        var result = VersionSanitizer.Sanitize(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("my-custom-path")]
    [InlineData("/home/user/sdk")]
    [InlineData("C:\\Users\\secret\\path")]
    [InlineData("some random text")]
    public void Sanitize_InvalidInput_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);

        Assert.Equal("invalid", result);
    }

    [Theory]
    [InlineData("9.0.100-preview.1")]
    [InlineData("9.0.100-rc.2")]
    [InlineData("10.0.100-preview.3.25678.9")]
    public void Sanitize_PreReleaseVersions_PassThrough(string input)
    {
        var result = VersionSanitizer.Sanitize(input);

        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_NullOrEmpty_ReturnsUnspecified(string? input)
    {
        var result = VersionSanitizer.Sanitize(input);

        Assert.Equal("unspecified", result);
    }

    [Theory]
    [InlineData("10.0.10xx")]  // Two digits before xx not allowed
    [InlineData("10.0.100x")] // Three digits before x not allowed
    [InlineData("10.0.xxxx")] // Too many x's
    public void Sanitize_InvalidWildcards_ReturnsInvalid(string input)
    {
        var result = VersionSanitizer.Sanitize(input);

        Assert.Equal("invalid", result);
    }

    [Theory]
    [InlineData("10.0.1xx")]  // Feature band wildcard
    [InlineData("10.0.20x")] // Single digit wildcard
    public void Sanitize_ValidWildcards_PassThrough(string input)
    {
        var result = VersionSanitizer.Sanitize(input);

        Assert.Equal(input, result);
    }
}

public class UrlSanitizerTests
{
    [Theory]
    [InlineData("https://download.visualstudio.microsoft.com/download/pr/123/file.zip", "download.visualstudio.microsoft.com")]
    [InlineData("https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.100/dotnet-sdk.zip", "builds.dotnet.microsoft.com")]
    [InlineData("https://ci.dot.net/job/123/artifact.zip", "ci.dot.net")]
    [InlineData("https://dotnetcli.blob.core.windows.net/dotnet/Sdk/9.0.100/dotnet-sdk.zip", "dotnetcli.blob.core.windows.net")]
    [InlineData("https://dotnetcli.azureedge.net/dotnet/Sdk/9.0.100/dotnet-sdk.zip", "dotnetcli.azureedge.net")]
    public void SanitizeDomain_KnownDomains_ReturnsDomain(string url, string expectedDomain)
    {
        var result = UrlSanitizer.SanitizeDomain(url);

        Assert.Equal(expectedDomain, result);
    }

    [Theory]
    [InlineData("https://my-private-mirror.company.com/dotnet/sdk.zip")]
    [InlineData("https://internal.corp.net/artifacts/dotnet-sdk.zip")]
    [InlineData("https://192.168.1.100/dotnet/sdk.zip")]
    [InlineData("file:///C:/Users/someone/Downloads/sdk.zip")]
    public void SanitizeDomain_UnknownDomains_ReturnsUnknown(string url)
    {
        var result = UrlSanitizer.SanitizeDomain(url);

        Assert.Equal("unknown", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://")]
    public void SanitizeDomain_InvalidUrls_ReturnsUnknown(string? url)
    {
        var result = UrlSanitizer.SanitizeDomain(url);

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void KnownDownloadDomains_ContainsExpectedDomains()
    {
        Assert.Contains("download.visualstudio.microsoft.com", UrlSanitizer.KnownDownloadDomains);
        Assert.Contains("builds.dotnet.microsoft.com", UrlSanitizer.KnownDownloadDomains);
        Assert.Contains("ci.dot.net", UrlSanitizer.KnownDownloadDomains);
    }
}

public class DotnetupTelemetryTests : IDisposable
{
    private readonly ActivityListener _listener;

    public DotnetupTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.Dotnet.Bootstrapper",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = DotnetupTelemetry.Instance;
        var instance2 = DotnetupTelemetry.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void SessionId_IsValidGuid()
    {
        var sessionId = DotnetupTelemetry.Instance.SessionId;

        Assert.True(Guid.TryParse(sessionId, out _));
    }

    [Fact]
    public void CommandSource_IsNotNull()
    {
        Assert.NotNull(DotnetupTelemetry.CommandSource);
    }

    [Fact]
    public void CommandSource_HasCorrectName()
    {
        Assert.Equal("Microsoft.Dotnet.Bootstrapper", DotnetupTelemetry.CommandSource.Name);
    }

    [Fact]
    public void Flush_DoesNotThrow()
    {
        // Even if telemetry is disabled, Flush should not throw
        var exception = Record.Exception(() => DotnetupTelemetry.Instance.Flush());

        Assert.Null(exception);
    }

    [Fact]
    public void Flush_WithTimeout_DoesNotThrow()
    {
        var exception = Record.Exception(() => DotnetupTelemetry.Instance.Flush(1000));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordException_WithNullActivity_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            DotnetupTelemetry.Instance.RecordException(null, new Exception("test")));

        Assert.Null(exception);
    }

    [Fact]
    public void ApplyLastErrorToActivity_WithNullActivity_DoesNotThrow()
    {
        // RecordException with null activity should not throw
        var exception = Record.Exception(() =>
            DotnetupTelemetry.Instance.RecordException(null, new Exception("test")));

        Assert.Null(exception);
    }

    /// <summary>
    /// Simulates what RecordException does internally: maps exception to error info,
    /// applies tags to the activity, then walks up to root and applies there too.
    /// Tests use this instead of RecordException because that method guards on Enabled
    /// (which is false in unit tests — telemetry export requires opt-in).
    /// </summary>
    private static void SimulateRecordException(Activity activity, Exception ex)
    {
        var errorInfo = ErrorCodeMapper.GetErrorInfo(ex);
        ErrorCodeMapper.ApplyErrorTags(activity, errorInfo);

        // Walk to root and apply there too (mirrors ApplyErrorToRootActivity)
        var root = activity;
        while (root.Parent != null)
        {
            root = root.Parent;
        }

        if (root != activity)
        {
            ErrorCodeMapper.ApplyErrorTags(root, errorInfo);
        }
    }

    [Fact]
    public void RecordException_PropagatesTagsToRootActivity()
    {
        // Root activity → child activity records exception → root should also get tags
        using var rootActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-root", ActivityKind.Internal);
        Assert.NotNull(rootActivity);

        using var commandActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-command", ActivityKind.Internal);
        Assert.NotNull(commandActivity);

        SimulateRecordException(commandActivity,
            new DotnetInstallException(DotnetInstallErrorCode.ContextResolutionFailed, "resolution failed"));

        // Both command and root should have error tags
        Assert.Equal("ContextResolutionFailed", commandActivity.GetTagItem("error.type"));
        Assert.Equal("user", commandActivity.GetTagItem("error.category"));
        Assert.Equal("ContextResolutionFailed", rootActivity.GetTagItem("error.type"));
        Assert.Equal("user", rootActivity.GetTagItem("error.category"));
    }

    [Fact]
    public void RecordException_OnRootActivity_DoesNotDoubleTag()
    {
        // When exception is recorded directly on the root (no parent), it should still work
        using var rootActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-root-only", ActivityKind.Internal);
        Assert.NotNull(rootActivity);

        SimulateRecordException(rootActivity,
            new DotnetInstallException(DotnetInstallErrorCode.InstallFailed, "install failed"));

        Assert.Equal("InstallFailed", rootActivity.GetTagItem("error.type"));
        Assert.Equal("product", rootActivity.GetTagItem("error.category"));
    }

    [Fact]
    public void RecordException_SecondExceptionOverwritesFirstOnRoot()
    {
        // Two exceptions on the same trace — last one wins on the root span
        using var rootActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-overwrite-root", ActivityKind.Internal);
        Assert.NotNull(rootActivity);

        using var commandActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-overwrite-cmd", ActivityKind.Internal);
        Assert.NotNull(commandActivity);

        SimulateRecordException(commandActivity,
            new DotnetInstallException(DotnetInstallErrorCode.AdminPathBlocked, "blocked"));
        SimulateRecordException(commandActivity,
            new DotnetInstallException(DotnetInstallErrorCode.InstallFailed, "failed"));

        // Root should have the last error (SetTag overwrites)
        Assert.Equal("InstallFailed", rootActivity.GetTagItem("error.type"));
        Assert.Equal("product", rootActivity.GetTagItem("error.category"));
    }

    [Fact]
    public void RecordException_DeeplyNestedActivity_PropagatesTagsToRoot()
    {
        // Root → child → grandchild: exception on grandchild should reach root
        using var rootActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-deep-root", ActivityKind.Internal);
        Assert.NotNull(rootActivity);

        using var childActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-deep-child", ActivityKind.Internal);
        Assert.NotNull(childActivity);

        using var grandchildActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-deep-grandchild", ActivityKind.Internal);
        Assert.NotNull(grandchildActivity);

        SimulateRecordException(grandchildActivity,
            new DotnetInstallException(DotnetInstallErrorCode.InstallFailed, "install failed"));

        Assert.Equal("InstallFailed", grandchildActivity.GetTagItem("error.type"));
        Assert.Equal("InstallFailed", rootActivity.GetTagItem("error.type"));
    }

    [Fact]
    public void InnerRecordException_ThenOuterRecordException_LastWinsOnRoot()
    {
        // Simulates: InstallWorkflow throws DotnetInstallException(InstallFailed),
        // CommandBase catches it and calls RecordException on commandActivity.
        // Then Program.Main catches it and calls RecordException on rootActivity.
        // The root span should end up with the outermost exception's error type.
        using var rootActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-stack-root", ActivityKind.Internal);
        Assert.NotNull(rootActivity);

        using var commandActivity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-stack-command", ActivityKind.Internal);
        Assert.NotNull(commandActivity);

        // Step 1: CommandBase catches the install exception
        SimulateRecordException(commandActivity,
            new DotnetInstallException(DotnetInstallErrorCode.InstallFailed, "install failed"));

        // Root should now have "InstallFailed" via parent walk
        Assert.Equal("InstallFailed", rootActivity.GetTagItem("error.type"));

        commandActivity.Stop();

        // Step 2: If outer code records a different exception on root directly
        SimulateRecordException(rootActivity,
            new InvalidOperationException("Something broke"));

        // Root should now show the outer exception-level error
        Assert.Equal("InvalidOperation", rootActivity.GetTagItem("error.type"));
        Assert.Equal("product", rootActivity.GetTagItem("error.category"));

        // Command span should still have its original error
        Assert.Equal("InstallFailed", commandActivity.GetTagItem("error.type"));
    }

    [Fact]
    public void RecordException_ClassifiesByErrorCode()
    {
        using var activity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-classifier-default", ActivityKind.Internal);
        Assert.NotNull(activity);

        // AdminPathBlocked is classified as User by ErrorCategoryClassifier
        SimulateRecordException(activity,
            new DotnetInstallException(DotnetInstallErrorCode.AdminPathBlocked, "blocked"));

        Assert.Equal("user", activity.GetTagItem("error.category"));
    }

    [Fact]
    public void RecordException_UnknownException_DefaultsToProduct()
    {
        using var activity = DotnetupTelemetry.CommandSource.StartActivity(
            "test-classifier-unknown", ActivityKind.Internal);
        Assert.NotNull(activity);

        // Unknown exception types should default to product
        SimulateRecordException(activity, new Exception("something unexpected"));

        Assert.Equal("product", activity.GetTagItem("error.category"));
    }
}

[Collection("DotnetupEnvironmentMutationTests")]
public class FirstRunNoticeTests : IDisposable
{
    private const string NoLogoEnvVar = "DOTNET_NOLOGO";

    private readonly string _tempDir;

    public FirstRunNoticeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dotnetup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Thread-local override — safe for parallel test execution.
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
    }

    public void Dispose()
    {
        DotnetupPaths.ClearTestDataDirectoryOverride();

        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public void IsFirstRun_ReturnsTrueWhenSentinelDoesNotExist()
    {
        Assert.True(FirstRunNotice.IsFirstRun());
    }

    [Fact]
    public void ShowIfFirstRun_CreatesSentinelFile()
    {
        // Save and clear DOTNET_NOLOGO to ensure test runs the full path
        var originalNoLogo = Environment.GetEnvironmentVariable(NoLogoEnvVar);
        Environment.SetEnvironmentVariable(NoLogoEnvVar, null);

        try
        {
            var sentinelPath = DotnetupPaths.TelemetrySentinelPath;
            Assert.NotNull(sentinelPath);

            // Simulate first run with telemetry enabled
            FirstRunNotice.ShowIfFirstRun(telemetryEnabled: true);

            // Sentinel should now exist
            Assert.True(File.Exists(sentinelPath));

            // Subsequent calls should not be "first run"
            Assert.False(FirstRunNotice.IsFirstRun());
        }
        finally
        {
            Environment.SetEnvironmentVariable(NoLogoEnvVar, originalNoLogo);
        }
    }

    [Fact]
    public void ShowIfFirstRun_DoesNotCreateSentinel_WhenTelemetryDisabled()
    {
        // Simulate first run with telemetry disabled
        FirstRunNotice.ShowIfFirstRun(telemetryEnabled: false);

        // Sentinel should NOT be created (user has opted out)
        var sentinelPath = DotnetupPaths.TelemetrySentinelPath;
        Assert.NotNull(sentinelPath);
        Assert.False(File.Exists(sentinelPath));
    }
}

/// <summary>
/// Tests for ActivitySource integration - verifies that library consumers can hook into telemetry
/// using the pattern demonstrated in TelemetryIntegrationDemo.
/// </summary>
[Collection("ActivitySourceTests")]
public class ActivitySourceIntegrationTests
{
    private const string InstallationActivitySourceName = "Microsoft.Dotnet.Installation";

    [Fact]
    public void ActivityListener_CanCaptureActivities_FromInstallationActivitySource()
    {
        // Arrange - set up listener like the demo shows
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == InstallationActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        // Act - create an activity using the library's ActivitySource
        using (var activity = Metrics.ActivitySource.StartActivity("test-activity"))
        {
            activity?.SetTag("test.key", "test-value");
        }

        // Assert
        Assert.Single(capturedActivities);
        Assert.Equal("test-activity", capturedActivities[0].DisplayName);
        Assert.Contains(capturedActivities[0].Tags, t => t.Key == "test.key" && t.Value == "test-value");
    }
}

public class TrackedOperationTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _capturedActivities = [];
    private readonly List<(string EventName, IDictionary<string, string?> Properties)> _capturedEvents = [];

    public TrackedOperationTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Metrics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
        Metrics.OnTrackEvent = (name, props) => _capturedEvents.Add((name, props));
    }

    public void Dispose()
    {
        _listener.Dispose();
        Metrics.OnTrackEvent = null;
    }

    [Fact]
    public void TrackedOperation_EmitsEventOnDispose()
    {
        using (var op = Metrics.Track("test-op", "test/complete"))
        {
            op.Tag("key1", "value1");
        }

        Assert.Single(_capturedEvents);
        Assert.Equal("test/complete", _capturedEvents[0].EventName);
        Assert.Equal("value1", _capturedEvents[0].Properties["key1"]);
    }

    [Fact]
    public void TrackedOperation_IncludesDuration()
    {
        using (var op = Metrics.Track("test-duration", "test/duration"))
        {
            op.Tag("foo", "bar");
        }

        Assert.Single(_capturedEvents);
        Assert.True(_capturedEvents[0].Properties.ContainsKey("operation.duration_ms"));
        Assert.True(double.TryParse(_capturedEvents[0].Properties["operation.duration_ms"], out var ms));
        Assert.True(ms >= 0);
    }

    [Fact]
    public void TrackedOperation_CapturesTagsSetViaActivityCurrent()
    {
        using (var op = Metrics.Track("test-current", "test/current"))
        {
            // Simulate code deeper in the call stack setting tags via Activity.Current
            Activity.Current?.SetTag("inner.tag", "inner-value");
            op.Tag("outer.tag", "outer-value");
        }

        Assert.Single(_capturedEvents);
        Assert.Equal("inner-value", _capturedEvents[0].Properties["inner.tag"]);
        Assert.Equal("outer-value", _capturedEvents[0].Properties["outer.tag"]);
    }

    [Fact]
    public void TrackedOperation_SetsTagOnUnderlyingActivity()
    {
        using (var op = Metrics.Track("test-span-tags", "test/span"))
        {
            op.Tag("span.key", "span-value");
        }

        Assert.Single(_capturedActivities);
        Assert.Contains(_capturedActivities[0].Tags, t => t.Key == "span.key" && t.Value == "span-value");
    }

    [Fact]
    public void TrackedOperation_SetStatus_SetsOnActivity()
    {
        using (var op = Metrics.Track("test-status", "test/status"))
        {
            op.SetStatus(ActivityStatusCode.Error, "test failure");
        }

        Assert.Single(_capturedActivities);
        Assert.Equal(ActivityStatusCode.Error, _capturedActivities[0].Status);
    }

    [Fact]
    public void TrackedOperation_NoEventWhenCallbackNotRegistered()
    {
        Metrics.OnTrackEvent = null;

        using (var op = Metrics.Track("test-no-callback", "test/no-callback"))
        {
            op.Tag("key", "value");
        }

        Assert.Empty(_capturedEvents);
    }
}

/// <summary>
/// Enforces that production code uses <c>TrackedOperation.Tag()</c> or
/// <c>InstallationActivitySource.Tag()</c> instead of the raw
/// <c>Activity.SetTag()</c> API. Only the telemetry infrastructure files
/// that wrap the raw API are allowed to call <c>.SetTag()</c> directly.
/// </summary>
public class TelemetryDualWriteEnforcementTests
{
    /// <summary>
    /// Telemetry infrastructure files that legitimately call the raw
    /// <c>Activity.SetTag()</c> API internally. Every other source file
    /// must use the <c>.Tag()</c> wrapper instead.
    /// </summary>
    private static readonly HashSet<string> s_infrastructureFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "DotnetupTelemetry.cs",           // TrackEvent / StartCommand internals
        "ErrorCodeMapper.cs",             // Applies error tags to specific activity instances
        "InstallationActivitySource.cs",  // Tag() static helper wraps Activity.Current
        "TrackedOperation.cs",            // Tag() instance method wraps _activity
    };

    [Fact]
    public void DotnetupSource_ShouldNotCallSetTagDirectly()
    {
        var installerDir = FindInstallerSourceDirectory();
        if (installerDir is null)
        {
            // Skip if source directory not found (e.g., running from packaged test)
            return;
        }

        var violations = new List<string>();

        // Scan both dotnetup and the installation library
        var searchDirs = new[]
        {
            Path.Combine(installerDir, "dotnetup"),
            Path.Combine(installerDir, "Microsoft.Dotnet.Installation")
        };

        foreach (var searchDir in searchDirs.Where(Directory.Exists))
        {
            var csFiles = Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                var fileName = Path.GetFileName(file);
                if (s_infrastructureFiles.Contains(fileName))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.Contains(".SetTag(", StringComparison.Ordinal))
                    {
                        violations.Add($"{Path.GetRelativePath(installerDir, file)}:{i + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found {violations.Count} direct .SetTag() call(s) outside infrastructure files. " +
            "Use TrackedOperation.Tag() or InstallationActivitySource.Tag() instead.\n\n" +
            string.Join("\n", violations));
    }

    private static string? FindInstallerSourceDirectory()
    {
        // Walk up from test assembly location to find src/Installer
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "Installer");
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "dotnetup")))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return null;
    }
}
