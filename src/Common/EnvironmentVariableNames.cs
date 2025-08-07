// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

static class EnvironmentVariableNames
{
    public static readonly string ALLOW_TARGETING_PACK_CACHING = "DOTNETSDK_ALLOW_TARGETING_PACK_CACHING";
    public static readonly string WORKLOAD_PACK_ROOTS = "DOTNETSDK_WORKLOAD_PACK_ROOTS";
    public static readonly string WORKLOAD_MANIFEST_ROOTS = "DOTNETSDK_WORKLOAD_MANIFEST_ROOTS";
    public static readonly string WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS = "DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS";
    public static readonly string WORKLOAD_UPDATE_NOTIFY_DISABLE = "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE";
    public static readonly string WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS = "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS";
    public static readonly string WORKLOAD_DISABLE_PACK_GROUPS = "DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS";
    public static readonly string DISABLE_PUBLISH_AND_PACK_RELEASE = "DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE";
    public static readonly string DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS = "DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS";
    public static readonly string DOTNET_CLI_FORCE_UTF8_ENCODING = nameof(DOTNET_CLI_FORCE_UTF8_ENCODING);
    public static readonly string TELEMETRY_OPTOUT = "DOTNET_CLI_TELEMETRY_OPTOUT";
    public static readonly string DOTNET_ROOT = "DOTNET_ROOT";
    public static readonly string DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG = "DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG";
    public static readonly string DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR";
    public static readonly string DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER = "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER";
    public static readonly string DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT = "DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT";
    public static readonly string DOTNET_GENERATE_ASPNET_CERTIFICATE = nameof(DOTNET_GENERATE_ASPNET_CERTIFICATE);
    public static readonly string DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = nameof(DOTNET_ADD_GLOBAL_TOOLS_TO_PATH);
    public static readonly string DOTNET_NOLOGO = nameof(DOTNET_NOLOGO);
    public static readonly string DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK = nameof(DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK);

#if NET7_0_OR_GREATER

    public static string? TryGetDotNetRootVariableName(string runtimeIdentifier, string defaultAppHostRuntimeIdentifier)
        => TryGetDotNetRootVariableNameImpl(runtimeIdentifier, defaultAppHostRuntimeIdentifier, RuntimeInformation.ProcessArchitecture);

    internal static string? TryGetDotNetRootVariableNameImpl(string runtimeIdentifier, string defaultAppHostRuntimeIdentifier, Architecture currentArchitecture)
    {
        // If the app targets the same architecture as SDK is running on or an unknown architecture, set DOTNET_ROOT_arch.
        // If the app targets different architecture from the SDK, do not set DOTNET_ROOT.

        if (!TryParseArchitecture(runtimeIdentifier, out var targetArchitecture) && !TryParseArchitecture(defaultAppHostRuntimeIdentifier, out targetArchitecture) ||
            targetArchitecture == currentArchitecture)
        {
            return $"{DOTNET_ROOT}_{currentArchitecture.ToString().ToUpperInvariant()}";
        }

        return null;
    }

    internal static bool TryParseArchitecture(string runtimeIdentifier, out Architecture architecture)
    {
        // RID is [os].[version]-[architecture]-[additional qualifiers]
        // See https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

        int archStart = runtimeIdentifier.IndexOf('-') + 1;
        if (archStart <= 0)
        {
            architecture = default;
            return false;
        }

        int archEnd = runtimeIdentifier.IndexOf('-', archStart);
        var span = runtimeIdentifier.AsSpan(archStart, (archEnd > 0 ? archEnd : runtimeIdentifier.Length) - archStart);

        return Enum.TryParse(span, ignoreCase: true, out architecture);
    }

    public static Version? TryParseTargetFrameworkVersion(string targetFrameworkVersion)
    {
        // TargetFrameworkVersion appears as "vX.Y" in msbuild. Ignore the leading 'v'.
        return !string.IsNullOrEmpty(targetFrameworkVersion) && Version.TryParse(targetFrameworkVersion.Substring(1), out var version) ? version : null;
    }
#endif
}
