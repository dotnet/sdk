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
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

[Trait("Category", "SdkContent")]
public class ArtifactsSizeTest : SdkTests
{
    private const int SizeThresholdPercentage = 25;
    private static readonly string BaselineFilePath = BaselineHelper.GetBaselineFilePath($"ArtifactsSizes/{Config.TargetRid}.txt");
    private readonly Dictionary<string, long> Baseline = new();
    private Dictionary<string, int> FilePathCountMap = new();
    private StringBuilder Differences = new();

    public ArtifactsSizeTest(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        if (File.Exists(BaselineFilePath))
        {
            string[] baselineFileContent = File.ReadAllLines(BaselineFilePath);
            foreach (string entry in baselineFileContent)
            {
                string[] splitEntry = entry.Split(':', StringSplitOptions.TrimEntries);
                Baseline[splitEntry[0]] = long.Parse(splitEntry[1]);
            }
        }
        else
        {
            Assert.Fail($"Baseline file `{BaselineFilePath}' does not exist. Please create the baseline file then rerun the test.");
        }
    }

    [SkippableFact(Config.IncludeArtifactsSizeEnv, skipOnFalseEnv: true)]
    public void CompareArtifactsToBaseline()
    {
        Utilities.ValidateNotNullOrWhiteSpace(Config.SourceBuiltArtifactsPath, Config.SourceBuiltArtifactsPathEnv);
        Utilities.ValidateNotNullOrWhiteSpace(Config.SdkTarballPath, Config.SdkTarballPathEnv);
        Utilities.ValidateNotNullOrWhiteSpace(Config.TargetRid, Config.TargetRidEnv);

        var tarEntries = ProcessSdkAndArtifactsTarballs();
        ScanForDifferences(tarEntries);
        UpdateBaselineFile();

        // Must wait to report differences until after the baseline file is updated else a failure
        // will cause the baseline file to not be updated.
        ReportDifferences();
    }

    private Dictionary<string, long> ProcessSdkAndArtifactsTarballs()
    {
        string tempTarballDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempTarballDir);

        Utilities.ExtractTarball(Config.SdkTarballPath, tempTarballDir, OutputHelper);
        Utilities.ExtractTarball(Config.SourceBuiltArtifactsPath, tempTarballDir, OutputHelper);

        Dictionary<string, long> tarEntries = Directory.EnumerateFiles(tempTarballDir, "*", SearchOption.AllDirectories)
            .Where(filePath => !filePath.Contains("SourceBuildReferencePackages"))
            .Select(filePath =>
            {
                string relativePath = filePath.Substring(tempTarballDir.Length + 1);
                return (ProcessFilePath(relativePath), new FileInfo(filePath).Length);
            })
            .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);

        Directory.Delete(tempTarballDir, true);

        return tarEntries;
    }

    private string ProcessFilePath(string originalPath)
    {
        string result = BaselineHelper.RemoveRids(originalPath);
        result = BaselineHelper.RemoveVersions(result);

        return AddDifferenciatingSuffix(result);
    }

    // Because version numbers are abstracted, it is possible to have duplicate FilePath entries.
    // This code adds a numeric suffix to differentiate duplicate FilePath entries.
    private string AddDifferenciatingSuffix(string filePath)
    {
        string[] patterns = {@"x\.y\.z", @"x\.y(?!\.z)"};
        int matchIndex = -1;
        string matchPattern = "";
        foreach (string pattern in patterns)
        {
            MatchCollection matches = Regex.Matches(filePath, pattern);

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
            int count = FilePathCountMap.TryGetValue(filePath, out count) ? count : 0;
            FilePathCountMap[filePath] = count + 1;

            if (count > 0)
            {
                return filePath.Substring(0, matchIndex) + $"{matchPattern}-{count}" + filePath.Substring(matchIndex + matchPattern.Length);
            }
        }

        return filePath;
    }

    private void ScanForDifferences(Dictionary<string, long> tarEntries)
    {
        foreach (var entry in tarEntries)
        {
            if (!Baseline.TryGetValue(entry.Key, out long baselineBytes))
            {
                TrackDifference($"{entry.Key} does not exist in baseline. It is {entry.Value} bytes. Adding it to the baseline file.");
                Baseline.Add(entry.Key, entry.Value); 
            }
            else
            {
                CompareFileSizes(entry.Key, entry.Value, baselineBytes);
            }
        }

        foreach (var removedFile in Baseline.Keys.Except(tarEntries.Keys))
        {
            TrackDifference($"`{removedFile}` is no longer being produced. It was {Baseline[removedFile]} bytes.");
            Baseline.Remove(removedFile);
        }
    }

    private void CompareFileSizes(string filePath, long fileSize, long baselineSize)
    {
        // Only update the baseline with breaking differences. Non-breaking differences are file size changes
        // less than the threshold percentage. This makes it easier to review the breaking changes and prevents
        // inadvertently allowing small percentage changes to be accepted that can add up to a significant
        // difference over time.
        string breakingDifference = string.Empty;

        if (fileSize == 0 && baselineSize != 0)
        {
            breakingDifference = $"'{filePath}' is now 0 bytes. It was {baselineSize} bytes.";
        }
        else if (fileSize != 0 && baselineSize == 0)
        {
            breakingDifference = $"'{filePath}' is no longer 0 bytes. It is now {fileSize} bytes.";
        }
        else if (baselineSize != 0 && (((fileSize - baselineSize) / (double)baselineSize) * 100) >= SizeThresholdPercentage)
        {
            breakingDifference =
                $"'{filePath}' increased in size by more than {SizeThresholdPercentage}%. It was originally {baselineSize} bytes and is now {fileSize} bytes.";
        }
        else if (baselineSize != 0 && (((baselineSize - fileSize) / (double)baselineSize) * 100) >= SizeThresholdPercentage)
        {
            breakingDifference =
                $"'{filePath}' decreased in size by more than {SizeThresholdPercentage}%. It was originally {baselineSize} bytes and is now {fileSize} bytes.";
        }

        if (!string.IsNullOrEmpty(breakingDifference))
        {
            TrackDifference(breakingDifference);
            Baseline[filePath] = fileSize;
        }
    }

    private void TrackDifference(string difference) => Differences.AppendLine(difference);

    private void ReportDifferences()
    {
        if (Differences.Length > 0)
        {
            if (Config.WarnOnSdkContentDiffs)
            {
                OutputHelper.LogWarningMessage(Differences.ToString());
            }
            else
            {
                OutputHelper.WriteLine(Differences.ToString());
                Assert.Fail("Differences were found in the artifacts sizes.");
            }
        }
    }

    private void UpdateBaselineFile()
    {
        try
        {
            string actualFilePath = Path.Combine(LogsDirectory, $"UpdatedArtifactsSizes_{Config.TargetRid}.txt");
            File.WriteAllLines(
                actualFilePath,
                Baseline
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"An error occurred while copying the baselines file: {BaselineFilePath}", ex);
        }
    }
}
