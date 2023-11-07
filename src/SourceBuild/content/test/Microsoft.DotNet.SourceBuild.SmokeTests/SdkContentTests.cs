// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

[Trait("Category", "SdkContent")]
public class SdkContentTests : SdkTests
{
    private const string MsftSdkType = "msft";
    private const string SourceBuildSdkType = "sb";

    public SdkContentTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    /// <Summary>
    /// Verifies the file layout of the source built sdk tarball to the Microsoft build.
    /// The differences are captured in baselines/MsftToSbSdkDiff.txt.
    /// Version numbers that appear in paths are compared but are stripped from the baseline.
    /// This makes the baseline durable between releases.  This does mean however, entries
    /// in the baseline may appear identical if the diff is version specific.
    /// </Summary>
    [SkippableFact(new[] { Config.MsftSdkTarballPathEnv, Config.SdkTarballPathEnv }, skipOnNullOrWhiteSpaceEnv: true)]
    public void CompareMsftToSbFileList()
    {
        const string msftFileListingFileName = "msftSdkFiles.txt";
        const string sbFileListingFileName = "sbSdkFiles.txt";
        WriteTarballFileList(Config.MsftSdkTarballPath, msftFileListingFileName, isPortable: true, MsftSdkType);
        WriteTarballFileList(Config.SdkTarballPath, sbFileListingFileName, isPortable: false, SourceBuildSdkType);

        string diff = BaselineHelper.DiffFiles(msftFileListingFileName, sbFileListingFileName, OutputHelper);
        diff = RemoveDiffMarkers(diff);
        BaselineHelper.CompareBaselineContents("MsftToSbSdkFiles.diff", diff, OutputHelper, Config.WarnOnSdkContentDiffs);
    }

    [SkippableFact(new[] { Config.MsftSdkTarballPathEnv, Config.SdkTarballPathEnv }, skipOnNullOrWhiteSpaceEnv: true)]
    public void CompareMsftToSbAssemblyVersions()
    {
        Assert.NotNull(Config.MsftSdkTarballPath);
        Assert.NotNull(Config.SdkTarballPath);

        DirectoryInfo tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            DirectoryInfo sbSdkDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, SourceBuildSdkType));
            Utilities.ExtractTarball(Config.SdkTarballPath, sbSdkDir.FullName, OutputHelper);

            DirectoryInfo msftSdkDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, MsftSdkType));
            Utilities.ExtractTarball(Config.MsftSdkTarballPath, msftSdkDir.FullName, OutputHelper);

            Dictionary<string, Version?> sbSdkAssemblyVersions = GetSbSdkAssemblyVersions(sbSdkDir.FullName);
            Dictionary<string, Version?> msftSdkAssemblyVersions = GetMsftSdkAssemblyVersions(msftSdkDir.FullName, sbSdkAssemblyVersions);

            RemoveExcludedAssemblyVersionPaths(sbSdkAssemblyVersions, msftSdkAssemblyVersions);

            const string SbVersionsFileName = "sb_assemblyversions.txt";
            WriteAssemblyVersionsToFile(sbSdkAssemblyVersions, SbVersionsFileName);

            const string MsftVersionsFileName = "msft_assemblyversions.txt";
            WriteAssemblyVersionsToFile(msftSdkAssemblyVersions, MsftVersionsFileName);

            string diff = BaselineHelper.DiffFiles(MsftVersionsFileName, SbVersionsFileName, OutputHelper);
            diff = RemoveDiffMarkers(diff);
            BaselineHelper.CompareBaselineContents("MsftToSbSdkAssemblyVersions.diff", diff, OutputHelper, Config.WarnOnSdkContentDiffs);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static void RemoveExcludedAssemblyVersionPaths(Dictionary<string, Version?> sbSdkAssemblyVersions, Dictionary<string, Version?> msftSdkAssemblyVersions)
    {
        IEnumerable<string> assemblyVersionDiffFilters = GetSdkAssemblyVersionDiffExclusionFilters()
            .Select(filter => filter.TrimStart("./".ToCharArray()));

        // Remove any excluded files as long as SB SDK's file has the same or greater assembly version compared to the corresponding
        // file in the MSFT SDK. If the version is less, the file will show up in the results as this is not a scenario
        // that is valid for shipping.
        string[] sbSdkFileArray = sbSdkAssemblyVersions.Keys.ToArray();
        for (int i = sbSdkFileArray.Length - 1; i >= 0; i--)
        {
            string assemblyPath = sbSdkFileArray[i];
            Version? sbVersion = sbSdkAssemblyVersions[assemblyPath];
            Version? msftVersion = msftSdkAssemblyVersions[assemblyPath];

            if (sbVersion is not null &&
                msftVersion is not null &&
                sbVersion >= msftVersion &&
                Utilities.IsFileExcluded(assemblyPath, assemblyVersionDiffFilters))
            {
                sbSdkAssemblyVersions.Remove(assemblyPath);
                msftSdkAssemblyVersions.Remove(assemblyPath);
            }
        }
    }

    private static void WriteAssemblyVersionsToFile(Dictionary<string, Version?> assemblyVersions, string outputPath)
    {
        string[] lines = assemblyVersions
            .Select(kvp => $"{kvp.Key} - {kvp.Value}")
            .Order()
            .ToArray();
        File.WriteAllLines(outputPath, lines);
    }

    private Dictionary<string, Version?> GetMsftSdkAssemblyVersions(
        string msftSdkPath, Dictionary<string, Version?> sbSdkAssemblyVersions)
    {
        Dictionary<string, Version?> msftSdkAssemblyVersions = new();
        foreach ((string relativePath, _) in sbSdkAssemblyVersions)
        {
            // Now we want to find the corresponding file that exists in the MSFT SDK.
            // We've already replaced version numbers with placeholders in the path.
            // So we can't directly use the relative path to find the corresponding file. Instead,
            // we need to replace the version placeholders with wildcards and find the path through path matching.
            string file = Path.Combine(msftSdkPath, relativePath);
            Matcher matcher = BaselineHelper.GetFileMatcherFromPath(relativePath);

            file = FindMatchingFilePath(msftSdkPath, matcher, relativePath);

            if (!File.Exists(file))
            {
                continue;
            }

            AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);
            msftSdkAssemblyVersions.Add(BaselineHelper.RemoveVersions(relativePath), GetVersion(assemblyName));
        }
        return msftSdkAssemblyVersions;
    }

    // It's known that assembly versions can be different between builds in their revision field. Disregard that difference
    // by excluding that field in the output.
    private static Version? GetVersion(AssemblyName assemblyName)
    {
        if (assemblyName.Version is not null)
        {
            return new Version(assemblyName.Version.ToString(3));
        }

        return null;
    }

    private string FindMatchingFilePath(string rootDir, Matcher matcher, string representativeFile)
    {
        foreach (string file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
        {
            if (matcher.Match(rootDir, file).HasMatches)
            {
                return file;
            }
        }

        Assert.Fail($"Unable to find matching file for '{representativeFile}' in '{rootDir}'.");
        return string.Empty;
    }

    private Dictionary<string, Version?> GetSbSdkAssemblyVersions(string sbSdkPath)
    {
        IEnumerable<string> exclusionFilters = GetSdkDiffExclusionFilters(SourceBuildSdkType)
            .Select(filter => filter.TrimStart("./".ToCharArray()));
        Dictionary<string, Version?> sbSdkAssemblyVersions = new();
        foreach (string file in Directory.EnumerateFiles(sbSdkPath, "*", SearchOption.AllDirectories))
        {
            string fileExt = Path.GetExtension(file);
            if (fileExt.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                fileExt.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);
                string relativePath = Path.GetRelativePath(sbSdkPath, file);
                string normalizedPath = BaselineHelper.RemoveVersions(relativePath);

                if (!Utilities.IsFileExcluded(normalizedPath, exclusionFilters))
                {
                    sbSdkAssemblyVersions.Add(normalizedPath, GetVersion(assemblyName));
                }
            }
        }
        return sbSdkAssemblyVersions;
    }

    private void WriteTarballFileList(string? tarballPath, string outputFileName, bool isPortable, string sdkType)
    {
        if (!File.Exists(tarballPath))
        {
            throw new InvalidOperationException($"Tarball path '{tarballPath}' does not exist.");
        }

        string fileListing = Utilities.GetTarballContentNames(tarballPath).Aggregate((a, b) => $"{a}{Environment.NewLine}{b}");
        fileListing = BaselineHelper.RemoveRids(fileListing, isPortable);
        fileListing = BaselineHelper.RemoveVersions(fileListing);
        IEnumerable<string> files = fileListing.Split(Environment.NewLine).OrderBy(path => path);
        files = RemoveExclusions(files, GetSdkDiffExclusionFilters(sdkType));

        File.WriteAllLines(outputFileName, files);
    }

    private static IEnumerable<string> RemoveExclusions(IEnumerable<string> files, IEnumerable<string> exclusions) =>
        files.Where(item => !Utilities.IsFileExcluded(item, exclusions));

    private static IEnumerable<string> GetSdkDiffExclusionFilters(string sdkType) =>
        Utilities.ParseExclusionsFile("SdkFileDiffExclusions.txt", sdkType);

    private static IEnumerable<string> GetSdkAssemblyVersionDiffExclusionFilters() =>
        Utilities.ParseExclusionsFile("SdkAssemblyVersionDiffExclusions.txt");

    private static string RemoveDiffMarkers(string source)
    {
        Regex indexRegex = new("^index .*", RegexOptions.Multiline);
        string result = indexRegex.Replace(source, "index ------------");

        Regex diffSegmentRegex = new("^@@ .* @@", RegexOptions.Multiline);
        return diffSegmentRegex.Replace(result, "@@ ------------ @@");
    }
}
