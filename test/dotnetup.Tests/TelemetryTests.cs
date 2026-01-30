// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Xunit;

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
        Assert.Contains(osPlatform, new[] { "Windows", "Linux", "macOS", "Unknown" });
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
    [InlineData("sts", "sts")]
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

public class DotnetupTelemetryTests
{
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
}

public class LibraryActivityTagTests
{
    [Fact]
    public void NonUpdatingProgressTarget_SetsCallerTagOnActivity()
    {
        var capturedActivities = new List<System.Diagnostics.Activity>();

        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.Dotnet.Installation",
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) =>
                System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capturedActivities.Add(activity)
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        // Capture console output to avoid test noise
        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            // Use the progress target to create an activity
            var progressTarget = new NonUpdatingProgressTarget();
            using var reporter = progressTarget.CreateProgressReporter();
            var task = reporter.AddTask("test-activity", "Test Description", 100);
            task.Value = 100;
            // Disposing the reporter will stop/dispose the activities
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Verify the activity was captured and has the caller tag
        Assert.Single(capturedActivities);
        var activity = capturedActivities[0];
        Assert.Equal("dotnetup", activity.GetTagItem("caller")?.ToString());
    }
}

public class FirstRunNoticeTests
{
    [Fact]
    public void IsFirstRun_ReturnsTrueWhenSentinelDoesNotExist()
    {
        // Clean up any existing sentinel for this test
        var sentinelDir = GetSentinelDirectory();
        var sentinelPath = Path.Combine(sentinelDir, ".dotnetup-telemetry-notice");

        if (File.Exists(sentinelPath))
        {
            File.Delete(sentinelPath);
        }

        Assert.True(FirstRunNotice.IsFirstRun());
    }

    [Fact]
    public void ShowIfFirstRun_CreatesSentinelFile()
    {
        // Clean up any existing sentinel for this test
        var sentinelDir = GetSentinelDirectory();
        var sentinelPath = Path.Combine(sentinelDir, ".dotnetup-telemetry-notice");

        if (File.Exists(sentinelPath))
        {
            File.Delete(sentinelPath);
        }

        // Simulate first run with telemetry enabled
        FirstRunNotice.ShowIfFirstRun(telemetryEnabled: true);

        // Sentinel should now exist
        Assert.True(File.Exists(sentinelPath));

        // Subsequent calls should not be "first run"
        Assert.False(FirstRunNotice.IsFirstRun());
    }

    [Fact]
    public void ShowIfFirstRun_DoesNotCreateSentinel_WhenTelemetryDisabled()
    {
        // Clean up any existing sentinel for this test
        var sentinelDir = GetSentinelDirectory();
        var sentinelPath = Path.Combine(sentinelDir, ".dotnetup-telemetry-notice");

        if (File.Exists(sentinelPath))
        {
            File.Delete(sentinelPath);
        }

        // Simulate first run with telemetry disabled
        FirstRunNotice.ShowIfFirstRun(telemetryEnabled: false);

        // Sentinel should NOT be created (user has opted out)
        Assert.False(File.Exists(sentinelPath));
    }

    private static string GetSentinelDirectory()
    {
        var baseDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Path.Combine(baseDir, ".dotnetup");
    }
}
