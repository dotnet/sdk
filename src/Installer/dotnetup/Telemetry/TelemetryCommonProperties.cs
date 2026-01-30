// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Provides common telemetry properties for dotnetup.
/// </summary>
internal static class TelemetryCommonProperties
{
    private static readonly Lazy<string> s_deviceId = new(GetDeviceId);
    private static readonly Lazy<bool> s_isCIEnvironment = new(DetectCIEnvironment);
    private static readonly Lazy<bool> s_isDevBuild = new(DetectDevBuild);

    /// <summary>
    /// Environment variable to mark telemetry as coming from a dev build.
    /// </summary>
    private const string DevBuildEnvVar = "DOTNETUP_DEV_BUILD";

    /// <summary>
    /// Gets common attributes for the OpenTelemetry resource.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, object>> GetCommonAttributes(string sessionId)
    {
        return new Dictionary<string, object>
        {
            ["session.id"] = sessionId,
            ["device.id"] = s_deviceId.Value,
            ["os.platform"] = GetOSPlatform(),
            ["os.version"] = Environment.OSVersion.VersionString,
            ["process.arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["ci.detected"] = s_isCIEnvironment.Value,
            ["dotnetup.version"] = GetVersion(),
            ["dev.build"] = s_isDevBuild.Value
        };
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

    private static string GetOSPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }
        return "Unknown";
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
            // Fallback to a new GUID if device ID retrieval fails
            return Guid.NewGuid().ToString();
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

    private static string GetVersion()
    {
        return typeof(TelemetryCommonProperties).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";
    }
}
