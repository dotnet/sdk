// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Tests for DotnetArchiveDownloader, focusing on hash verification and HTTP client configuration.
/// </summary>
public class DotnetArchiveDownloaderTests
{
    private readonly ITestOutputHelper _log;

    public DotnetArchiveDownloaderTests(ITestOutputHelper log)
    {
        _log = log;
    }

    /// <summary>
    /// Regression test: the default HttpClient must NOT set AutomaticDecompression.
    /// When AutomaticDecompression includes GZip, HttpClient adds Accept-Encoding: gzip
    /// and transparently strips the gzip layer if the CDN returns Content-Encoding: gzip.
    /// This causes .tar.gz files to be saved as raw .tar, producing a hash that does not
    /// match the manifest's expected hash for the .tar.gz file.
    /// </summary>
    [Fact]
    public void DefaultHttpClient_DoesNotSetAutomaticDecompression()
    {
        // The default constructor creates its own HttpClient via CreateDefaultHttpClient().
        // Verify that the handler does NOT have AutomaticDecompression set.
        using var downloader = new DotnetArchiveDownloader();

        // Use reflection to access the private _httpClient field
        var httpClientField = typeof(DotnetArchiveDownloader)
            .GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        httpClientField.Should().NotBeNull("should have _httpClient field");

        var httpClient = httpClientField!.GetValue(downloader) as HttpClient;
        httpClient.Should().NotBeNull();

        // Check that the request does not include Accept-Encoding: gzip or deflate.
        // The presence of these headers would indicate AutomaticDecompression is active.
        var acceptEncoding = httpClient!.DefaultRequestHeaders.Contains("Accept-Encoding")
            ? string.Join(",", httpClient.DefaultRequestHeaders.GetValues("Accept-Encoding"))
            : null;

        _log.WriteLine($"Accept-Encoding header: {acceptEncoding ?? "(not set)"}");

        // AutomaticDecompression causes the handler to add Accept-Encoding headers automatically
        // at request time, not on DefaultRequestHeaders. But we can verify through the handler.
        // Access the handler via reflection on HttpMessageInvoker (base class of HttpClient).
        var handlerField = typeof(HttpMessageInvoker)
            .GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance);

        if (handlerField != null)
        {
            var handler = handlerField.GetValue(httpClient);
            // Walk through potential wrapper handlers to find the HttpClientHandler
            var innerHandler = handler;
            while (innerHandler != null && innerHandler is not HttpClientHandler)
            {
                var innerField = innerHandler.GetType()
                    .GetProperty("InnerHandler", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                innerHandler = innerField?.GetValue(innerHandler);
            }

            if (innerHandler is HttpClientHandler clientHandler)
            {
                _log.WriteLine($"AutomaticDecompression: {clientHandler.AutomaticDecompression}");
                clientHandler.AutomaticDecompression.Should().Be(DecompressionMethods.None,
                    "AutomaticDecompression must be None to prevent corrupting .tar.gz downloads");
            }
            else
            {
                _log.WriteLine("Could not locate HttpClientHandler via reflection - skipping handler check");
            }
        }
    }

    [Fact]
    public void ComputeFileHash_ProducesCorrectSha512()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var filePath = Path.Combine(testEnv.TempRoot, "test.bin");

        var content = "Hello, dotnetup!"u8.ToArray();
        File.WriteAllBytes(filePath, content);

        // Compute expected hash
        using var sha512 = SHA512.Create();
        var expectedBytes = sha512.ComputeHash(content);
        var expectedHash = BitConverter.ToString(expectedBytes).Replace("-", "").ToLowerInvariant();

        var actualHash = DotnetArchiveDownloader.ComputeFileHash(filePath);

        _log.WriteLine($"Expected: {expectedHash}");
        _log.WriteLine($"Actual:   {actualHash}");

        actualHash.Should().Be(expectedHash);
    }

    /// <summary>
    /// Regression test: the hash must be computed on the .tar.gz bytes (the file on disk),
    /// not on decompressed content. This verifies ComputeFileHash reads raw file bytes.
    /// </summary>
    [Fact]
    public void ComputeFileHash_HashesRawGzipBytes_NotDecompressedContent()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        // Create a .tar.gz file with known content
        var tarGzPath = Path.Combine(testEnv.TempRoot, "test.tar.gz");
        var innerContent = "inner content for gzip test"u8.ToArray();

        using (var fs = File.Create(tarGzPath))
        using (var gzip = new GZipStream(fs, CompressionLevel.Optimal))
        {
            gzip.Write(innerContent, 0, innerContent.Length);
        }

        // Compute hash of the raw .tar.gz file (what the manifest expects)
        var tarGzBytes = File.ReadAllBytes(tarGzPath);
        using var sha512 = SHA512.Create();
        var expectedHash = BitConverter.ToString(sha512.ComputeHash(tarGzBytes)).Replace("-", "").ToLowerInvariant();

        // ComputeFileHash should produce the same hash (raw bytes, NOT decompressed)
        var actualHash = DotnetArchiveDownloader.ComputeFileHash(tarGzPath);

        _log.WriteLine($"tar.gz size: {tarGzBytes.Length} bytes");
        _log.WriteLine($"Expected (raw tar.gz hash): {expectedHash}");
        _log.WriteLine($"Actual:                     {actualHash}");

        actualHash.Should().Be(expectedHash,
            "hash must be computed on the raw .tar.gz bytes, not decompressed content");

        // Also verify it does NOT match the hash of the decompressed content
        var decompressedHash = BitConverter.ToString(sha512.ComputeHash(innerContent)).Replace("-", "").ToLowerInvariant();
        actualHash.Should().NotBe(decompressedHash,
            "hash should NOT match decompressed content hash");
    }

    [Fact]
    public void VerifyFileHash_ThrowsOnMismatch()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var filePath = Path.Combine(testEnv.TempRoot, "test.bin");
        File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });

        var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000" +
                        "0000000000000000000000000000000000000000000000000000000000000000";

        var ex = Assert.Throws<DotnetInstallException>(() => DotnetArchiveDownloader.VerifyFileHash(filePath, wrongHash));
        ex.Message.Should().Contain("File hash mismatch");
        _log.WriteLine($"Exception: {ex.Message}");
    }

    [Fact]
    public void VerifyFileHash_PassesOnCorrectHash()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var filePath = Path.Combine(testEnv.TempRoot, "test.bin");
        var content = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(filePath, content);

        var correctHash = DotnetArchiveDownloader.ComputeFileHash(filePath);

        // Should not throw
        DotnetArchiveDownloader.VerifyFileHash(filePath, correctHash);
    }

    [Fact]
    public void VerifyFileHash_ThrowsOnEmptyExpectedHash()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        var filePath = Path.Combine(testEnv.TempRoot, "test.bin");
        File.WriteAllBytes(filePath, new byte[] { 1 });

        Assert.Throws<ArgumentException>(() => DotnetArchiveDownloader.VerifyFileHash(filePath, ""));
        Assert.Throws<ArgumentException>(() => DotnetArchiveDownloader.VerifyFileHash(filePath, null!));
    }
}
