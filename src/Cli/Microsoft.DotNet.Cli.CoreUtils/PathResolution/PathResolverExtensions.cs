// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
/// Extension methods providing all derived paths from the three anchor points.
/// These methods compute paths based on DotnetRoot, SdkRoot, and DotnetExecutable.
/// </summary>
public static class PathResolverExtensions
{
    private static readonly string ExeSuffix =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

    private const string SdksDirectoryName = "Sdks";

    extension(IPathResolver resolver)
    {

        // ===================================================================
        // Derived Paths - From DOTNET_ROOT
        // ===================================================================

        /// <summary>
        /// Gets the directory containing all SDK versions.
        /// </summary>
        /// <example>{DOTNET_ROOT}/sdk</example>
        public string GetSdkDirectory() => Path.Combine(resolver.DotnetRoot, "sdk");

        /// <summary>
        /// Gets the directory containing workload manifests.
        /// </summary>
        /// <example>{DOTNET_ROOT}/sdk-manifests</example>
        public string GetManifestsDirectory() => Path.Combine(resolver.DotnetRoot, "sdk-manifests");

        /// <summary>
        /// Gets the directory containing workload and targeting packs.
        /// </summary>
        /// <example>{DOTNET_ROOT}/packs</example>
        public string GetPacksDirectory() => Path.Combine(resolver.DotnetRoot, "packs");

        /// <summary>
        /// Gets the directory containing shared frameworks (runtimes).
        /// </summary>
        /// <example>{DOTNET_ROOT}/shared</example>
        public string GetSharedFrameworksDirectory() => Path.Combine(resolver.DotnetRoot, "shared");

        /// <summary>
        /// Gets the directory containing the host (hostfxr).
        /// </summary>
        /// <example>{DOTNET_ROOT}/host</example>
        public string GetHostDirectory() => Path.Combine(resolver.DotnetRoot, "host");

        // ===================================================================
        // Derived Paths - From SDK_ROOT
        // ===================================================================

        /// <summary>
        /// Gets the path to MSBuild.dll.
        /// </summary>
        /// <example>{SDK_ROOT}/MSBuild.dll</example>
        public string GetMSBuildPath() => Path.Combine(resolver.SdkRoot, "MSBuild.dll");

        /// <summary>
        /// Gets the MSBuild SDKs directory.
        /// </summary>
        /// <example>{SDK_ROOT}/Sdks</example>
        public string GetMSBuildSdksPath() => Path.Combine(resolver.SdkRoot, SdksDirectoryName);

        /// <summary>
        /// Gets the directory containing bundled CLI tools (format, vstest, etc.).
        /// </summary>
        /// <example>{SDK_ROOT}/DotnetTools</example>
        public string GetBundledToolsDirectory() => Path.Combine(resolver.SdkRoot, "DotnetTools");

        /// <summary>
        /// Gets the directory containing AppHost template.
        /// </summary>
        /// <example>{SDK_ROOT}/AppHostTemplate</example>
        public string GetAppHostTemplateDirectory() => Path.Combine(resolver.SdkRoot, "AppHostTemplate");

        // ===================================================================
        // Helper Methods - General Paths
        // ===================================================================

        /// <summary>
        /// Gets the full path to a bundled tool using relative path from SDK root.
        /// </summary>
        /// <param name="relativePath">
        /// Path relative to SDK root. Examples:
        /// - "NuGet.CommandLine.XPlat.dll"
        /// - "DotnetTools/dotnet-format/dotnet-format.dll"
        /// - "FSharp/fsi.dll"
        /// </param>
        /// <returns>Full path to the bundled tool</returns>
        public string GetBundledToolPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                throw new ArgumentException("Relative path cannot be null or empty", nameof(relativePath));
            }

            return Path.Combine(resolver.SdkRoot, relativePath);
        }

        /// <summary>
        /// Gets the workload manifest directory for a specific feature band.
        /// </summary>
        /// <param name="featureBand">SDK feature band (e.g., "10.0.100")</param>
        /// <returns>Full path to manifest directory</returns>
        /// <example>{DOTNET_ROOT}/sdk-manifests/10.0.100</example>
        public string GetManifestDirectory(string featureBand)
        {
            if (string.IsNullOrEmpty(featureBand))
            {
                throw new ArgumentException("Feature band cannot be null or empty", nameof(featureBand));
            }

            return Path.Combine(resolver.GetManifestsDirectory(), featureBand);
        }

        /// <summary>
        /// Gets the path to a specific pack.
        /// </summary>
        /// <param name="packId">Pack identifier (e.g., "Microsoft.NETCore.App.Ref")</param>
        /// <param name="version">Pack version</param>
        /// <param name="rid">Runtime identifier</param>
        /// <returns>Full path to the pack</returns>
        /// <example>{DOTNET_ROOT}/packs/Microsoft.NETCore.App.Ref/10.0.0/runtimes/win-x64</example>
        public string GetPackPath(string packId, string version, string rid)
        {
            if (string.IsNullOrEmpty(packId))
            {
                throw new ArgumentException("Pack ID cannot be null or empty", nameof(packId));
            }
            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("Version cannot be null or empty", nameof(version));
            }
            if (string.IsNullOrEmpty(rid))
            {
                throw new ArgumentException("RID cannot be null or empty", nameof(rid));
            }

            return Path.Combine(resolver.GetPacksDirectory(), packId, version, "runtimes", rid);
        }

        // ===================================================================
        // Well-Known Bundled Tools
        // ===================================================================

        /// <summary>
        /// Gets the path to NuGet.CommandLine.XPlat.dll.
        /// </summary>
        public string GetNuGetPath() => resolver.GetBundledToolPath("NuGet.CommandLine.XPlat.dll");

        /// <summary>
        /// Gets the path to vstest.console.dll.
        /// </summary>
        public string GetVSTestPath() => resolver.GetBundledToolPath("vstest.console.dll");

        /// <summary>
        /// Gets the path to dotnet-format.dll.
        /// </summary>
        public string GetFormatPath() => resolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.dll");

        /// <summary>
        /// Gets the path to dotnet-format.deps.json.
        /// </summary>
        public string GetFormatDepsPath() => resolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.deps.json");

        /// <summary>
        /// Gets the path to dotnet-format.runtimeconfig.json.
        /// </summary>
        public string GetFormatRuntimeConfigPath() => resolver.GetBundledToolPath("DotnetTools/dotnet-format/dotnet-format.runtimeconfig.json");

        /// <summary>
        /// Gets the path to fsi.dll (F# Interactive).
        /// </summary>
        public string GetFsiPath() => resolver.GetBundledToolPath("FSharp/fsi.dll");

        // ===================================================================
        // Common Pack Paths
        // ===================================================================

        /// <summary>
        /// Gets the path to the Microsoft.NETCore.App reference assemblies.
        /// </summary>
        /// <param name="version">Target framework version (e.g., "10.0.0")</param>
        /// <param name="tfm">Target framework moniker (e.g., "net10.0")</param>
        public string GetNetCoreAppRefPath(string version, string tfm) => Path.Combine(
                resolver.GetPacksDirectory(),
                "Microsoft.NETCore.App.Ref",
                version,
                "ref",
                tfm);

        /// <summary>
        /// Gets the path to the Microsoft.NETCore.App.Host apphost for a specific RID.
        /// </summary>
        /// <param name="rid">Runtime identifier (e.g., "win-x64")</param>
        /// <param name="version">Runtime version</param>
        public string GetAppHostPath(string rid, string version)
        {
            string packId = $"Microsoft.NETCore.App.Host.{rid}";
            return Path.Combine(
                resolver.GetPacksDirectory(),
                packId,
                version,
                "runtimes",
                rid,
                "native",
                $"apphost{ExeSuffix}");
        }

        // ===================================================================
        // Workload Helpers
        // ===================================================================

        /// <summary>
        /// Gets the path to workload sets for a specific feature band.
        /// </summary>
        /// <param name="featureBand">SDK feature band (e.g., "10.0.100")</param>
        /// <param name="workloadSetVersion">Workload set version</param>
        public string GetWorkloadSetPath(string featureBand, string workloadSetVersion) => Path.Combine(
                resolver.GetManifestDirectory(featureBand),
                "workloadsets",
                workloadSetVersion);
    }
}
