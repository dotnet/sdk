// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias DotNetNativeWrapper;

using System.Security;
using System.Text.Json;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;
using EnvironmentProvider = DotNetNativeWrapper::Microsoft.DotNet.NativeWrapper.EnvironmentProvider;
using HostFxrNotFoundException = DotNetNativeWrapper::Microsoft.DotNet.NativeWrapper.HostFxrNotFoundException;
using HostFxrRuntimePropertyNotSetException = DotNetNativeWrapper::Microsoft.DotNet.NativeWrapper.HostFxrRuntimePropertyNotSetException;
using NETCoreSdkResolverNativeWrapper = DotNetNativeWrapper::Microsoft.DotNet.NativeWrapper.NETCoreSdkResolverNativeWrapper;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Add;

/// <summary>
/// Resolves the SDK version to write when the user does not specify one explicitly.
/// </summary>
internal static class SdkAddVersionHelper
{
    private const string PackageCacheFolderName = "dotnet-sdk-add";
    /// <summary>
    /// Resolves the version to store in the project or file-based app, and whether an existing reference may be left unchanged.
    /// </summary>
    public static (string? Version, bool LeaveExistingUnchangedWhenVersionIsNull) ResolveVersion(
        string sdkName,
        string? userVersion,
        bool userVersionSpecified,
        string startDirectory)
    {
        if (userVersionSpecified)
        {
            return (userVersion, LeaveExistingUnchangedWhenVersionIsNull: false);
        }

        if (TryGetMsbuildSdkVersionFromGlobalJson(startDirectory, sdkName, out _))
        {
            return (null, LeaveExistingUnchangedWhenVersionIsNull: true);
        }

        if (IsBundledInCurrentDotNetSdk(sdkName, startDirectory))
        {
            return (null, LeaveExistingUnchangedWhenVersionIsNull: true);
        }

        var nugetVersion = GetLatestNuGetSdkVersion(sdkName, startDirectory);
        return (nugetVersion, LeaveExistingUnchangedWhenVersionIsNull: false);
    }

    internal static bool TryGetMsbuildSdkVersionFromGlobalJson(string? startDirectory, string sdkName, out string? version)
    {
        version = null;
        string? globalJsonPath = SdkDirectoryWorkloadManifestProvider.GetGlobalJsonPath(startDirectory);
        if (globalJsonPath is null)
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or SecurityException)
        {
            throw new GracefulException(
                string.Format(CliCommandStrings.GlobalJsonFileParsingFailed, globalJsonPath, ex.Message));
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("msbuild-sdks", out JsonElement msbuildSdks) ||
                msbuildSdks.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (JsonProperty property in msbuildSdks.EnumerateObject())
            {
                if (string.Equals(property.Name, sdkName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    version = property.Value.GetString();
                    return !string.IsNullOrEmpty(version);
                }
            }
        }

        return false;
    }

    private static bool IsBundledInCurrentDotNetSdk(string sdkName, string startDirectory)
    {
        string? dotnetDir = EnvironmentProvider.GetDotnetExeDirectory(
            key => Environment.GetEnvironmentVariable(key),
            getCurrentProcessPath: () => Environment.ProcessPath ?? string.Empty);

        if (string.IsNullOrEmpty(dotnetDir))
        {
            return false;
        }

        foreach (string sdkDirectory in GetSdkDirectoriesToSearch(dotnetDir, startDirectory))
        {
            string sdkImportPath = Path.Combine(sdkDirectory, "Sdks", sdkName, "Sdk");
            if (Directory.Exists(sdkImportPath))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetSdkDirectoriesToSearch(string dotnetDir, string startDirectory)
    {
        string? resolvedSdkDirectory = TryResolveSdkDirectory(dotnetDir, startDirectory);
        if (resolvedSdkDirectory is not null)
        {
            yield return resolvedSdkDirectory;
            yield break;
        }

        foreach (string sdkDirectory in GetInstalledSdkDirectories(dotnetDir))
        {
            yield return sdkDirectory;
        }
    }

    private static string? TryResolveSdkDirectory(string dotnetDir, string startDirectory)
    {
        try
        {
            return NETCoreSdkResolverNativeWrapper.ResolveSdk(dotnetDir, startDirectory).ResolvedSdkDirectory;
        }
        catch (Exception ex) when (ex is HostFxrRuntimePropertyNotSetException or HostFxrNotFoundException)
        {
            return null;
        }
    }

    private static IEnumerable<string> GetInstalledSdkDirectories(string dotnetDir)
    {
        string sdkRoot = Path.Combine(dotnetDir, "sdk");
        if (!Directory.Exists(sdkRoot))
        {
            yield break;
        }

        IEnumerable<string> sdkDirectories;
        try
        {
            sdkDirectories = NETCoreSdkResolverNativeWrapper.GetAvailableSdks(dotnetDir);
        }
        catch (Exception ex) when (ex is HostFxrRuntimePropertyNotSetException or HostFxrNotFoundException)
        {
            sdkDirectories = Directory.GetDirectories(sdkRoot);
        }

        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(Product.Version))
        {
            string productVersionPath = Path.Combine(sdkRoot, Product.Version);
            if (Directory.Exists(productVersionPath) && yielded.Add(productVersionPath))
            {
                yield return productVersionPath;
            }
        }

        foreach (string sdkDirectory in sdkDirectories)
        {
            if (yielded.Add(sdkDirectory))
            {
                yield return sdkDirectory;
            }
        }
    }

    private static string GetLatestNuGetSdkVersion(string sdkName, string startDirectory)
    {
        string packageInstallDir = Path.Combine(Path.GetTempPath(), PackageCacheFolderName, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageInstallDir);

        try
        {
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(
                packageInstallDir: new DirectoryPath(packageInstallDir),
                currentWorkingDirectory: startDirectory);

            NuGetVersion version = downloader
                .GetLatestPackageVersion(new PackageId(sdkName), includePreview: false)
                .GetAwaiter()
                .GetResult();

            return version.ToNormalizedString();
        }
        catch (Exception ex)
        {
            throw new GracefulException(
                string.Format(CliCommandStrings.SdkVersionResolutionFailed, sdkName, ex.Message));
        }
        finally
        {
            TryDeleteDirectory(packageInstallDir);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
