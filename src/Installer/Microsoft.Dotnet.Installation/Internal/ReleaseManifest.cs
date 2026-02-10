// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net.Http;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Handles downloading and parsing .NET release manifests to find the correct installer/archive for a given installation.
/// </summary>
internal class ReleaseManifest
{
    private ProductCollection? _productCollection;

    public ReleaseManifest()
    {
    }

    /// <summary>
    /// Finds the appropriate release file for the given installation.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <returns>The matching ReleaseFile, throws if none are available.</returns>
    public ReleaseFile? FindReleaseFile(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
    {
        try
        {
            var productCollection = GetReleasesIndex();
            var product = FindProduct(productCollection, resolvedVersion)
                ?? throw new DotnetInstallException(
                    DotnetInstallErrorCode.VersionNotFound,
                    $"No product found for version {resolvedVersion}",
                    version: resolvedVersion.ToString(),
                    component: installRequest.Component.ToString());
            var release = FindRelease(product, resolvedVersion, installRequest.Component)
                ?? throw new DotnetInstallException(
                    DotnetInstallErrorCode.ReleaseNotFound,
                    $"No release found for version {resolvedVersion}",
                    version: resolvedVersion.ToString(),
                    component: installRequest.Component.ToString());
            return FindMatchingFile(release, installRequest, resolvedVersion);
        }
        catch (DotnetInstallException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ManifestFetchFailed,
                $"Failed to fetch release manifest: {ex.Message}",
                ex,
                version: resolvedVersion.ToString(),
                component: installRequest.Component.ToString());
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ManifestParseFailed,
                $"Failed to parse release manifest: {ex.Message}",
                ex,
                version: resolvedVersion.ToString(),
                component: installRequest.Component.ToString());
        }
        catch (Exception ex)
        {
            throw new DotnetInstallException(
                DotnetInstallErrorCode.Unknown,
                $"Failed to find an available release for install {installRequest}: {ex.Message}",
                ex,
                version: resolvedVersion.ToString(),
                component: installRequest.Component.ToString());
        }
    }

    /// <summary>
    /// Gets or loads the ProductCollection.
    /// TODO: Caching of the manifest or product collection after the program exits would be ideal.
    /// </summary>
    public ProductCollection GetReleasesIndex()
    {
        if (_productCollection is not null)
        {
            return _productCollection;
        }

        _productCollection = ProductCollection.GetAsync().GetAwaiter().GetResult();
        return _productCollection;
    }

    /// <summary>
    /// Finds the product for the given version.
    /// </summary>
    private static Product? FindProduct(ProductCollection productCollection, ReleaseVersion releaseVersion)
    {
        var majorMinor = $"{releaseVersion.Major}.{releaseVersion.Minor}";
        return productCollection.FirstOrDefault(p => p.ProductVersion == majorMinor);
    }

    /// <summary>
    /// Finds the specific release for the given version.
    /// </summary>
    private static ReleaseComponent? FindRelease(Product product, ReleaseVersion resolvedVersion, InstallComponent component)
    {
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult().ToList();

        foreach (var release in releases)
        {
            if (component == InstallComponent.SDK)
            {
                foreach (var sdk in release.Sdks)
                {
                    if (sdk.Version.Equals(resolvedVersion))
                    {
                        return sdk;
                    }
                }
            }
            else
            {
                var runtimesQuery = component switch
                {
                    InstallComponent.ASPNETCore => release.Runtimes
                        .Where(r => r.Name.Contains("ASP", StringComparison.OrdinalIgnoreCase)),
                    InstallComponent.WindowsDesktop => release.Runtimes
                        .Where(r => r.Name.Contains("Desktop", StringComparison.OrdinalIgnoreCase)),
                    _ => release.Runtimes
                        .Where(r => r.Name.Contains(".NET Runtime", StringComparison.OrdinalIgnoreCase) ||
                               r.Name.Contains(".NET Core Runtime", StringComparison.OrdinalIgnoreCase)),
                };
                foreach (var runtime in runtimesQuery)
                {
                    if (runtime.Version.Equals(resolvedVersion))
                    {
                        return runtime;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the matching file in the release for the given installation requirements.
    /// </summary>
    private static ReleaseFile? FindMatchingFile(ReleaseComponent release, DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
    {
        var rid = DotnetupUtilities.GetRuntimeIdentifier(installRequest.InstallRoot.Architecture);
        var fileExtension = DotnetupUtilities.GetArchiveFileExtensionForPlatform();

        var matchingFiles = release.Files
             .Where(f => f.Rid == rid) // TODO: Do we support musl here?
             .Where(f => f.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
             .ToList();

        if (matchingFiles.Count == 0)
        {
            return null;
        }

        return matchingFiles.First();
    }
}
