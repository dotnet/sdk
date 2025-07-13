// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Extensions.Configuration.DotnetCli.Providers;

/// <summary>
/// Configuration provider that maps DOTNET_ prefixed environment variables to canonical keys.
/// </summary>
public class DotNetEnvironmentConfigurationProvider : ConfigurationProvider
{
    private static readonly Dictionary<string, string> EnvironmentKeyMappings = new()
    {
        ["DOTNET_HOST_PATH"] = "RuntimeHost:HostPath",
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "CliUserExperience:TelemetryOptOut",
        ["DOTNET_NOLOGO"] = "CliUserExperience:NoLogo",
        ["DOTNET_CLI_FORCE_UTF8_ENCODING"] = "CliUserExperience:ForceUtf8Encoding",
        ["DOTNET_CLI_UI_LANGUAGE"] = "CliUserExperience:UILanguage",
        ["DOTNET_CLI_TELEMETRY_PROFILE"] = "CliUserExperience:TelemetryProfile",
        ["DOTNET_CLI_PERF_LOG"] = "Development:PerfLogEnabled",
        ["DOTNET_PERF_LOG_COUNT"] = "Development:PerfLogCount",
        ["DOTNET_CLI_HOME"] = "Development:CliHome",
        ["DOTNET_CLI_CONTEXT_VERBOSE"] = "Development:ContextVerbose",
        ["DOTNET_MULTILEVEL_LOOKUP"] = "RuntimeHost:MultilevelLookup",
        ["DOTNET_ROLL_FORWARD"] = "RuntimeHost:RollForward",
        ["DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX"] = "RuntimeHost:RollForwardOnNoCandidateFx",
        ["DOTNET_ROOT"] = "RuntimeHost:Root",
        ["DOTNET_ROOT(x86)"] = "RuntimeHost:RootX86",
        ["DOTNET_CLI_RUN_MSBUILD_OUTOFPROC"] = "Build:RunMSBuildOutOfProc",
        ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "Build:UseMSBuildServer",
        ["DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER"] = "Build:ConfigureMSBuildTerminalLogger",
        ["DOTNET_CLI_DISABLE_PUBLISH_AND_PACK_RELEASE"] = "Build:DisablePublishAndPackRelease",
        ["DOTNET_CLI_LAZY_PUBLISH_AND_PACK_RELEASE_FOR_SOLUTIONS"] = "Build:LazyPublishAndPackReleaseForSolutions",
        ["DOTNET_MSBUILD_SDK_RESOLVER_ENABLE_LOG"] = "SdkResolver:EnableLog",
        ["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = "SdkResolver:SdksDirectory",
        ["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER"] = "SdkResolver:SdksVersion",
        ["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = "SdkResolver:CliDirectory",
        ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "Workload:UpdateNotifyDisable",
        ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_INTERVAL_HOURS"] = "Workload:UpdateNotifyIntervalHours",
        ["DOTNET_CLI_WORKLOAD_DISABLE_PACK_GROUPS"] = "Workload:DisablePackGroups",
        ["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = "Workload:SkipIntegrityCheck",
        ["DOTNETSDK_WORKLOAD_MANIFEST_ROOTS"] = "Workload:ManifestRoots",
        ["DOTNETSDK_WORKLOAD_PACK_ROOTS"] = "Workload:PackRoots",
        ["DOTNETSDK_WORKLOAD_MANIFEST_IGNORE_DEFAULT_ROOTS"] = "Workload:ManifestIgnoreDefaultRoots",
        ["DOTNETSDK_ALLOW_TARGETING_PACK_CACHING"] = "Development:AllowTargetingPackCaching",
        ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "FirstTimeUse:GenerateAspNetCertificate",
        ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "FirstTimeUse:AddGlobalToolsToPath",
        ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "FirstTimeUse:SkipFirstTimeExperience",
        ["DOTNET_TOOLS_ALLOW_MANIFEST_IN_ROOT"] = "Tool:AllowManifestInRoot",
        ["DOTNET_NUGET_SIGNATURE_VERIFICATION"] = "NuGet:SignatureVerificationEnabled",
    };

    public override void Load()
    {
        Data.Clear();

        foreach (var mapping in EnvironmentKeyMappings)
        {
            var value = Environment.GetEnvironmentVariable(mapping.Key);
            if (!string.IsNullOrEmpty(value))
            {
                Data[mapping.Value] = value;
            }
        }

        // Handle array-type environment variables (semicolon-separated)
        HandleArrayEnvironmentVariable("DOTNETSDK_WORKLOAD_MANIFEST_ROOTS", "Workload:ManifestRoots");
        HandleArrayEnvironmentVariable("DOTNETSDK_WORKLOAD_PACK_ROOTS", "Workload:PackRoots");
    }

    private void HandleArrayEnvironmentVariable(string envVar, string configKey)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(value))
        {
            var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                Data[$"{configKey}:{i}"] = parts[i];
            }
        }
    }
}
