// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Outcome of <see cref="ReleaseManifest.TryFindReleaseFile"/>.
/// </summary>
internal enum FindReleaseFileStatus
{
    /// <summary>The release was found and a matching archive for the platform exists.</summary>
    Found,

    /// <summary>The release exists in the manifest but has no archive for the requested RID/extension.</summary>
    NoMatchingFile,

    /// <summary>No product (major.minor) for the version was found in the manifest.</summary>
    ProductNotFound,

    /// <summary>The product was found, but the specific version is not listed under it.</summary>
    ReleaseNotFound,
}

/// <summary>
/// Result of looking up a release file in the manifest.
/// </summary>
internal readonly record struct FindReleaseFileResult(FindReleaseFileStatus Status, ReleaseFile? File)
{
    public static FindReleaseFileResult FromFile(ReleaseFile? file) =>
        new(file is null ? FindReleaseFileStatus.NoMatchingFile : FindReleaseFileStatus.Found, file);

    public static FindReleaseFileResult ProductNotFound { get; } = new(FindReleaseFileStatus.ProductNotFound, null);
    public static FindReleaseFileResult ReleaseNotFound { get; } = new(FindReleaseFileStatus.ReleaseNotFound, null);
}

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
    /// Attempts to find the appropriate release file for the given installation.
    /// Returns a <see cref="FindReleaseFileResult"/> indicating whether the
    /// product/release/file was found. Network and parse failures are still
    /// thrown as <see cref="DotnetInstallException"/>.
    /// </summary>
    public virtual FindReleaseFileResult TryFindReleaseFile(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion)
    {
        try
        {
            var productCollection = GetReleasesIndex();
            var product = FindProduct(productCollection, resolvedVersion);
            if (product is null)
            {
                return FindReleaseFileResult.ProductNotFound;
            }
            var release = FindRelease(product, resolvedVersion, installRequest.Component);
            if (release is null)
            {
                return FindReleaseFileResult.ReleaseNotFound;
            }
            return FindReleaseFileResult.FromFile(FindMatchingFile(release, installRequest));
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
    /// Determines whether a runtime component's display name matches the requested install component type.
    /// This is used to filter <see cref="ProductRelease.Runtimes"/> to find the correct component.
    /// </summary>
    /// <remarks>
    /// Known component names from the releases library:
    /// - ".NET Core Runtime" or ".NET Runtime" for the base runtime
    /// - "ASP.NET Core Runtime" for ASP.NET Core
    /// - "Desktop Runtime" for Windows Desktop
    /// The filters must be specific enough that ordering of Runtimes does not affect which component is selected.
    /// </remarks>
    internal static bool IsMatchingRuntimeComponent(string componentName, InstallComponent component)
    {
        return component switch
        {
            InstallComponent.ASPNETCore => componentName.Contains("ASP", StringComparison.OrdinalIgnoreCase),
            InstallComponent.WindowsDesktop => componentName.Contains("Desktop", StringComparison.OrdinalIgnoreCase),
            _ => !componentName.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) &&
                 (componentName.Contains(".NET Runtime", StringComparison.OrdinalIgnoreCase) ||
                  componentName.Contains(".NET Core Runtime", StringComparison.OrdinalIgnoreCase)),
        };
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
                foreach (var runtime in release.Runtimes.Where(r => IsMatchingRuntimeComponent(r.Name, component)))
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
    /// <remarks>
    /// Some components (notably ASP.NET Core) include both regular and composite (AOT) archives
    /// with the same RID and extension. Composite archives contain "composite" in the file name
    /// and are filtered out to avoid selecting the wrong archive.
    /// </remarks>
    private static ReleaseFile? FindMatchingFile(ReleaseComponent release, DotnetInstallRequest installRequest)
    {
        var rid = DotnetupUtilities.GetRuntimeIdentifier(installRequest.InstallRoot.Architecture);
        var fileExtension = DotnetupUtilities.GetArchiveFileExtensionForPlatform();

        var matchingFiles = release.Files
             .Where(f => f.Rid == rid) // TODO: Do we support musl here?
             .Where(f => f.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
             .Where(f => !IsCompositeArchive(f.Name))
             .Where(f => !IsApphostPackArchive(f.Name))
             .ToList();

        if (matchingFiles.Count == 0)
        {
            return null;
        }

        return matchingFiles.First();
    }

    /// <summary>
    /// Determines whether a release file name refers to a composite (AOT) archive.
    /// Composite archives such as "aspnetcore-runtime-composite-linux-x64.tar.gz"
    /// should not be selected for standard installs.
    /// </summary>
    internal static bool IsCompositeArchive(string fileName)
    {
        return fileName.Contains("composite", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a release file name refers to an apphost-pack archive.
    /// Apphost packs (e.g. "dotnet-apphost-pack-win-x64.zip") contain only NuGet packs
    /// and are not the actual runtime/SDK archives that should be installed.
    /// </summary>
    internal static bool IsApphostPackArchive(string fileName)
    {
        return fileName.Contains("apphost-pack", StringComparison.OrdinalIgnoreCase);
    }
}
