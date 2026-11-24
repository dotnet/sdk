// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DownloadCacheTests
{
    [TestMethod]
    public void AddToCache_HardLinksArchiveWhenSupported()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string source = Path.Combine(testEnv.TempRoot, "archive.tar.gz");
        string cacheDirectory = Path.Combine(testEnv.TempRoot, "cache");
        File.WriteAllText(source, "original");
        var cache = new DownloadCache(cacheDirectory);

        cache.AddToCache("https://example.test/archive.tar.gz", source, preferHardLink: true);
        string cachedPath = cache.GetCachedFilePath("https://example.test/archive.tar.gz")!;
        File.WriteAllText(source, "modified");

        File.ReadAllText(cachedPath).Should().Be("modified");
    }

    [TestMethod]
    public void AddToCache_CopiesWhenHardLinkIsUnavailable()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string source = Path.Combine(testEnv.TempRoot, "archive.tar.gz");
        string cacheDirectory = Path.Combine(testEnv.TempRoot, "cache");
        File.WriteAllText(source, "original");
        var cache = new DownloadCache(cacheDirectory, (_, _) => false);

        cache.AddToCache("https://example.test/archive.tar.gz", source, preferHardLink: true);
        string cachedPath = cache.GetCachedFilePath("https://example.test/archive.tar.gz")!;
        File.WriteAllText(source, "modified");

        File.ReadAllText(cachedPath).Should().Be("original");
    }

    [TestMethod]
    public void AddToCache_CachedHardLinkSurvivesSourceDeletion()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string source = Path.Combine(testEnv.TempRoot, "archive.tar.gz");
        string cacheDirectory = Path.Combine(testEnv.TempRoot, "cache");
        File.WriteAllText(source, "archive");
        var cache = new DownloadCache(cacheDirectory);

        cache.AddToCache("https://example.test/archive.tar.gz", source, preferHardLink: true);
        string cachedPath = cache.GetCachedFilePath("https://example.test/archive.tar.gz")!;
        File.Delete(source);

        File.ReadAllText(cachedPath).Should().Be("archive");
    }

    [TestMethod]
    public void MaterializeFile_DeletingScratchLinkPreservesCacheEntry()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string source = Path.Combine(testEnv.TempRoot, "archive.tar.gz");
        string scratch = Path.Combine(testEnv.TempRoot, "scratch", "archive.tar.gz");
        string cacheDirectory = Path.Combine(testEnv.TempRoot, "cache");
        Directory.CreateDirectory(Path.GetDirectoryName(scratch)!);
        File.WriteAllText(source, "archive");
        var cache = new DownloadCache(cacheDirectory);
        const string url = "https://example.test/archive.tar.gz";
        cache.AddToCache(url, source, preferHardLink: true);
        string cachedPath = cache.GetCachedFilePath(url)!;

        cache.MaterializeFile(cachedPath, scratch, preferHardLink: true);
        File.Delete(scratch);

        File.ReadAllText(cachedPath).Should().Be("archive");
    }

    [TestMethod]
    public void AddToCache_DoesNotReplaceCollidingFileName()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string firstSource = Path.Combine(testEnv.TempRoot, "first");
        string secondSource = Path.Combine(testEnv.TempRoot, "second");
        string cacheDirectory = Path.Combine(testEnv.TempRoot, "cache");
        File.WriteAllText(firstSource, "first");
        File.WriteAllText(secondSource, "second");
        var cache = new DownloadCache(cacheDirectory);
        const string firstUrl = "https://first.example.test/archive.tar.gz";
        const string secondUrl = "https://second.example.test/archive.tar.gz";

        cache.AddToCache(firstUrl, firstSource, preferHardLink: true);
        cache.AddToCache(secondUrl, secondSource, preferHardLink: true);

        File.ReadAllText(cache.GetCachedFilePath(firstUrl)!).Should().Be("first");
        cache.GetCachedFilePath(secondUrl).Should().BeNull();
    }

    [TestMethod]
    public void AddToCache_ConcurrentSameUrlLeavesCompleteEntry()
    {
        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();
        string source = Path.Combine(testEnv.TempRoot, "archive.tar.gz");
        string cacheDirectory = Path.Combine(testEnv.TempRoot, "cache");
        File.WriteAllText(source, new string('a', 4096));
        var cache = new DownloadCache(cacheDirectory);
        const string url = "https://example.test/archive.tar.gz";

        Parallel.For(0, 8, _ => cache.AddToCache(url, source, preferHardLink: true));

        File.ReadAllText(cache.GetCachedFilePath(url)!).Should().Be(new string('a', 4096));
        Directory.GetFiles(cacheDirectory, "*.tmp").Should().BeEmpty();
    }
}
