// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.UnifiedBuild.Tests;

[Trait("Category", "SdkContent")]
public class SdkContentTests : TestBase
{
    Exclusions Exclusions;

    static string UbSdkArchivePath { get; } = Config.GetRuntimeConfig(UbSdkArchivePathSwitch);
    const string UbSdkArchivePathSwitch = Config.RuntimeConfigSwitchPrefix + nameof(UbSdkArchivePath);

    static string UbSdkVersion { get; } = Config.GetRuntimeConfig(UbSdkVersionSwitch);
    const string UbSdkVersionSwitch = Config.RuntimeConfigSwitchPrefix + nameof(UbSdkVersion);

    static string MsftSdkArchivePath { get; } = Config.TryGetRuntimeConfig(MsftSdkArchivePathSwitch, out string? value) ? value : DownloadMsftSdkArchive().Result;
    const string MsftSdkArchivePathSwitch = Config.RuntimeConfigSwitchPrefix + nameof(MsftSdkArchivePath);


    public SdkContentTests(ITestOutputHelper outputHelper) : base(outputHelper)
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
        WriteTarballFileList(MsftSdkArchivePath, msftFileListingFileName, isPortable: true, Exclusions.MsftPrefix);
        WriteTarballFileList(UbSdkArchivePath, ubFileListingFileName, isPortable: true, Exclusions.UbPrefix);

        string diff = BaselineHelper.DiffFiles(msftFileListingFileName, ubFileListingFileName, OutputHelper);
        diff = RemoveDiffMarkers(diff);
        BaselineHelper.CompareBaselineContents(Exclusions.GetBaselineFileDiffFileName(), diff, Config.LogsDirectory, OutputHelper, Config.WarnOnContentDiffs);
    }

    [Fact]
    public async Task CompareMsftToUbAssemblyVersions()
    {
        Assert.NotNull(MsftSdkArchivePath);
        Assert.NotNull(UbSdkArchivePath);

        DirectoryInfo tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        try
        {
            DirectoryInfo ubSdkDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, Exclusions.UbPrefix));
            Utilities.ExtractTarball(UbSdkArchivePath, ubSdkDir.FullName, OutputHelper);

            DirectoryInfo msftSdkDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, Exclusions.MsftPrefix));
            Utilities.ExtractTarball(MsftSdkArchivePath, msftSdkDir.FullName, OutputHelper);

            var t1 = Task.Run(() => GetSdkAssemblyVersions(ubSdkDir.FullName));
            var t2 = Task.Run(() => GetSdkAssemblyVersions(msftSdkDir.FullName));
            var results = await Task.WhenAll(t1, t2);
            Dictionary<string, Version?> ubSdkAssemblyVersions = results[0];
            Dictionary<string, Version?> msftSdkAssemblyVersions = results[1];

            RemoveExcludedAssemblyVersionPaths(ubSdkAssemblyVersions, msftSdkAssemblyVersions);

            const string UbVersionsFileName = "ub_assemblyversions.txt";
            AssemblyVersionHelpers.WriteAssemblyVersionsToFile(ubSdkAssemblyVersions, UbVersionsFileName);

            const string MsftVersionsFileName = "msft_assemblyversions.txt";
            AssemblyVersionHelpers.WriteAssemblyVersionsToFile(msftSdkAssemblyVersions, MsftVersionsFileName);

            string diff = BaselineHelper.DiffFiles(MsftVersionsFileName, UbVersionsFileName, OutputHelper);
            diff = RemoveDiffMarkers(diff);
            BaselineHelper.CompareBaselineContents($"MsftToUbSdkAssemblyVersions-{Config.TargetRid}.diff", diff, Config.LogsDirectory, OutputHelper, Config.WarnOnContentDiffs);
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
                                Assert.True(ubSdkAssemblyVersions.TryAdd(normalizedPath, AssemblyVersionHelpers.GetVersion(assemblyName)));
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
        fileListing = BaselineHelper.RemoveRids(fileListing, Config.PortableRid, Config.TargetRid, isPortable);
        fileListing = BaselineHelper.RemoveVersions(fileListing);
        IEnumerable<string> files = fileListing.Split(Environment.NewLine).OrderBy(path => path);
        files = Exclusions.RemoveContentDiffFileExclusions(files, sdkType);

        File.WriteAllLines(outputFileName, files);
    }

    public static string RemoveDiffMarkers(string source)
    {
        Regex indexRegex = new("^index .*", RegexOptions.Multiline);
        string result = indexRegex.Replace(source, "index ------------");

        Regex diffSegmentRegex = new("^@@ .* @@", RegexOptions.Multiline);
        return diffSegmentRegex.Replace(result, "@@ ------------ @@").ReplaceLineEndings();
    }

    static string GetArchiveExtension(string path)
    {
        if (path.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
            return ".zip";
        if (path.EndsWith(".tar.gz", StringComparison.InvariantCultureIgnoreCase))
            return ".tar.gz";
        throw new InvalidOperationException($"Path does not have a valid archive extenions: '{path}'");
    }

    static async Task<string> DownloadMsftSdkArchive()
    {
        string downloadCacheDir = Path.Combine(Config.DownloadCacheDirectory, "Sdks");
        Directory.CreateDirectory(downloadCacheDir);
        var client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });
        var channel = UbSdkVersion[..5] + "xx";
        var akaMsUrl = $"https://aka.ms/dotnet/{channel}/daily/dotnet-sdk-{Config.TargetRid}{GetArchiveExtension(UbSdkArchivePath)}";

        var redirectResponse = await client.GetAsync(akaMsUrl);

        // aka.ms returns a 301 for valid redirects and a 302 to Bing for invalid URLs
        if (redirectResponse.StatusCode != HttpStatusCode.Moved)
        {
            throw new InvalidOperationException($"Could not find download link for Microsoft built sdk at '{akaMsUrl}'");
        }
        var closestUrl = redirectResponse.Headers.Location!.ToString();
        var msftSdkFileName = Path.GetFileName(redirectResponse.Headers.Location.LocalPath);

        var localMsftSdkPath = Path.Combine(downloadCacheDir, msftSdkFileName);

        if (File.Exists(localMsftSdkPath))
        {
            return localMsftSdkPath;
        }

        var fileStream = await client.GetStreamAsync(closestUrl);
        using (FileStream file = File.Create(localMsftSdkPath))
        {
            await fileStream.CopyToAsync(file);
        }

        return localMsftSdkPath;
    }
}
