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

    [SkippableFact(new[] { Config.SourceBuiltArtifactsPathEnv, Config.SdkTarballPathEnv, Config.TargetRidEnv }, skipOnNullOrWhiteSpaceEnv: true)]
    public void CompareArtifactsToBaseline()
    {
        Assert.NotNull(Config.SourceBuiltArtifactsPath);
        Assert.NotNull(Config.SdkTarballPath);
        Assert.NotNull(Config.TargetRid);

        string tempTarballDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempTarballDir);

        Utilities.ExtractTarball(Config.SdkTarballPath, tempTarballDir, OutputHelper);
        Utilities.ExtractTarball(Config.SourceBuiltArtifactsPath, tempTarballDir, OutputHelper);

        var filePathCountMap = new Dictionary<string, int>();
        (string FilePath, long Bytes)[] tarEntries = Directory.EnumerateFiles(tempTarballDir, "*", SearchOption.AllDirectories)
            .Where(filepath => !filepath.Contains("SourceBuildReferencePackages"))
            .Select(filePath =>
            {
                string result = filePath.Substring(tempTarballDir.Length + 1);
                result = ProcessEntryName(result, filePathCountMap);
                return (FilePath: result, Bytes: new FileInfo(filePath).Length);
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

        Directory.Delete(tempTarballDir, true);

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

    private string ProcessEntryName(string originalName, Dictionary<string, int> filePathCountMap)
    {
        string result = BaselineHelper.RemoveRids(originalName);
        result = BaselineHelper.RemoveVersions(result);

        string[] patterns = {@"x\.y\.z", @"x\.y(?!\.z)"};
        int matchIndex = -1;
        string matchPattern = "";
        foreach (string pattern in patterns)
        {
            MatchCollection matches = Regex.Matches(result, pattern);

            if (matches.Count > 0)
            {
                if (matches[matches.Count - 1].Index > matchIndex)
                {
                    matchIndex = matches[matches.Count - 1].Index;
                    matchPattern = matches[matches.Count - 1].Value;
                }
            }
        }

        if (matchIndex != -1)
        {
            int count = filePathCountMap.TryGetValue(result, out count) ? count : 0;
            filePathCountMap[result] = count + 1;

            if (count > 0)
            {
                result = result.Substring(0, matchIndex) + $"{matchPattern}-{count}" + result.Substring(matchIndex + matchPattern.Length);
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
        else if (baselineSize != 0 && (((fileSize - baselineSize) / (double)baselineSize) * 100) >= SizeThresholdPercentage)
        {
            OutputHelper.LogWarningMessage($"'{filePath}' increased in size by more than {SizeThresholdPercentage}%. It was originally {baselineSize} bytes and is now {fileSize} bytes");
        }
        else if (baselineSize != 0 && (((baselineSize - fileSize) / (double)baselineSize) * 100) >= SizeThresholdPercentage)
        {
            OutputHelper.LogWarningMessage($"'{filePath}' decreased in size by more than {SizeThresholdPercentage}%. It was originally {baselineSize} bytes and is now {fileSize} bytes");
        }
    }
}
