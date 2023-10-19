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
public class ArtifactsSizeTest : SdkTests
{
    private const int SizeThresholdPercentage = 25;
    private static readonly string BaselineFilePath = BaselineHelper.GetBaselineFilePath($"ArtifactsSizes/{Config.TargetRid}.txt");
    private readonly Dictionary<string, long> BaselineFileContent = new();
    private Dictionary<string, int> FilePathCountMap = new();

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


    // https://github.com/dotnet/source-build/issues/3668
    //[SkippableFact(Config.IncludeArtifactsSizeEnv, skipOnFalseEnv: true)]
    public void CompareArtifactsToBaseline()
    {
        Utilities.ValidateNotNullOrWhiteSpace(Config.SourceBuiltArtifactsPath, Config.SourceBuiltArtifactsPathEnv);
        Utilities.ValidateNotNullOrWhiteSpace(Config.SdkTarballPath, Config.SdkTarballPathEnv);
        Utilities.ValidateNotNullOrWhiteSpace(Config.TargetRid, Config.TargetRidEnv);

        var tarEntries = ProcessSdkAndArtifactsTarballs();

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
            string actualFilePath = Path.Combine(LogsDirectory, $"UpdatedArtifactsSizes_{Config.TargetRid}.txt");
            File.WriteAllLines(actualFilePath, tarEntries.Select(entry => $"{entry.FilePath}: {entry.Bytes}"));
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"An error occurred while copying the baselines file: {BaselineFilePath}", ex);
        }
    }

    private (string FilePath, long Bytes)[] ProcessSdkAndArtifactsTarballs()
    {
        string tempTarballDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempTarballDir);

        Utilities.ExtractTarball(Config.SdkTarballPath, tempTarballDir, OutputHelper);
        Utilities.ExtractTarball(Config.SourceBuiltArtifactsPath, tempTarballDir, OutputHelper);

        (string FilePath, long Bytes)[] tarEntries = Directory.EnumerateFiles(tempTarballDir, "*", SearchOption.AllDirectories)
            .Where(filepath => !filepath.Contains("SourceBuildReferencePackages"))
            .Select(filePath =>
            {
                string result = filePath.Substring(tempTarballDir.Length + 1);
                result = ProcessFilePath(result);
                return (FilePath: result, Bytes: new FileInfo(filePath).Length);
            })
            .OrderBy(entry => entry.FilePath)
            .ToArray();
        
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
