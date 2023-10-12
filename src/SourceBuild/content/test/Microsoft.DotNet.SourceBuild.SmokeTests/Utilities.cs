// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public static class Utilities
{
    /// <summary>
    /// Returns whether the given file path is excluded by the given exclusions using glob file matching.
    /// </summary>
    public static bool IsFileExcluded(string filePath, IEnumerable<string> exclusions) =>
        GetMatchingFileExclusions(filePath, exclusions, exclusion => exclusion).Any();

    public static IEnumerable<T> GetMatchingFileExclusions<T>(string filePath, IEnumerable<T> exclusions, Func<T, string> getExclusionExpression) =>
        exclusions.Where(exclusion => FileSystemName.MatchesSimpleExpression(getExclusionExpression(exclusion), filePath));

    /// <summary>
    /// Parses a common file format in the test suite for listing file exclusions.
    /// </summary>
    /// <param name="exclusionsFileName">Name of the exclusions file.</param>
    /// <param name="prefix">When specified, filters the exclusions to those that begin with the prefix value.</param>
    public static IEnumerable<string> ParseExclusionsFile(string exclusionsFileName, string? prefix = null)
    {
        string exclusionsFilePath = Path.Combine(BaselineHelper.GetAssetsDirectory(), exclusionsFileName);
        int prefixSkip = prefix?.Length + 1 ?? 0;
        return File.ReadAllLines(exclusionsFilePath)
            // process only specific exclusions if a prefix is provided
            .Where(line => prefix is null || line.StartsWith(prefix + ","))
            .Select(line =>
            {
                // Ignore comments
                var index = line.IndexOf('#');
                return index >= 0 ? line[prefixSkip..index].TrimEnd() : line[prefixSkip..];
            })
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();
    }
    
    public static void ExtractTarball(string tarballPath, string outputDir, ITestOutputHelper outputHelper)
    {
        // TarFile doesn't properly handle hard links (https://github.com/dotnet/runtime/pull/85378#discussion_r1221817490),
        // use 'tar' instead.
        ExecuteHelper.ExecuteProcessValidateExitCode("tar", $"xzf {tarballPath} -C {outputDir}", outputHelper);
    }

    public static void ExtractTarball(string tarballPath, string outputDir, string targetFilePath)
    {
        Matcher matcher = new();
        matcher.AddInclude(targetFilePath);

        using FileStream fileStream = File.OpenRead(tarballPath);
        using GZipStream decompressorStream = new(fileStream, CompressionMode.Decompress);
        using TarReader reader = new(decompressorStream);

        TarEntry entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (matcher.Match(entry.Name).HasMatches)
            {
                string outputPath = Path.Join(outputDir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                using FileStream outputFileStream = File.Create(outputPath);
                entry.DataStream.CopyTo(outputFileStream);
                break;
            }
        }
    }

    public static IEnumerable<string> GetTarballContentNames(string tarballPath)
    {
        using FileStream fileStream = File.OpenRead(tarballPath);
        using GZipStream decompressorStream = new(fileStream, CompressionMode.Decompress);
        using TarReader reader = new(decompressorStream);

        TarEntry entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            yield return entry.Name;
        }
    }

    public static void ExtractNupkg(string package, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        using ZipArchive zip = ZipFile.OpenRead(package);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string outputPath = Path.Combine(outputDir, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            entry.ExtractToFile(outputPath);
        }
    }

    public static async Task RetryAsync(Func<Task> executor, ITestOutputHelper outputHelper)
    {
        await Utilities.RetryAsync(
            async () =>
            {
                try
                {
                    await executor();
                    return null;
                }
                catch (Exception e)
                {
                    return e;
                }
            },
            outputHelper);
    }

    private static async Task RetryAsync(Func<Task<Exception?>> executor, ITestOutputHelper outputHelper)
    {
        const int maxRetries = 5;
        const int waitFactor = 5;

        int retryCount = 0;

        Exception? exception = await executor();
        while (exception != null)
        {
            retryCount++;
            if (retryCount >= maxRetries)
            {
                throw new InvalidOperationException($"Failed after {retryCount} retries.", exception);
            }

            int waitTime = Convert.ToInt32(Math.Pow(waitFactor, retryCount - 1));
            if (outputHelper != null)
            {
                outputHelper.WriteLine($"Retry {retryCount}/{maxRetries}, retrying in {waitTime} seconds...");
            }

            Thread.Sleep(TimeSpan.FromSeconds(waitTime));
            exception = await executor();
        }
    }

    public static string GetFile(string path, string pattern)
    {
        string[] files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
        Assert.False(files.Length > 1, $"Found multiple files matching the pattern {pattern}: {Environment.NewLine}{string.Join(Environment.NewLine, files)}");
        Assert.False(files.Length == 0, $"Did not find any files matching the pattern {pattern}");
        return files[0];
    }
}
