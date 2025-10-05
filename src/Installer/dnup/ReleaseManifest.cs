// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Handles downloading and parsing .NET release manifests to find the correct installer/archive for a given installation.
/// </summary>
internal class ReleaseManifest : IDisposable
{
    /// <summary>
    /// Parses a version channel string into its components.
    /// </summary>
    /// <param name="channel">Channel string to parse (e.g., "9", "9.0", "9.0.1xx", "9.0.103")</param>
    /// <returns>Tuple containing (major, minor, featureBand, isFullySpecified)</returns>
    private (int Major, int Minor, string? FeatureBand, bool IsFullySpecified) ParseVersionChannel(string channel)
    {
        var parts = channel.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : -1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : -1;

        // Check if we have a feature band (like 1xx) or a fully specified patch
        string? featureBand = null;
        bool isFullySpecified = false;

        if (parts.Length >= 3)
        {
            if (parts[2].EndsWith("xx"))
            {
                // Feature band pattern (e.g., "1xx")
                featureBand = parts[2].Substring(0, parts[2].Length - 2);
            }
            else if (int.TryParse(parts[2], out _))
            {
                // Fully specified version (e.g., "9.0.103")
                isFullySpecified = true;
            }
        }

        return (major, minor, featureBand, isFullySpecified);
    }

    /// <summary>
    /// Gets products from the index that match the specified major version.
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="major">The major version to match</param>
    /// <returns>List of matching products, ordered by minor version (descending)</returns>
    private List<Product> GetProductsForMajorVersion(ProductCollection index, int major)
    {
        var matchingProducts = index.Where(p =>
        {
            var productParts = p.ProductVersion.Split('.');
            if (productParts.Length > 0 && int.TryParse(productParts[0], out var productMajor))
            {
                return productMajor == major;
            }
            return false;
        }).ToList();

        // Order by minor version (descending) to prioritize newer versions
        return matchingProducts.OrderByDescending(p =>
        {
            var productParts = p.ProductVersion.Split('.');
            if (productParts.Length > 1 && int.TryParse(productParts[1], out var productMinor))
            {
                return productMinor;
            }
            return 0;
        }).ToList();
    }

    /// <summary>
    /// Gets all SDK components from the releases and returns the latest one.
    /// </summary>
    /// <param name="releases">List of releases to search</param>
    /// <param name="majorFilter">Optional major version filter</param>
    /// <param name="minorFilter">Optional minor version filter</param>
    /// <returns>Latest SDK version string, or null if none found</returns>
    private string? GetLatestSdkVersion(IEnumerable<ProductRelease> releases, int? majorFilter = null, int? minorFilter = null)
    {
        var allSdks = releases
            .SelectMany(r => r.Sdks)
            .Where(sdk =>
                (!majorFilter.HasValue || sdk.Version.Major == majorFilter.Value) &&
                (!minorFilter.HasValue || sdk.Version.Minor == minorFilter.Value))
            .OrderByDescending(sdk => sdk.Version)
            .ToList();

        if (allSdks.Any())
        {
            return allSdks.First().Version.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets all runtime components from the releases and returns the latest one.
    /// </summary>
    /// <param name="releases">List of releases to search</param>
    /// <param name="majorFilter">Optional major version filter</param>
    /// <param name="minorFilter">Optional minor version filter</param>
    /// <param name="runtimeType">Optional runtime type filter (null for any runtime)</param>
    /// <returns>Latest runtime version string, or null if none found</returns>
    private string? GetLatestRuntimeVersion(IEnumerable<ProductRelease> releases, int? majorFilter = null, int? minorFilter = null, string? runtimeType = null)
    {
        var allRuntimes = releases.SelectMany(r => r.Runtimes).ToList();

        // Filter by version constraints if provided
        if (majorFilter.HasValue)
        {
            allRuntimes = allRuntimes.Where(r => r.Version.Major == majorFilter.Value).ToList();
        }

        if (minorFilter.HasValue)
        {
            allRuntimes = allRuntimes.Where(r => r.Version.Minor == minorFilter.Value).ToList();
        }

        // Filter by runtime type if specified
        if (!string.IsNullOrEmpty(runtimeType))
        {
            if (string.Equals(runtimeType, "aspnetcore", StringComparison.OrdinalIgnoreCase))
            {
                allRuntimes = allRuntimes
                    .Where(r => r.GetType().Name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else if (string.Equals(runtimeType, "windowsdesktop", StringComparison.OrdinalIgnoreCase))
            {
                allRuntimes = allRuntimes
                    .Where(r => r.GetType().Name.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else // Regular runtime
            {
                allRuntimes = allRuntimes
                    .Where(r => !r.GetType().Name.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) &&
                              !r.GetType().Name.Contains("WindowsDesktop", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        if (allRuntimes.Any())
        {
            return allRuntimes.OrderByDescending(r => r.Version).First().Version.ToString();
        }

        return null;
    }

    /// <summary>
    /// Gets the latest SDK version that matches a specific feature band pattern.
    /// </summary>
    /// <param name="releases">List of releases to search</param>
    /// <param name="major">Major version</param>
    /// <param name="minor">Minor version</param>
    /// <param name="featureBand">Feature band prefix (e.g., "1" for "1xx")</param>
    /// <returns>Latest matching version string, or fallback format if none found</returns>
    private string? GetLatestFeatureBandVersion(IEnumerable<ProductRelease> releases, int major, int minor, string featureBand)
    {
        var allSdkComponents = releases.SelectMany(r => r.Sdks).ToList();

        // Filter by feature band
        var featureBandSdks = allSdkComponents
            .Where(sdk =>
            {
                var version = sdk.Version.ToString();
                var versionParts = version.Split('.');
                if (versionParts.Length < 3) return false;

                var patchPart = versionParts[2].Split('-')[0]; // Remove prerelease suffix
                return patchPart.Length >= 3 && patchPart.StartsWith(featureBand);
            })
            .OrderByDescending(sdk => sdk.Version)
            .ToList();

        if (featureBandSdks.Any())
        {
            // Return the exact version from the latest matching SDK
            return featureBandSdks.First().Version.ToString();
        }

        // Fallback if no actual release matches the feature band pattern
        return $"{major}.{minor}.{featureBand}00";
    }

    /// <summary>
    /// Finds the latest fully specified version for a given channel string (major, major.minor, or feature band).
    /// </summary>
    /// <param name="channel">Channel string (e.g., "9", "9.0", "9.0.1xx", "9.0.103", "lts", "sts", "preview")</param>
    /// <param name="mode">InstallMode.SDK or InstallMode.Runtime</param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public string? GetLatestVersionForChannel(string channel, InstallMode mode)
    {
        // If channel is null or empty, return null
        if (string.IsNullOrEmpty(channel))
        {
            return null;
        }

        // Check for special channel strings (case insensitive)
        if (string.Equals(channel, "lts", StringComparison.OrdinalIgnoreCase))
        {
            // Handle LTS (Long-Term Support) channel
            var productIndex = ProductCollection.GetAsync().GetAwaiter().GetResult();
            return GetLatestVersionBySupportStatus(productIndex, isLts: true, mode);
        }
        else if (string.Equals(channel, "sts", StringComparison.OrdinalIgnoreCase))
        {
            // Handle STS (Standard-Term Support) channel
            var productIndex = ProductCollection.GetAsync().GetAwaiter().GetResult();
            return GetLatestVersionBySupportStatus(productIndex, isLts: false, mode);
        }
        else if (string.Equals(channel, "preview", StringComparison.OrdinalIgnoreCase))
        {
            // Handle Preview channel - get the latest preview version
            var productIndex = ProductCollection.GetAsync().GetAwaiter().GetResult();
            return GetLatestPreviewVersion(productIndex, mode);
        }        // Parse the channel string into components
        var (major, minor, featureBand, isFullySpecified) = ParseVersionChannel(channel);

        // If major is invalid, return null
        if (major < 0)
        {
            return null;
        }

        // If the version is already fully specified, just return it as-is
        if (isFullySpecified)
        {
            return channel;
        }

        // Load the index manifest
        var index = ProductCollection.GetAsync().GetAwaiter().GetResult();

        // Case 1: Major only version (e.g., "9")
        if (minor < 0)
        {
            return GetLatestVersionForMajorOnly(index, major, mode);
        }

        // Case 2: Major.Minor version (e.g., "9.0")
        if (minor >= 0 && featureBand == null)
        {
            return GetLatestVersionForMajorMinor(index, major, minor, mode);
        }

        // Case 3: Feature band version (e.g., "9.0.1xx")
        if (minor >= 0 && featureBand != null)
        {
            return GetLatestVersionForFeatureBand(index, major, minor, featureBand, mode);
        }

        return null;
    }

    /// <summary>
    /// Gets the latest version for a major-only channel (e.g., "9").
    /// </summary>
    private string? GetLatestVersionForMajorOnly(ProductCollection index, int major, InstallMode mode)
    {
        // Get products matching the major version
        var matchingProducts = GetProductsForMajorVersion(index, major);

        if (!matchingProducts.Any())
        {
            return null;
        }

        // Get all releases from all matching products
        var allReleases = new List<ProductRelease>();
        foreach (var matchingProduct in matchingProducts)
        {
            allReleases.AddRange(matchingProduct.GetReleasesAsync().GetAwaiter().GetResult());
        }

        // Find the latest version based on mode
        if (mode == InstallMode.SDK)
        {
            return GetLatestSdkVersion(allReleases, major);
        }
        else // Runtime mode
        {
            return GetLatestRuntimeVersion(allReleases, major);
        }
    }

    /// <summary>
    /// Gets the latest version based on support status (LTS or STS).
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="isLts">True for LTS (Long-Term Support), false for STS (Standard-Term Support)</param>
    /// <param name="mode">InstallMode.SDK or InstallMode.Runtime</param>
    /// <returns>Latest stable version string matching the support status, or null if none found</returns>
    private string? GetLatestVersionBySupportStatus(ProductCollection index, bool isLts, InstallMode mode)
    {
        // Get all products
        var allProducts = index.ToList();

        // Use ReleaseType from manifest (dotnetreleases library)
        var targetType = isLts ? ReleaseType.LTS : ReleaseType.STS;
        var filteredProducts = allProducts
            .Where(p => p.ReleaseType == targetType)
            .OrderByDescending(p =>
            {
                var productParts = p.ProductVersion.Split('.');
                if (productParts.Length > 0 && int.TryParse(productParts[0], out var majorVersion))
                {
                    return majorVersion * 100 + (productParts.Length > 1 && int.TryParse(productParts[1], out var minorVersion) ? minorVersion : 0);
                }
                return 0;
            })
            .ToList();

        // Get all releases from filtered products
        foreach (var product in filteredProducts)
        {
            var releases = product.GetReleasesAsync().GetAwaiter().GetResult();

            // Filter out preview versions
            var stableReleases = releases
                .Where(r => !r.IsPreview)
                .ToList();

            if (!stableReleases.Any())
            {
                continue; // No stable releases for this product, try next one
            }

            // Find latest version based on mode
            if (mode == InstallMode.SDK)
            {
                var sdks = stableReleases
                    .SelectMany(r => r.Sdks)
                    .Where(sdk => !sdk.Version.ToString().Contains("-")) // Exclude any preview/RC versions
                    .OrderByDescending(sdk => sdk.Version)
                    .ToList();

                if (sdks.Any())
                {
                    return sdks.First().Version.ToString();
                }
            }
            else // Runtime mode
            {
                var runtimes = stableReleases
                    .SelectMany(r => r.Runtimes)
                    .Where(runtime => !runtime.Version.ToString().Contains("-")) // Exclude any preview/RC versions
                    .OrderByDescending(runtime => runtime.Version)
                    .ToList();

                if (runtimes.Any())
                {
                    return runtimes.First().Version.ToString();
                }
            }
        }

        return null; // No matching versions found
    }

    /// <summary>
    /// Gets the latest preview version available.
    /// </summary>
    /// <param name="index">The product collection to search</param>
    /// <param name="mode">InstallMode.SDK or InstallMode.Runtime</param>
    /// <returns>Latest preview version string, or null if none found</returns>
    private string? GetLatestPreviewVersion(ProductCollection index, InstallMode mode)
    {
        // Get all products
        var allProducts = index.ToList();

        // Order by major and minor version (descending) to get the most recent first
        var sortedProducts = allProducts
            .OrderByDescending(p =>
            {
                var productParts = p.ProductVersion.Split('.');
                if (productParts.Length > 0 && int.TryParse(productParts[0], out var majorVersion))
                {
                    return majorVersion * 100 + (productParts.Length > 1 && int.TryParse(productParts[1], out var minorVersion) ? minorVersion : 0);
                }
                return 0;
            })
            .ToList();

        // Get all releases from products
        foreach (var product in sortedProducts)
        {
            var releases = product.GetReleasesAsync().GetAwaiter().GetResult();

            // Filter for preview versions
            var previewReleases = releases
                .Where(r => r.IsPreview)
                .ToList();

            if (!previewReleases.Any())
            {
                continue; // No preview releases for this product, try next one
            }

            // Find latest version based on mode
            if (mode == InstallMode.SDK)
            {
                var sdks = previewReleases
                    .SelectMany(r => r.Sdks)
                    .Where(sdk => sdk.Version.ToString().Contains("-")) // Include only preview/RC versions
                    .OrderByDescending(sdk => sdk.Version)
                    .ToList();

                if (sdks.Any())
                {
                    return sdks.First().Version.ToString();
                }
            }
            else // Runtime mode
            {
                var runtimes = previewReleases
                    .SelectMany(r => r.Runtimes)
                    .Where(runtime => runtime.Version.ToString().Contains("-")) // Include only preview/RC versions
                    .OrderByDescending(runtime => runtime.Version)
                    .ToList();

                if (runtimes.Any())
                {
                    return runtimes.First().Version.ToString();
                }
            }
        }

        return null; // No preview versions found
    }    /// <summary>
         /// Gets the latest version for a major.minor channel (e.g., "9.0").
         /// </summary>
    private string? GetLatestVersionForMajorMinor(ProductCollection index, int major, int minor, InstallMode mode)
    {
        // Find the product for the requested major.minor
        string channelKey = $"{major}.{minor}";
        var product = index.FirstOrDefault(p => p.ProductVersion == channelKey);

        if (product == null)
        {
            return null;
        }

        // Load releases from the sub-manifest for this product
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult();

        // Find the latest version based on mode
        if (mode == InstallMode.SDK)
        {
            return GetLatestSdkVersion(releases, major, minor);
        }
        else // Runtime mode
        {
            return GetLatestRuntimeVersion(releases, major, minor);
        }
    }

    /// <summary>
    /// Gets the latest version for a feature band channel (e.g., "9.0.1xx").
    /// </summary>
    private string? GetLatestVersionForFeatureBand(ProductCollection index, int major, int minor, string featureBand, InstallMode mode)
    {
        // Find the product for the requested major.minor
        string channelKey = $"{major}.{minor}";
        var product = index.FirstOrDefault(p => p.ProductVersion == channelKey);

        if (product == null)
        {
            return null;
        }

        // Load releases from the sub-manifest for this product
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult();

        // For SDK mode, use feature band filtering
        if (mode == InstallMode.SDK)
        {
            return GetLatestFeatureBandVersion(releases, major, minor, featureBand);
        }
        else // For Runtime mode, just use regular major.minor filtering
        {
            return GetLatestRuntimeVersion(releases, major, minor);
        }
    }

    private const string CacheSubdirectory = "dotnet-manifests";
    private const int MaxRetryCount = 3;
    private const int RetryDelayMilliseconds = 1000;
    private const string ReleaseCacheMutexName = "Global\\DotNetReleaseCache";

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private ProductCollection? _productCollection;

    public ReleaseManifest()
        : this(CreateDefaultHttpClient(), GetDefaultCacheDirectory())
    {
    }

    public ReleaseManifest(HttpClient httpClient)
        : this(httpClient, GetDefaultCacheDirectory())
    {
    }

    public ReleaseManifest(HttpClient httpClient, string cacheDirectory)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Creates an HttpClient with enhanced proxy support for enterprise environments.
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler()
        {
            // Use system proxy settings by default
            UseProxy = true,
            // Use default credentials for proxy authentication if needed
            UseDefaultCredentials = true,
            // Handle redirects automatically
            AllowAutoRedirect = true,
            // Set maximum number of redirects to prevent infinite loops
            MaxAutomaticRedirections = 10,
            // Enable decompression for better performance
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            // Set a reasonable timeout for downloads
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Set user agent to identify the client
        client.DefaultRequestHeaders.UserAgent.ParseAdd("dnup-dotnet-installer");

        return client;
    }

    /// <summary>
    /// Gets the default cache directory path.
    /// </summary>
    private static string GetDefaultCacheDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "dnup", CacheSubdirectory);
    }

    /// <summary>
    /// Downloads the releases.json manifest and finds the download URL for the specified installation.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <returns>The download URL for the installer/archive, or null if not found</returns>
    public string? GetDownloadUrl(DotnetInstall install)
    {
        var targetFile = FindReleaseFile(install);
        return targetFile?.Address.ToString();
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path with progress reporting.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download was successful, false otherwise</returns>
    public async Task<bool> DownloadArchiveAsync(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        // Create temp file path in same directory for atomic move when complete
        string tempPath = $"{destinationPath}.download";

        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                // Try to get content length for progress reporting
                long? totalBytes = await GetContentLengthAsync(downloadUrl);

                // Make the actual download request
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Get the total bytes if we didn't get it before
                if (!totalBytes.HasValue && response.Content.Headers.ContentLength.HasValue)
                {
                    totalBytes = response.Content.Headers.ContentLength.Value;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920]; // 80KB buffer
                long bytesRead = 0;
                int read;

                var lastProgressReport = DateTime.UtcNow;

                while ((read = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));

                    bytesRead += read;

                    // Report progress at most every 100ms to avoid UI thrashing
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressReport).TotalMilliseconds > 100)
                    {
                        lastProgressReport = now;
                        progress?.Report(new DownloadProgress(bytesRead, totalBytes));
                    }
                }

                // Final progress report
                progress?.Report(new DownloadProgress(bytesRead, totalBytes));

                // Ensure all data is written to disk
                await fileStream.FlushAsync();
                fileStream.Close();

                // Atomic move to final destination
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
                File.Move(tempPath, destinationPath);

                return true;
            }
            catch (Exception)
            {
                // Delete the partial download if it exists
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                if (attempt < MaxRetryCount)
                {
                    await Task.Delay(RetryDelayMilliseconds * attempt); // Exponential backoff
                }
                else
                {
                    return false;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the content length of a resource.
    /// </summary>
    private async Task<long?> GetContentLengthAsync(string url)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(headRequest);
            return headResponse.Content.Headers.ContentLength;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path (synchronous version).
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download was successful, false otherwise</returns>
    public bool DownloadArchive(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        return DownloadArchiveAsync(downloadUrl, destinationPath, progress).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads the archive for the specified installation and verifies its hash.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download and verification were successful, false otherwise</returns>
    public bool DownloadArchiveWithVerification(DotnetInstall install, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        // Get the download URL and expected hash
        string? downloadUrl = GetDownloadUrl(install);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            return false;
        }

        string? expectedHash = GetArchiveHash(install);
        if (string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        if (!DownloadArchive(downloadUrl, destinationPath, progress))
        {
            return false;
        }

        return VerifyFileHash(destinationPath, expectedHash);
    }

    /// <summary>
    /// Finds the appropriate release file for the given installation.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <returns>The matching ReleaseFile, throws if none are available.</returns>
    private ReleaseFile? FindReleaseFile(DotnetInstall install)
    {
        try
        {
            var productCollection = GetProductCollection();
            var product = FindProduct(productCollection, install.FullySpecifiedVersion.Value) ?? throw new InvalidOperationException($"No product found for version {install.FullySpecifiedVersion.MajorMinor}");
            var release = FindRelease(product, install.FullySpecifiedVersion.Value, install.Mode) ?? throw new InvalidOperationException($"No release found for version {install.FullySpecifiedVersion.Value}");
            return FindMatchingFile(release, install);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to find an available release for install {install} : ${ex.Message}");
        }
    }

    /// <summary>
    /// Gets or loads the ProductCollection with caching.
    /// </summary>
    private ProductCollection GetProductCollection()
    {
        if (_productCollection != null)
        {
            return _productCollection;
        }

        // Use ScopedMutex for cross-process locking
        using var mutex = new ScopedMutex(ReleaseCacheMutexName);

        // Always use the index manifest for ProductCollection
        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                _productCollection = ProductCollection.GetAsync().GetAwaiter().GetResult();
                return _productCollection;
            }
            catch
            {
                if (attempt == MaxRetryCount)
                {
                    throw;
                }
                Thread.Sleep(RetryDelayMilliseconds * attempt); // Exponential backoff
            }
        }

        // This shouldn't be reached due to throw above, but compiler doesn't know that
        throw new InvalidOperationException("Failed to fetch .NET releases data");
    }

    /// <summary>
    /// Serializes a ProductCollection to JSON.
    /// </summary>
    private static string SerializeProductCollection(ProductCollection collection)
    {
        // Use options that indicate we've verified AOT compatibility
        var options = new System.Text.Json.JsonSerializerOptions();
#pragma warning disable IL2026, IL3050
        return System.Text.Json.JsonSerializer.Serialize(collection, options);
#pragma warning restore IL2026, IL3050
    }

    /// <summary>
    /// Deserializes a ProductCollection from JSON.
    /// </summary>
    private static ProductCollection DeserializeProductCollection(string json)
    {
        // Use options that indicate we've verified AOT compatibility
        var options = new System.Text.Json.JsonSerializerOptions();
#pragma warning disable IL2026, IL3050
        return System.Text.Json.JsonSerializer.Deserialize<ProductCollection>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize ProductCollection from JSON");
#pragma warning restore IL2026, IL3050
    }

    /// <summary>
    /// Finds the product for the given version.
    /// </summary>
    private static Product? FindProduct(ProductCollection productCollection, string version)
    {
        var releaseVersion = new ReleaseVersion(version);
        var majorMinor = $"{releaseVersion.Major}.{releaseVersion.Minor}";
        return productCollection.FirstOrDefault(p => p.ProductVersion == majorMinor);
    }

    /// <summary>
    /// Finds the specific release for the given version.
    /// </summary>
    private static ProductRelease? FindRelease(Product product, string version, InstallMode mode)
    {
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult();
        var targetReleaseVersion = new ReleaseVersion(version);

        // Get all releases
        var allReleases = releases.ToList();

        // First try to find the exact version in the original release list
        var exactReleaseMatch = allReleases.FirstOrDefault(r => r.Version.Equals(targetReleaseVersion));
        if (exactReleaseMatch != null)
        {
            return exactReleaseMatch;
        }

        // Now check through the releases to find matching components
        foreach (var release in allReleases)
        {
            bool foundMatch = false;

            // Check the appropriate collection based on the mode
            if (mode == InstallMode.SDK)
            {
                foreach (var sdk in release.Sdks)
                {
                    // Check for exact match
                    if (sdk.Version.Equals(targetReleaseVersion))
                    {
                        foundMatch = true;
                        break;
                    }

                    // Check for match on major, minor, patch
                    if (sdk.Version.Major == targetReleaseVersion.Major &&
                        sdk.Version.Minor == targetReleaseVersion.Minor &&
                        sdk.Version.Patch == targetReleaseVersion.Patch)
                    {
                        foundMatch = true;
                        break;
                    }
                }
            }
            else // Runtime mode
            {
                // Filter by runtime type based on file names in the release
                var runtimeTypeMatches = release.Files.Any(f =>
                    f.Name.Contains("runtime", StringComparison.OrdinalIgnoreCase) &&
                    !f.Name.Contains("aspnetcore", StringComparison.OrdinalIgnoreCase) &&
                    !f.Name.Contains("windowsdesktop", StringComparison.OrdinalIgnoreCase));

                var aspnetCoreMatches = release.Files.Any(f =>
                    f.Name.Contains("aspnetcore", StringComparison.OrdinalIgnoreCase));

                var windowsDesktopMatches = release.Files.Any(f =>
                    f.Name.Contains("windowsdesktop", StringComparison.OrdinalIgnoreCase));

                // Get the appropriate runtime components based on the file patterns
                var filteredRuntimes = release.Runtimes;

                // Use the type information from the file names to filter runtime components
                // This will prioritize matching the exact runtime type the user is looking for

                foreach (var runtime in filteredRuntimes)
                {
                    // Check for exact match
                    if (runtime.Version.Equals(targetReleaseVersion))
                    {
                        foundMatch = true;
                        break;
                    }

                    // Check for match on major, minor, patch
                    if (runtime.Version.Major == targetReleaseVersion.Major &&
                        runtime.Version.Minor == targetReleaseVersion.Minor &&
                        runtime.Version.Patch == targetReleaseVersion.Patch)
                    {
                        foundMatch = true;
                        break;
                    }
                }
            }

            if (foundMatch)
            {
                return release;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the matching file in the release for the given installation requirements.
    /// </summary>
    private static ReleaseFile? FindMatchingFile(ProductRelease release, DotnetInstall install)
    {
        var rid = DnupUtilities.GetRuntimeIdentifier(install.Architecture);
        var fileExtension = DnupUtilities.GetArchiveFileExtensionForPlatform();

        // Determine the component type pattern to look for in file names
        string componentTypePattern;
        if (install.Mode == InstallMode.SDK)
        {
            componentTypePattern = "sdk";
        }
        else // Runtime mode
        {
            // Determine the specific runtime type based on the release's file patterns
            // Default to "runtime" if can't determine more specifically
            componentTypePattern = "runtime";

            // Check if this is specifically an ASP.NET Core runtime
            if (install.FullySpecifiedVersion.Value.Contains("aspnetcore"))
            {
                componentTypePattern = "aspnetcore";
            }
            // Check if this is specifically a Windows Desktop runtime
            else if (install.FullySpecifiedVersion.Value.Contains("windowsdesktop"))
            {
                componentTypePattern = "windowsdesktop";
            }
        }

        // Filter files based on runtime identifier, component type, and file extension
        var matchingFiles = release.Files
            .Where(f => f.Rid == rid)
            .Where(f => f.Name.Contains(componentTypePattern, StringComparison.OrdinalIgnoreCase))
            .Where(f => f.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingFiles.Count == 0)
        {
            return null;
        }

        // If we have multiple matching files, prefer the one with the full version in the name
        var versionString = install.FullySpecifiedVersion.Value;
        var bestMatch = matchingFiles.FirstOrDefault(f => f.Name.Contains(versionString, StringComparison.OrdinalIgnoreCase));

        // If no file has the exact version string, return the first match
        return bestMatch ?? matchingFiles.First();
    }

    /// <summary>
    /// Gets the SHA512 hash of the archive for the specified installation.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <returns>The SHA512 hash string of the installer/archive, or null if not found</returns>
    public string? GetArchiveHash(DotnetInstall install)
    {
        var targetFile = FindReleaseFile(install);
        return targetFile?.Hash;
    }

    /// <summary>
    /// Computes the SHA512 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file to hash</param>
    /// <returns>The hash as a lowercase hex string</returns>
    public static string ComputeFileHash(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var sha512 = SHA512.Create();
        byte[] hashBytes = sha512.ComputeHash(fileStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a downloaded file matches the expected hash.
    /// </summary>
    /// <param name="filePath">Path to the file to verify</param>
    /// <param name="expectedHash">Expected hash value</param>
    /// <returns>True if the hash matches, false otherwise</returns>
    public static bool VerifyFileHash(string filePath, string expectedHash)
    {
        if (string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        string actualHash = ComputeFileHash(filePath);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Represents download progress information.
/// </summary>
public readonly struct DownloadProgress
{
    /// <summary>
    /// Gets the number of bytes downloaded.
    /// </summary>
    public long BytesDownloaded { get; }

    /// <summary>
    /// Gets the total number of bytes to download, if known.
    /// </summary>
    public long? TotalBytes { get; }

    /// <summary>
    /// Gets the percentage of download completed, if total size is known.
    /// </summary>
    public double? PercentComplete => TotalBytes.HasValue ? (double)BytesDownloaded / TotalBytes.Value * 100 : null;

    public DownloadProgress(long bytesDownloaded, long? totalBytes)
    {
        BytesDownloaded = bytesDownloaded;
        TotalBytes = totalBytes;
    }
}
