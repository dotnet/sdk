// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
internal class DotnetArchiveDownloader(HttpClient httpClient) : IDisposable
{
    private const int MaxRetryCount = 3;
    private const int RetryDelayMilliseconds = 1000;

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private ProductCollection? _productCollection;
    private ReleaseManifest _releaseManifest = new();

    public DotnetArchiveDownloader()
        : this(CreateDefaultHttpClient())
    {
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

        // Set user-agent to identify dnup in telemetry
        client.DefaultRequestHeaders.UserAgent.ParseAdd("dnup-dotnet-installer");

        return client;
    }

    /// <summary>
    /// Downloads the archive from the specified URL to the destination path with progress reporting.
    /// </summary>
    /// <param name="downloadUrl">The URL to download from</param>
    /// <param name="destinationPath">The local path to save the downloaded file</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>True if download was successful, false otherwise</returns>
    protected async Task<bool> DownloadArchiveAsync(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
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
    protected bool DownloadArchive(string downloadUrl, string destinationPath, IProgress<DownloadProgress>? progress = null)
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
    public bool DownloadArchiveWithVerification(DotnetInstallRequest installRequest, ReleaseVersion resolvedVersion, string destinationPath, IProgress<DownloadProgress>? progress = null)
    {
        var targetFile = _releaseManifest.FindReleaseFile(installRequest, resolvedVersion);
        string? downloadUrl = targetFile?.Address.ToString();
        string? expectedHash = targetFile?.Hash.ToString();

        if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(downloadUrl))
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
