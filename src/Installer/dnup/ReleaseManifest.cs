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
    /// Finds the latest fully specified version for a given channel string (major, major.minor, or feature band).
    /// </summary>
    /// <param name="channel">Channel string (e.g., "9", "9.0", "9.0.1xx")</param>
    /// <param name="mode">InstallMode.SDK or InstallMode.Runtime</param>
    /// <returns>Latest fully specified version string, or null if not found</returns>
    public string? GetLatestVersionForChannel(string channel, InstallMode mode)
    {
        // Parse channel
        var parts = channel.Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : -1;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : -1;
        string? featureBandPattern = null;
        if (parts.Length == 3 && parts[2].EndsWith("xx"))
        {
            featureBandPattern = parts[2].Substring(0, parts[2].Length - 2); // e.g., "1" from "1xx"
        }

        // Load the index manifest
        var index = ProductCollection.GetAsync().GetAwaiter().GetResult();

        // For major-only channels like "9", we need to find all products with that major version
        if (minor == -1)
        {
            // Get all products with matching major version
            var matchingProducts = index.Where(p =>
            {
                // Split the product version into parts
                var productParts = p.ProductVersion.Split('.');
                if (productParts.Length > 0 && int.TryParse(productParts[0], out var productMajor))
                {
                    return productMajor == major;
                }
                return false;
            }).ToList();

            // For each matching product, get releases and filter
            var allReleases = new List<(ProductRelease Release, Product Product)>();

            foreach (var matchingProduct in matchingProducts)
            {
                var productReleases = matchingProduct.GetReleasesAsync().GetAwaiter().GetResult();

                // Filter by mode (SDK or Runtime)
                var filteredForProduct = productReleases.Where(r =>
                    r.Files.Any(f => mode == InstallMode.SDK ?
                        f.Name.Contains("sdk", StringComparison.OrdinalIgnoreCase) :
                        f.Name.Contains("runtime", StringComparison.OrdinalIgnoreCase))
                ).ToList();

                foreach (var release in filteredForProduct)
                {
                    allReleases.Add((release, matchingProduct));
                }
            }

            // Find the latest release across all products
            var latestAcrossProducts = allReleases.OrderByDescending(r => r.Release.Version).FirstOrDefault();

            if (latestAcrossProducts.Release != null)
            {
                return latestAcrossProducts.Release.Version.ToString();
            }

            return null;
        }

        // Find the product for the requested major.minor
        string channelKey = $"{major}.{minor}";
        var product = index.FirstOrDefault(p => p.ProductVersion == channelKey);
        if (product == null)
        {
            return null;
        }

        // Load releases from the sub-manifest for this product
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult();

        // Filter by mode (SDK or Runtime)
        var filtered = releases.Where(r =>
            r.Files.Any(f => mode == InstallMode.SDK ?
                f.Name.Contains("sdk", StringComparison.OrdinalIgnoreCase) :
                f.Name.Contains("runtime", StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // If feature band pattern is specified, handle it specially for SDK
        if (featureBandPattern != null && mode == InstallMode.SDK)
        {
            if (int.TryParse(featureBandPattern, out var bandNum))
            {
                // For feature bands, we need to construct the version manually
                // Since SDK feature bands are represented differently than runtime versions,
                // we return a special format for feature bands
                if (filtered.Any())
                {
                    // Return the feature band version pattern
                    return $"{major}.{minor}.{featureBandPattern}00";
                }
            }
        }

        var latest = filtered.OrderByDescending(r => r.Version).FirstOrDefault();
        if (latest != null)
        {
            return latest.Version.ToString();
        }

        return null;
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
            var product = FindProduct(productCollection, install.FullySpecifiedVersion.Value);
            if (product == null) return null;

            var release = FindRelease(product, install.FullySpecifiedVersion.Value);
            if (release == null) return null;

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
    private static ProductRelease? FindRelease(Product product, string version)
    {
        var releases = product.GetReleasesAsync().GetAwaiter().GetResult();
        var targetReleaseVersion = new ReleaseVersion(version);
        return releases.FirstOrDefault(r => r.Version.Equals(targetReleaseVersion));
    }

    /// <summary>
    /// Finds the matching file in the release for the given installation requirements.
    /// </summary>
    private static ReleaseFile? FindMatchingFile(ProductRelease release, DotnetInstall install)
    {
        var rid = DnupUtilities.GetRuntimeIdentifier(install.Architecture);
        var fileExtension = DnupUtilities.GetFileExtensionForPlatform();
        var componentType = install.Mode == InstallMode.SDK ? "sdk" : "runtime";

        return release.Files
            .Where(f => f.Rid == rid)
            .Where(f => f.Name.Contains(componentType, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(f => f.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase));
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
