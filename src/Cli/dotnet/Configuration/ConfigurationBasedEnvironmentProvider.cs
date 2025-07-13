// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Configuration.DotnetCli.Services;

namespace Microsoft.DotNet.Cli.Configuration
{
    /// <summary>
    /// Bridge between the new configuration system and existing IEnvironmentProvider interface.
    /// Provides backward compatibility while enabling migration to the new configuration system.
    /// </summary>
    public class ConfigurationBasedEnvironmentProvider : IEnvironmentProvider
    {
        private readonly IDotNetConfigurationService _configurationService;
        private readonly IEnvironmentProvider _fallbackProvider;

        // Reverse mapping from environment variable names to canonical keys
        private static readonly Dictionary<string, string> EnvironmentToCanonicalMappings = new()
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
        };

        public ConfigurationBasedEnvironmentProvider(
            IDotNetConfigurationService configurationService,
            IEnvironmentProvider? fallbackProvider = null)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _fallbackProvider = fallbackProvider ?? new EnvironmentProvider();
        }

        public IEnumerable<string> ExecutableExtensions => _fallbackProvider.ExecutableExtensions;

        public string? GetCommandPath(string commandName, params string[] extensions)
        {
            return _fallbackProvider.GetCommandPath(commandName, extensions);
        }

        public string? GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
        {
            return _fallbackProvider.GetCommandPathFromRootPath(rootPath, commandName, extensions);
        }

        public string? GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions)
        {
            return _fallbackProvider.GetCommandPathFromRootPath(rootPath, commandName, extensions);
        }

        public string? GetEnvironmentVariable(string name)
        {
            // First try to get from the new configuration system
            if (EnvironmentToCanonicalMappings.TryGetValue(name, out var canonicalKey))
            {
                var value = _configurationService.RawConfiguration[canonicalKey];
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            // Fall back to direct environment variable access
            return _fallbackProvider.GetEnvironmentVariable(name);
        }

        public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
        {
            var value = GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return value.ToLowerInvariant() switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                _ => defaultValue
            };
        }

        public int? GetEnvironmentVariableAsNullableInt(string name)
        {
            var value = GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            return int.TryParse(value, out var result) ? result : null;
        }

        public string? GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
        {
            return _fallbackProvider.GetEnvironmentVariable(variable, target);
        }

        public void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
        {
            _fallbackProvider.SetEnvironmentVariable(variable, value, target);
        }
    }
}
