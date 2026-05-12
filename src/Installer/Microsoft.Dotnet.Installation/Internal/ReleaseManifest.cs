// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation.Internal.Signing;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Handles downloading and parsing .NET release manifests to find the correct installer/archive for a given installation.
/// </summary>
internal class ReleaseManifest
{
    private ProductCollection? _productCollection;

    // Per-product release cache. Wrapping the value in Lazy<T> with ExecutionAndPublication
    // is required: ConcurrentDictionary.GetOrAdd does NOT guarantee single-invocation of the
    // value factory under concurrent access. Without Lazy<T>, two PrepareInstall threads asking
    // for the same channel could each download + verify the same JSON.
    private readonly ConcurrentDictionary<string, Lazy<ReadOnlyCollection<ProductRelease>>> _releaseCache = new(StringComparer.Ordinal);

    // Lazy<T>'s default mode is ExecutionAndPublication, so the orchestrator's parallel
    // PrepareConcurrent calls cannot double-instantiate the loader.
    //
    // We don't dispose the loader: ReleaseManifest's lifetime is the process (held via
    // ChannelVersionResolver -> InstallCommand) and propagating IDisposable up the chain
    // for a sub-megabyte temp directory cleanup is invasive. The OS temp cleanup picks
    // it up on next reboot; for hot paths that's good enough.
    private readonly Lazy<SignedReleaseManifestLoader> _loader;

    public ReleaseManifest()
    {
        _loader = new Lazy<SignedReleaseManifestLoader>(() => new SignedReleaseManifestLoader(
            DefaultHttpClient.Instance,
            DefaultSignatureOptions.Instance,
            indexUrl: ProductCollection.ReleasesIndexDefaultUrl));
    }

    /// <summary>
    /// Test seam — inject a fake loader (e.g. one that reads fixture JSON+sig from disk).
    /// </summary>
    internal ReleaseManifest(SignedReleaseManifestLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = new Lazy<SignedReleaseManifestLoader>(() => loader);
    }

    /// <summary>
    /// Finds the appropriate release file for the given installation.
    /// </summary>
    /// <param name="installRequest">The .NET installation request details</param>
    /// <param name="resolvedVersion">The resolved release version to find</param>
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
            var release = FindRelease(GetReleases(product), resolvedVersion, installRequest.Component)
                ?? throw new DotnetInstallException(
                    DotnetInstallErrorCode.ReleaseNotFound,
                    $"No release found for version {resolvedVersion}. Daily build versions are not yet supported.",
                    version: resolvedVersion.ToString(),
                    component: installRequest.Component.ToString());
            return FindMatchingFile(release, installRequest);
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

        _productCollection = _loader.Value.GetVerifiedReleasesIndex();
        return _productCollection;
    }

    /// <summary>
    /// Returns releases for a product. Downloaded + signature-verified once per process per
    /// product, then served from <see cref="_releaseCache"/>. <see cref="Lazy{T}"/> guarantees the
    /// verify runs exactly once even under concurrent <see cref="GetReleases"/> calls.
    ///
    /// <para>
    /// On failure, the cache entry is removed so a retry within the same process gets a fresh
    /// attempt. Without this, <see cref="Lazy{T}"/>'s exception memoization would permanently
    /// block recovery from transient errors (503, network blips) for the rest of the process.
    /// </para>
    /// </summary>
    public ReadOnlyCollection<ProductRelease> GetReleases(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var lazy = _releaseCache.GetOrAdd(product.ProductVersion, _ =>
            new Lazy<ReadOnlyCollection<ProductRelease>>(
                () => _loader.Value.GetVerifiedReleases(product),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return lazy.Value;
        }
        catch
        {
            // Atomic compare-and-remove: only drop the failed entry, not a fresh one another
            // thread may have already swapped in.
            _releaseCache.TryRemove(new KeyValuePair<string, Lazy<ReadOnlyCollection<ProductRelease>>>(product.ProductVersion, lazy));
            throw;
        }
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
    private static ReleaseComponent? FindRelease(ReadOnlyCollection<ProductRelease> releases, ReleaseVersion resolvedVersion, InstallComponent component)
    {
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
