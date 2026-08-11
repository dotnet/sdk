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

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (matcher.Match(entry.Name).HasMatches)
            {
                string outputPath = Path.Join(outputDir, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                using FileStream outputFileStream = File.Create(outputPath);
                entry.DataStream!.CopyTo(outputFileStream);
                return;
            }
        }

        throw new FileNotFoundException($"Could not find {targetFilePath} in {tarballPath}.");
    }

    public static IEnumerable<string> GetTarballContentNames(string tarballPath)
    {
        using FileStream fileStream = File.OpenRead(tarballPath);
        using GZipStream decompressorStream = new(fileStream, CompressionMode.Decompress);
        using TarReader reader = new(decompressorStream);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            yield return entry.Name;
        }
    }

    public static void ExtractNupkg(string package, string outputDir)
    {
        ZipFile.ExtractToDirectory(package, outputDir);
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

    public static void LogWarningMessage(this ITestOutputHelper outputHelper, string message)
    {
        string prefix = "##vso[task.logissue type=warning;]";

        outputHelper.WriteLine($"{Environment.NewLine}{prefix}{message}.{Environment.NewLine}");
        outputHelper.WriteLine("##vso[task.complete result=SucceededWithIssues;]");
    }

    public static string GetFile(string path, string pattern)
    {
        string[] files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
        Assert.False(files.Length > 1, $"Found multiple files matching the pattern {pattern}: {Environment.NewLine}{string.Join(Environment.NewLine, files)}");
        Assert.False(files.Length == 0, $"Did not find any files matching the pattern {pattern}");
        return files[0];
    }
}
