// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Provides common telemetry properties for dotnetup.
/// </summary>
internal static class TelemetryCommonProperties
{
    private static readonly Lazy<string> s_deviceId = new(GetDeviceId);
    private static readonly Lazy<bool> s_isCIEnvironment = new(DetectCIEnvironment);
    private static readonly Lazy<string?> s_llmEnvironment = new(DetectLLMEnvironment);
    private static readonly Lazy<bool> s_isDevBuild = new(DetectDevBuild);

    /// <summary>
    /// Gets common attributes for the OpenTelemetry resource.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, object>> GetCommonAttributes(string sessionId)
    {
        var attributes = new Dictionary<string, object>
        {
            ["session.id"] = sessionId,
            ["device.id"] = s_deviceId.Value,
            ["os.platform"] = RuntimeInformation.OSDescription,
            ["os.version"] = Environment.OSVersion.VersionString,
            ["process.arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["ci.detected"] = s_isCIEnvironment.Value,
            ["dotnetup.version"] = GetVersion(),
            ["dev.build"] = s_isDevBuild.Value
        };

        // Add LLM environment if detected (same detection as .NET SDK)
        var llmEnv = s_llmEnvironment.Value;
        if (!string.IsNullOrEmpty(llmEnv))
        {
            attributes["llm.agent"] = llmEnv;
        }

        return attributes;
    }

    /// <summary>
    /// Hashes a path for privacy-safe telemetry.
    /// </summary>
    public static string HashPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return Hash(path);
    }

    /// <summary>
    /// Computes a SHA256 hash of the input string.
    /// </summary>
    public static string Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string GetDeviceId()
    {
        try
        {
            // Reuse the SDK's device ID getter for consistency
            return DeviceIdGetter.GetDeviceId();
        }
        catch
        {
            // Fallback to empty string if device ID retrieval fails (consistent with SDK behavior)
            return string.Empty;
        }
    }

    private static bool DetectCIEnvironment()
    {
        try
        {
            // Reuse the SDK's CI detection
            var detector = new CIEnvironmentDetectorForTelemetry();
            return detector.IsCIEnvironment();
        }
        catch
        {
            return false;
        }
    }

    private static string? DetectLLMEnvironment()
    {
        try
        {
            // Reuse the SDK's LLM/agent detection
            var detector = new LLMEnvironmentDetectorForTelemetry();
            return detector.GetLLMEnvironment();
        }
        catch
        {
            return null;
        }
    }

    private static bool DetectDevBuild()
    {
        // Debug builds are always considered dev builds
#if DEBUG
        return true;
#else
        // Check for DOTNETUP_DEV_BUILD environment variable (for release builds in dev scenarios)
        var devBuildValue = Environment.GetEnvironmentVariable(DevBuildEnvVar);
        return string.Equals(devBuildValue, "1", StringComparison.Ordinal) ||
               string.Equals(devBuildValue, "true", StringComparison.OrdinalIgnoreCase);
#endif
    }

    internal static string GetVersion()
    {
        var version = BuildInfo.Version;

        // For dev builds, append the commit SHA so we can correlate failures to specific commits.
        // Prod telemetry stays clean (e.g., "10.0.100-alpha"), while dev shows "10.0.100-alpha@abc1234".
        if (s_isDevBuild.Value)
        {
            var commitSha = BuildInfo.CommitSha;
            if (commitSha != "unknown")
            {
                return $"{version}@{commitSha}";
            }
        }

        return version;
    }
}
