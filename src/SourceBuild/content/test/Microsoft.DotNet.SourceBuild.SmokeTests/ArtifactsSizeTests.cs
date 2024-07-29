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
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class ArtifactsSizeTests : SdkTests
{
    private const string PreviouslySourceBuiltArtifactsType = "psb";
    private const string SdkType = "sdk";
    private const string SdkSymbolsType = "symSdk";
    private const string UnifiedSymbolsType = "sdkUnified";

    private StringBuilder Differences = new();
    private List<string> NewExclusions = new List<string>();
    private Dictionary<string, int> FilePathCountMap = new();
    ExclusionsHelper exclusionsHelper = new ExclusionsHelper("ArtifactExclusions.txt", nameof(ArtifactsSizeTests));

    public ArtifactsSizeTests(ITestOutputHelper outputHelper) : base(outputHelper) {}

    [ConditionalFact(typeof(Config), nameof(Config.IncludeArtifactsSizeTests))]
    public void CheckZeroSizeArtifacts()
    {
        Assert.False(string.IsNullOrWhiteSpace(Config.SourceBuiltArtifactsPath));
        Assert.False(string.IsNullOrWhiteSpace(Config.SdkTarballPath));
        Assert.False(string.IsNullOrWhiteSpace(Config.SdkSymbolsTarballPath));
        Assert.False(string.IsNullOrWhiteSpace(Config.UnifiedSymbolsTarballPath));

        ProcessTarball(Config.SourceBuiltArtifactsPath, PreviouslySourceBuiltArtifactsType);
        ProcessTarball(Config.SdkTarballPath, SdkType);
        ProcessTarball(Config.SdkSymbolsTarballPath, SdkSymbolsType);
        ProcessTarball(Config.UnifiedSymbolsTarballPath, UnifiedSymbolsType);

        exclusionsHelper.GenerateNewBaselineFile(updatedFileTag: null, NewExclusions);

        // Wait to report differences until after the baseline file is updated. 
        // Else a failure will cause the baseline file to not be updated.
        ReportDifferences();
    }

    private void ProcessTarball(string tarballPath, string type)
    {
        string tempTarballDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempTarballDir);

        Utilities.ExtractTarball(tarballPath, tempTarballDir, OutputHelper);

        foreach (string filePath in Directory.EnumerateFiles(tempTarballDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = filePath.Substring(tempTarballDir.Length + 1);
            string processedPath = ProcessFilePath(relativePath);

            if (new FileInfo(filePath).Length == 0)
            {
                if (!exclusionsHelper.IsFileExcluded(processedPath, type))
                {
                    NewExclusions.Add($"{processedPath}|{type}");
                    TrackDifference($"{processedPath} is 0 bytes.");
                }
            }
        }

        Directory.Delete(tempTarballDir, true);
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
}
