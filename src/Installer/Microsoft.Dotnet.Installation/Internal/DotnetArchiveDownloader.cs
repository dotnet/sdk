// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.Dotnet.Installation;

namespace Microsoft.Dotnet.Installation.Internal;

/// <summary>
/// Handles downloading and parsing .NET release manifests to find the correct installer/archive for a given installation.
/// </summary>
internal class DotnetArchiveDownloader : IArchiveDownloader
{
    private const int MaxRetryCount = 3;
    private const int RetryDelayMilliseconds = 1000;

    private readonly HttpClient _httpClient;
    private readonly bool _shouldDisposeHttpClient;
    private ReleaseManifest _releaseManifest;
    private readonly DownloadCache _downloadCache;

    public DotnetArchiveDownloader()
        : this(new ReleaseManifest())
    {
    }

    public DotnetArchiveDownloader(ReleaseManifest releaseManifest, HttpClient? httpClient = null)
    {
        _releaseManifest = releaseManifest ?? throw new ArgumentNullException(nameof(releaseManifest));
        _downloadCache = new DownloadCache();
        if (httpClient == null)
        {
            _httpClient = CreateDefaultHttpClient();
            _shouldDisposeHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _shouldDisposeHttpClient = false;
        }
    }

    /// <summary>
    /// Creates an HttpClient with enhanced proxy support for enterprise environments.
    /// </summary>
    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler()
        {
            UseProxy = true,
            UseDefaultCredentials = true,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        // Set user-agent to identify dotnetup in telemetry, including version
        var informationalVersion = typeof(DotnetArchiveDownloader).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string userAgent = informationalVersion == null ? "dotnetup-dotnet-installer" : $"dotnetup-dotnet-installer/{informationalVersion}";

        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        return client;
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path with progress reporting.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    async Task DownloadArchiveAsync(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        // Create temp file path in same directory for atomic move when complete
        string tempPath = $"{destinationPath}.download";

        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

                // Content length for progress reporting
                long? totalBytes = null;

                // Make the actual download request
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength.HasValue)
                {
                    totalBytes = response.Content.Headers.ContentLength.Value;
                }

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[81920]; // 80KB buffer
                long bytesRead = 0;
                int read;

                var lastProgressReport = DateTime.MinValue;

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

                return;
            }
            catch (Exception)
            {
                if (attempt < MaxRetryCount)
                {
                    await Task.Delay(RetryDelayMilliseconds * attempt); // Linear backoff
                }
                else
                {
                    throw;
                }
            }
            finally
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

            }
        }

    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path (synchronous version).
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    void DownloadArchive(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        DownloadArchiveAsync(downloadUrl, destinationPath, progress).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Downloads the archive for the specified installation and verifies its hash.
    /// Checks the download cache first to avoid re-downloading.
    /// </summary>
    /// <param name="install">The .NET installation details</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download and verification were successful, false otherwise</returns>
    public void DownloadArchiveWithVerification(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        var targetFile = _releaseManifest.FindReleaseFile(installRequest, resolvedVersion);
        string? downloadUrl = targetFile?.Address.ToString();
        string? expectedHash = targetFile?.Hash.ToString();

        if (string.IsNullOrEmpty(expectedHash))
        {
            throw new ArgumentException($"{nameof(expectedHash)} cannot be null or empty");
        }
        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new ArgumentException($"{nameof(downloadUrl)} cannot be null or empty");
        }

        // Check the cache first
        string? cachedFilePath = _downloadCache.GetCachedFilePath(downloadUrl);
        if (cachedFilePath != null)
        {
            try
            {
                // Verify the cached file's hash
                VerifyFileHash(cachedFilePath, expectedHash);

                // Copy from cache to destination
                File.Copy(cachedFilePath, destinationPath, overwrite: true);

                // Report 100% progress immediately since we're using cache
                progress?.Report(new DownloadProgress(100, 100));
                return;
            }
            catch
            {
                // If cached file is corrupted, fall through to download
            }
        }

        // Download the file if not in cache or cache is invalid
        DownloadArchive(downloadUrl, destinationPath, progress);

        // Verify the downloaded file
        VerifyFileHash(destinationPath, expectedHash);

        // Add the verified file to the cache
        try
        {
            _downloadCache.AddToCache(downloadUrl, destinationPath);
        }
        catch
        {
            // Ignore errors adding to cache - it's not critical
        }
    }


    /// <summary>
    /// Computes the SHA512 hash of a file.
    /// </summary>
    /// <param name="filePath">Path to the file to hash</param>
    /// <returns>The hash as a lowercase hex string</returns>
    public static string ComputeFileHash(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        // TODO: Older runtime versions use a different SHA algorithm.
        // Eventually the manifest should indicate which algorithm to use.
        using var sha512 = SHA512.Create();
        byte[] hashBytes = sha512.ComputeHash(fileStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a downloaded file matches the expected hash.
    /// </summary>
    /// <param name="filePath">Path to the file to verify</param>
    /// <param name="expectedHash">Expected hash value</param>
    public static void VerifyFileHash(string filePath, string expectedHash)
    {
        if (string.IsNullOrEmpty(expectedHash))
        {
            throw new ArgumentException("Expected hash cannot be null or empty", nameof(expectedHash));
        }

        string actualHash = ComputeFileHash(filePath);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"File hash mismatch. Expected: {expectedHash}, Actual: {actualHash}");
        }
    }

    public void Dispose()
    {
        if (_shouldDisposeHttpClient)
        {
            _httpClient?.Dispose();
        }
    }
}
