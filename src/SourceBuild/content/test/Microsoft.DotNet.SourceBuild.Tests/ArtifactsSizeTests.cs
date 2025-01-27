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
using ExclusionsLibrary;
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

public class ArtifactsSizeTests : SdkTests
{
    private const string SdkType = "sdk";
    private readonly StringBuilder _differences = new();
    private readonly List<string> _newExclusions = new List<string>();
    private readonly Dictionary<string, int> _filePathCountMap = new();
    private readonly ExclusionsHelper _exclusionsHelper = new ExclusionsHelper(BaselineHelper.GetBaselineFilePath("ZeroSizeExclusions.txt", nameof(ArtifactsSizeTests)));
    public static bool IncludeArtifactsSizeTests => !string.IsNullOrWhiteSpace(Config.SdkTarballPath);

    public ArtifactsSizeTests(ITestOutputHelper outputHelper) : base(outputHelper) {}

    [ConditionalFact(typeof(ArtifactsSizeTests), nameof(IncludeArtifactsSizeTests))]
    public void CheckZeroSizeArtifacts()
    {
        ProcessTarball(Config.SdkTarballPath!, SdkType);

        _exclusionsHelper.GenerateNewBaselineFile(additionalLines: _newExclusions, targetDirectory: Config.LogsDirectory);

        // Wait to report differences until after the baseline file is updated. 
        // Else a failure will cause the baseline file to not be updated.
        ReportDifferences();
    }

    private void ProcessTarball(string tarballPath, string type)
    {
        string tempTarballDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempTarballDir);

        Utilities.ExtractTarball(tarballPath, tempTarballDir, OutputHelper);

        var newZeroSizedFiles = Directory
            .EnumerateFiles(tempTarballDir, "*", SearchOption.AllDirectories)
            .Where(filePath => new FileInfo(filePath).Length == 0)
            .Select(filePath => ProcessFilePath(tempTarballDir, filePath))
            .Where(processedPath => !_exclusionsHelper.IsFileExcluded(processedPath, type));

        foreach (string file in newZeroSizedFiles)
        {
            _newExclusions.Add($"{file}|{type}");
            TrackDifference($"{file} is 0 bytes.");
        }

        Directory.Delete(tempTarballDir, true);
    }

    private string ProcessFilePath(string relativeTo, string originalPath)
    {
        string relativePath = Path.GetRelativePath(relativeTo, originalPath);
        string result = BaselineHelper.RemoveRids(relativePath);
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
            int count = _filePathCountMap.TryGetValue(filePath, out count) ? count : 0;
            _filePathCountMap[filePath] = count + 1;

            if (count > 0)
            {
                return filePath.Substring(0, matchIndex) + $"{matchPattern}-{count}" + filePath.Substring(matchIndex + matchPattern.Length);
            }
        }

        return filePath;
    }

    private void TrackDifference(string difference) => _differences.AppendLine(difference);

    private void ReportDifferences()
    {
        if (_differences.Length > 0)
        {
            OutputHelper.LogWarningMessage(_differences.ToString());
            Assert.Fail("Differences were found in the artifacts sizes.");
        }
    }
}
