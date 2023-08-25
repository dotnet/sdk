// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Formats.Tar;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

[Trait("Category", "SdkContent")]
public class ArtifactsSizeTest : SmokeTests
{
    private static readonly string BaselineFilePath = BaselineHelper.GetBaselineFilePath($"ArtifactsSizes/{Config.TargetRid}.txt");
    private static readonly Dictionary<string, long> BaselineFileContent = new Dictionary<string, long>();
    private const int SizeThresholdPercentage = 25;


    public ArtifactsSizeTest(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        if (File.Exists(BaselineFilePath))
        {
            string[] baselineFileContent = File.ReadAllLines(BaselineFilePath);
            foreach (string entry in baselineFileContent)
            {
                string[] splitEntry = entry.Split(':', StringSplitOptions.TrimEntries);
                BaselineFileContent[splitEntry[0]] = long.Parse(splitEntry[1]);
            }
        }
        else
        {
            Assert.False(true, $"Baseline file `{BaselineFilePath}' does not exist. Please create the baseline file then rerun the test.");
        }
    }

    [SkippableFact(new[] { Config.SourceBuiltArtifactsPathEnv, Config.SdkTarballPathEnv, Config.TargetRidEnv }, skipOnNullOrWhiteSpace: true)]
    public void CompareArtifactsToBaseline()
    {
        Assert.NotNull(Config.SourceBuiltArtifactsPath);
        Assert.NotNull(Config.SdkTarballPath);
        Assert.NotNull(Config.TargetRid);

        IEnumerable<TarEntry> artifactsTarEntries = Utilities.GetTarballContent(Config.SourceBuiltArtifactsPath)
                                                        .Where(entry => entry.EntryType == TarEntryType.RegularFile)
                                                        .Where(entry => !entry.Name.Contains("SourceBuildReferencePackages"));
        IEnumerable<TarEntry> sdkTarEntries = Utilities.GetTarballContent(Config.SdkTarballPath)
                                                        .Where(entry => entry.EntryType == TarEntryType.RegularFile);

        Dictionary<string, int> fileNameCountMap = new Dictionary<string, int>();
        (string FilePath, long Bytes)[] tarEntries = sdkTarEntries.Concat(artifactsTarEntries)
            .Select(entry =>
            {
                string result = ProcessEntryName(entry.Name, fileNameCountMap);
                return (FilePath: result, Bytes: entry.Length);
            })
            .OrderBy(entry => entry.FilePath)
            .ToArray();

        foreach (var entry in tarEntries)
        {
            if (!BaselineFileContent.TryGetValue(entry.FilePath, out long baselineBytes))
            {
                OutputHelper.LogWarningMessage($"{entry.FilePath} does not exist in baseline. Adding it to the baseline file");
            }
            else
            {
                CompareFileSizes(entry.FilePath, entry.Bytes, baselineBytes);
            }
        }

        try
        {
            string actualFilePath = Path.Combine(DotNetHelper.LogsDirectory, $"Updated_ArtifactsSizes_{Config.TargetRid}.txt");
            File.WriteAllLines(actualFilePath, tarEntries.Select(entry => $"{entry.FilePath}: {entry.Bytes}"));
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"An error occurred while copying the baselines file: {BaselineFilePath}", ex);
        }
    }

    private string ProcessEntryName(string originalName, Dictionary<string, int> fileNameCountMap)
    {
        string result = BaselineHelper.RemoveRids(originalName);
        result = BaselineHelper.RemoveVersions(result);

        string pattern = @"x\.y\.z";
        MatchCollection matches = Regex.Matches(result, pattern);

        if (matches.Count > 0)
        {
            int count = fileNameCountMap.TryGetValue(result, out int value) ? value : 0;
            fileNameCountMap[result] = count + 1;

            if (count > 0)
            {
                int lastIndex = matches[matches.Count - 1].Index;
                result = result.Substring(0, lastIndex) + $"x.y.z-{count}" + result.Substring(lastIndex + pattern.Length - 2);
            }
        }

        return result;
    }

    private void CompareFileSizes(string filePath, long fileSize, long baselineSize)
    {
        if (fileSize == 0 && baselineSize != 0)
        {
            OutputHelper.LogWarningMessage($"'{filePath}' is now 0 bytes. It was {baselineSize} bytes");
        }
        else if (fileSize != 0 && baselineSize == 0)
        {
            OutputHelper.LogWarningMessage($"'{filePath}' is no longer 0 bytes. It is now {fileSize} bytes");
        }
        else if (baselineSize != 0 && Math.Abs(((fileSize - baselineSize) / (double)baselineSize) * 100) >= SizeThresholdPercentage)
        {
            OutputHelper.LogWarningMessage($"'{filePath}' increased in size by more than {SizeThresholdPercentage}%. It was originally {baselineSize} bytes and is now {fileSize} bytes");
        }
    }
}
