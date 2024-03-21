// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

[Trait("Category", "SdkContent")]
public class SdkContentTests : TestBase
{
    Exclusions Exclusions;
    public SdkContentTests(ITestOutputHelper outputHelper, Config config) : base(outputHelper, config)
    { 
        Exclusions = new(Config.TargetRid);
    }

    /// <Summary>
    /// Verifies the file layout of the source built sdk tarball to the Microsoft build.
    /// The differences are captured in baselines/MsftToUbSdkDiff.txt.
    /// Version numbers that appear in paths are compared but are stripped from the baseline.
    /// This makes the baseline durable between releases.  This does mean however, entries
    /// in the baseline may appear identical if the diff is version specific.
    /// </Summary>
    [Fact]
    public void CompareMsftToUbFileList()
    {
        const string msftFileListingFileName = "msftSdkFiles.txt";
        const string ubFileListingFileName = "ubSdkFiles.txt";
        WriteTarballFileList(Config.MsftSdkArchivePath, msftFileListingFileName, isPortable: true, Exclusions.MsftPrefix);
        WriteTarballFileList(Config.UbSdkArchivePath, ubFileListingFileName, isPortable: true, Exclusions.UbPrefix);

        string diff = BaselineHelper.DiffFiles(msftFileListingFileName, ubFileListingFileName, OutputHelper);
        diff = RemoveDiffMarkers(diff);
        BaselineHelper.CompareBaselineContents(Exclusions.GetBaselineFileDiffFileName(), diff, OutputHelper, Config.WarnOnSdkContentDiffs);
    }

    [Fact]
    public async Task CompareMsftToUbAssemblyVersions()
    {
        Assert.NotNull(Config.MsftSdkArchivePath);
        Assert.NotNull(Config.UbSdkArchivePath);

        DirectoryInfo tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            DirectoryInfo ubSdkDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, Exclusions.UbPrefix));
            Utilities.ExtractTarball(Config.UbSdkArchivePath, ubSdkDir.FullName, OutputHelper);

            DirectoryInfo msftSdkDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, Exclusions.MsftPrefix));
            Utilities.ExtractTarball(Config.MsftSdkArchivePath, msftSdkDir.FullName, OutputHelper);

            var t1 = Task.Run(() => GetSdkAssemblyVersions(ubSdkDir.FullName));
            var t2 = Task.Run(() => GetSdkAssemblyVersions(msftSdkDir.FullName));
            var results = await Task.WhenAll(t1, t2);
            Dictionary<string, Version?> ubSdkAssemblyVersions = results[0];
            Dictionary<string, Version?> msftSdkAssemblyVersions = results[1];

            RemoveExcludedAssemblyVersionPaths(ubSdkAssemblyVersions, msftSdkAssemblyVersions);

            const string UbVersionsFileName = "ub_assemblyversions.txt";
            WriteAssemblyVersionsToFile(ubSdkAssemblyVersions, UbVersionsFileName);

            const string MsftVersionsFileName = "msft_assemblyversions.txt";
            WriteAssemblyVersionsToFile(msftSdkAssemblyVersions, MsftVersionsFileName);

            string diff = BaselineHelper.DiffFiles(MsftVersionsFileName, UbVersionsFileName, OutputHelper);
            diff = RemoveDiffMarkers(diff);
            BaselineHelper.CompareBaselineContents($"MsftToUbSdkAssemblyVersions-{Config.TargetRid}.diff", diff, OutputHelper, Config.WarnOnSdkContentDiffs);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private void RemoveExcludedAssemblyVersionPaths(Dictionary<string, Version?> ubSdkAssemblyVersions, Dictionary<string, Version?> msftSdkAssemblyVersions)
    {
        IEnumerable<string> assemblyVersionDiffFilters = Exclusions.GetAssemblyVersionExclusions()
            .Select(filter => filter.TrimStart("./".ToCharArray()));
        // Remove entries that are not in both. If they should be in both, the mismatch will be caught in another test
        foreach (var kvp in ubSdkAssemblyVersions)
        {
            if (!msftSdkAssemblyVersions.ContainsKey(kvp.Key))
            {
                ubSdkAssemblyVersions.Remove(kvp.Key);
            }
        }

        foreach (var kvp in msftSdkAssemblyVersions)
        {
            if (!ubSdkAssemblyVersions.ContainsKey(kvp.Key))
            {
                msftSdkAssemblyVersions.Remove(kvp.Key);
            }
        }

        // Remove any excluded files as long as UB SDK's file has the same or greater assembly version compared to the corresponding
        // file in the MSFT SDK. If the version is less, the file will show up in the results as this is not a scenario
        // that is valid for shipping.
        string[] ubSdkFileArray = ubSdkAssemblyVersions.Keys.ToArray();
        for (int i = ubSdkFileArray.Length - 1; i >= 0; i--)
        {
            string assemblyPath = ubSdkFileArray[i];
            if (ubSdkAssemblyVersions.TryGetValue(assemblyPath, out Version? ubVersion) &&
                msftSdkAssemblyVersions.TryGetValue(assemblyPath, out Version? msftVersion) &&
                ubVersion >= msftVersion &&
                Utilities.IsFileExcluded(assemblyPath, assemblyVersionDiffFilters))
            {
                ubSdkAssemblyVersions.Remove(assemblyPath);
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

    private Dictionary<string, Version?> GetSdkAssemblyVersions(string ubSdkPath, string? prefix = null)
    {
        Exclusions ex = Exclusions;
        IEnumerable<string> exclusionFilters = ex.GetFileExclusions(prefix)
            .Concat(ex.GetNativeDllExclusions(prefix))
            .Concat(ex.GetAssemblyVersionExclusions(prefix))
            .Select(filter => filter.TrimStart("./".ToCharArray()));
        ConcurrentDictionary<string, Version?> ubSdkAssemblyVersions = new();
        List<Task> tasks = new List<Task>();
        foreach (string dir in Directory.EnumerateDirectories(ubSdkPath, "*", SearchOption.AllDirectories).Append(ubSdkPath))
        {
            var t = Task.Run(() =>
            {
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    string fileExt = Path.GetExtension(file);
                    if (fileExt.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                        fileExt.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string relativePath = Path.GetRelativePath(ubSdkPath, file);
                        string normalizedPath = BaselineHelper.RemoveVersions(relativePath);
                        if (!ex.IsFileExcluded(normalizedPath, exclusionFilters))
                        {
                            try
                            {
                                AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);
                                Assert.True(ubSdkAssemblyVersions.TryAdd(normalizedPath, GetVersion(assemblyName)));
                            }
                            catch (BadImageFormatException)
                            {
                                Console.WriteLine($"BadImageFormatException: {file}");
                            }
                        }
                    }
                }
            });
            tasks.Add(t);
        }
        Task.WaitAll(tasks.ToArray());
        return ubSdkAssemblyVersions.ToDictionary();
    }

    private void WriteTarballFileList(string? tarballPath, string outputFileName, bool isPortable, string sdkType)
    {
        if (!File.Exists(tarballPath))
        {
            throw new InvalidOperationException($"Tarball path '{tarballPath}' does not exist.");
        }

        string fileListing = Utilities.GetTarballContentNames(tarballPath).Aggregate((a, b) => $"{a}{Environment.NewLine}{b}");
        fileListing = BaselineHelper.RemoveRids(fileListing, Config.PortableRidEnv, Config.TargetRid, isPortable);
        fileListing = BaselineHelper.RemoveVersions(fileListing);
        IEnumerable<string> files = fileListing.Split(Environment.NewLine).OrderBy(path => path);
        files = Exclusions.RemoveContentDiffFileExclusions(files, sdkType);

        File.WriteAllLines(outputFileName, files);
    }

    private static string RemoveDiffMarkers(string source)
    {
        Regex indexRegex = new("^index .*", RegexOptions.Multiline);
        string result = indexRegex.Replace(source, "index ------------");

        Regex diffSegmentRegex = new("^@@ .* @@", RegexOptions.Multiline);
        return diffSegmentRegex.Replace(result, "@@ ------------ @@").ReplaceLineEndings();
    }
}
