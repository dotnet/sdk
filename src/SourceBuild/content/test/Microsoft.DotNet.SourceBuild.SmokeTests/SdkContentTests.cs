// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class SdkContentTests : SmokeTests
{
    public SdkContentTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Verifies the file layout of the source built sdk tarball to the Microsoft build.
    /// The differences are captured in baselines/MsftToSbSdkDiff.txt.
    /// Version numbers that appear in paths are compared but are stripped from the baseline.
    /// This makes the baseline durable between releases.  This does mean however, entries
    /// in the baseline may appear identical if the diff is version specific.
    /// </Summary>
    [SkippableFact(new[] { Config.MsftSdkTarballPathEnv, Config.SdkTarballPathEnv }, skipOnNullOrWhiteSpace: true)]
    public void CompareMsftToSb()
    {
        const string msftFileListingFileName = "msftSdkFiles.txt";
        const string sbFileListingFileName = "sbSdkFiles.txt";
        WriteTarballFileList(Config.MsftSdkTarballPath, msftFileListingFileName, isPortable: true, "msft");
        WriteTarballFileList(Config.SdkTarballPath, sbFileListingFileName, isPortable: false, "sb");

        string diff = BaselineHelper.DiffFiles(msftFileListingFileName, sbFileListingFileName, OutputHelper);
        diff = RemoveDiffMarkers(diff);
        BaselineHelper.CompareContents("MsftToSbSdk.diff", diff, OutputHelper, Config.WarnOnSdkContentDiffs);
    }

    private void WriteTarballFileList(string? tarballPath, string outputFileName, bool isPortable, string sdkType)
    {
        if (!File.Exists(tarballPath))
        {
            throw new InvalidOperationException($"Tarball path '{tarballPath}' does not exist.");
        }

        string fileListing = ExecuteHelper.ExecuteProcessValidateExitCode("tar", $"tf {tarballPath}", OutputHelper);
        fileListing = BaselineHelper.RemoveRids(fileListing, isPortable);
        fileListing = BaselineHelper.RemoveVersions(fileListing);
        IEnumerable<string> files = fileListing.Split(Environment.NewLine).OrderBy(path => path);
        files = RemoveExclusions(
                    files,
                    GetExclusionFilters(
                        Path.Combine(BaselineHelper.GetAssetsDirectory(), "SdkDiffExclusions.txt"),
                        sdkType));

        File.WriteAllLines(outputFileName, files);
    }

        private static IEnumerable<string> RemoveExclusions(IEnumerable<string> files, IEnumerable<string> exclusions) =>
            files.Where(item => !exclusions.Any(p => FileSystemName.MatchesSimpleExpression(p, item)));

        private static IEnumerable<string> GetExclusionFilters(string exclusionsFilePath, string sdkType)
        {
            int prefixSkip = sdkType.Length + 1;
            return File.ReadAllLines(exclusionsFilePath)
                .Where(line => line.StartsWith(sdkType + ",")) // process only specific sdk exclusions
                .Select(line =>
                {
                    // Ignore comments
                    var index = line.IndexOf('#');
                    return index >= 0 ? line[prefixSkip..index].TrimEnd() : line[prefixSkip..];
                })
                .ToList();
        }

    private static string RemoveDiffMarkers(string source)
    {
        Regex indexRegex = new("^index .*", RegexOptions.Multiline);
        string result = indexRegex.Replace(source, "index ------------");

        Regex diffSegmentRegex = new("^@@ .* @@", RegexOptions.Multiline);
        return diffSegmentRegex.Replace(result, "@@ ------------ @@");
    }
}
