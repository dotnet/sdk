// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class SdkContentTests
{
    private ITestOutputHelper OutputHelper { get; }
    private DotNetHelper DotNetHelper { get; }

    public SdkContentTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
        DotNetHelper = new DotNetHelper(outputHelper);
    }

    /// <Summary>
    /// Verifies the file layout of the source built sdk tarball to the Microsoft build.
    /// The differences are captured in baselines/MsftToSbSdkDiff.txt.
    /// Version numbers that appear in paths are compared but are stripped from the baseline.
    /// This makes the baseline durable between releases.  This does mean however, entries
    /// in the baseline may appear identical if the diff is version specific.
    /// </Summary>
    [Fact]
    public void CompareMsftToSb()
    {
        if (string.IsNullOrWhiteSpace(Config.MsftSdkTarballPath))
        {
            OutputHelper.WriteLine($"skipping {nameof(CompareMsftToSb)} because {Config.MsftSdkTarballPathEnv} was not specified.");
            return;
        }

        if (!File.Exists(Config.MsftSdkTarballPath))
        {
            throw new InvalidOperationException($"Tarball path '{Config.MsftSdkTarballPath}' specified in {Config.MsftSdkTarballPathEnv} does not exist.");
        }

        const string msftFileListingFileName = "msftSdkFiles.txt";
        const string sbFileListingFileName = "sbSdkFiles.txt";
        WriteTarballFileList(Config.MsftSdkTarballPath, msftFileListingFileName);
        WriteTarballFileList(Config.DotNetTarballPath, sbFileListingFileName);

        string diff = BaselineHelper.DiffFiles(msftFileListingFileName, sbFileListingFileName, OutputHelper);
        diff = RemoveVersionedPaths(diff);
        diff = RemoveDiffMarkers(diff);
        diff = RemoveRids(diff);
        BaselineHelper.CompareContents("MsftToSbSdk.diff", diff, OutputHelper);
    }

    private void WriteTarballFileList(string tarballPath, string outputFileName)
    {
        string fileListing = ExecuteHelper.ExecuteProcessValidateExitCode("tar", $"tf {tarballPath}", OutputHelper);
        IEnumerable<string> files = fileListing.Split(Environment.NewLine).OrderBy(path => path);
        File.WriteAllLines(outputFileName, files);
    }

    private static string RemoveDiffMarkers(string source)
    {
        Regex indexRegex = new("^index .*", RegexOptions.Multiline);
        string result = indexRegex.Replace(source, "index ------------");

        Regex diffSegmentRegex = new("^@@ .* @@", RegexOptions.Multiline);
        return diffSegmentRegex.Replace(result, "@@ ------------ @@");
    }

    private string RemoveRids(string diff) => diff.Replace(Config.TargetRid, "bannana.rid");

    private static string RemoveVersionedPaths(string source)
    {
        // Remove semantic version path segments
        string pathSeparator = Regex.Escape(Path.DirectorySeparatorChar.ToString());
        // Regex source: https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
        Regex semanticVersionRegex = new(
            $"{pathSeparator}(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)"
            + $"(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))"
            + $"?(?:\\+([0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?{pathSeparator}");
        string result = semanticVersionRegex.Replace(source, $"{Path.DirectorySeparatorChar}x.y.z{Path.DirectorySeparatorChar}");

        // Remove net.x.y path segments
        Regex netTfmRegex = new($"{pathSeparator}net[1-9]*.[0-9]{pathSeparator}");
        return netTfmRegex.Replace(result, $"{Path.DirectorySeparatorChar}netx.y{Path.DirectorySeparatorChar}");
    }
}
