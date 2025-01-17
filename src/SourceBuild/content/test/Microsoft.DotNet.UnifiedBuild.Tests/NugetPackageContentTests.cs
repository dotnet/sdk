// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.UnifiedBuild.Tests;

[Trait("Category", "SdkContent")]
public class NugetPackageContentTests : TestBase
{
    static readonly ImmutableArray<string> ExcludedFileExtensions = [".psmdcp", ".p7s"];

    static ImmutableArray<string[]>? _packages = null;
    public NugetPackageContentTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    public static IEnumerable<object[]> GetPackages()
    {
        return _packages ??= NugetPackageDownloadHelpers.DownloadPackages();
    }

    /// <Summary>
    /// Verifies the file layout of the source built sdk tarball to the Microsoft build.
    /// The differences are captured in baselines/MsftToUbSdkDiff.txt.
    /// Version numbers that appear in paths are compared but are stripped from the baseline.
    /// This makes the baseline durable between releases.  This does mean however, entries
    /// in the baseline may appear identical if the diff is version specific.
    /// </Summary>
    [Theory]
    [MemberData(nameof(GetPackages))]
    public async Task CompareFileContents(string packageName, string testNugetPackagePath, string baselineNugetPackagePath)
    {
        var ct = CancellationToken.None;
        using PackageArchiveReader testPackageReader = new PackageArchiveReader(File.OpenRead(testNugetPackagePath));

        using PackageArchiveReader packageReader = new PackageArchiveReader(baselineNugetPackagePath);
        IEnumerable<string> baselineFiles = (await packageReader.GetFilesAsync(ct)).Where(f => !ExcludedFileExtensions.Contains(Path.GetExtension(f)));
        IEnumerable<string> testFiles = (await testPackageReader.GetFilesAsync(ct)).Where(f => !ExcludedFileExtensions.Contains(Path.GetExtension(f)));

        var testPackageContentsFileName = Path.Combine(Config.LogsDirectory, packageName + "_ub_files.txt");
        await File.WriteAllLinesAsync(testPackageContentsFileName, testFiles);
        var baselinePackageContentsFileName = Path.Combine(Config.LogsDirectory, packageName + "_msft_files.txt");
        await File.WriteAllLinesAsync(testPackageContentsFileName, baselineFiles);

        string diff = BaselineHelper.DiffFiles(baselinePackageContentsFileName, testPackageContentsFileName, OutputHelper);
        diff = SdkContentTests.RemoveDiffMarkers(diff);
        BaselineHelper.CompareBaselineContents($"MsftToUb-{packageName}-Files.diff", diff, Config.LogsDirectory, OutputHelper, Config.WarnOnContentDiffs);
    }

    [Theory]
    [MemberData(nameof(GetPackages))]
    public async Task CompareAssemblyVersions(string packageName, string testNugetPackagePath, string baselineNugetPackagePath)
    {
        using PackageArchiveReader testPackageReader = new PackageArchiveReader(File.OpenRead(testNugetPackagePath));

        using PackageArchiveReader baselinePackageReader = new PackageArchiveReader(baselineNugetPackagePath);
        IEnumerable<string> baselineFiles = (await baselinePackageReader.GetFilesAsync(CancellationToken.None)).Where(f => !ExcludedFileExtensions.Contains(Path.GetExtension(f)));
        IEnumerable<string> testFiles = (await testPackageReader.GetFilesAsync(CancellationToken.None)).Where(f => !ExcludedFileExtensions.Contains(Path.GetExtension(f)));
        Dictionary<string, Version?> baselineAssemblyVersions = new();
        Dictionary<string, Version?> testAssemblyVersions = new();
        foreach (var fileName in baselineFiles.Intersect(testFiles))
        {
            string baselineFileName = Path.GetTempFileName();
            string testFileName = Path.GetTempFileName();
            using (FileStream baselineFile = File.OpenWrite(baselineFileName))
            using (FileStream testFile = File.OpenWrite(testFileName))
            {
                await baselinePackageReader.GetEntry(fileName).Open().CopyToAsync(baselineFile);
                await testPackageReader.GetEntry(fileName).Open().CopyToAsync(testFile);
            }
            try
            {
                var baselineAssemblyVersion = AssemblyName.GetAssemblyName(testFileName);
                baselineAssemblyVersions.Add(fileName, baselineAssemblyVersion.Version);
            }
            catch (BadImageFormatException)
            {
                Assert.Throws<BadImageFormatException>(() => AssemblyName.GetAssemblyName(baselineFileName));
                break;
            }
            var testAssemblyVersion = AssemblyName.GetAssemblyName(baselineFileName);
            testAssemblyVersions.Add(fileName, testAssemblyVersion.Version);

            File.Delete(baselineFileName);
            File.Delete(testFileName);
        }

        string UbVersionsFileName = packageName + "_ub_assemblyversions.txt";
        AssemblyVersionHelpers.WriteAssemblyVersionsToFile(testAssemblyVersions, UbVersionsFileName);

        string MsftVersionsFileName = packageName + "_msft_assemblyversions.txt";
        AssemblyVersionHelpers.WriteAssemblyVersionsToFile(baselineAssemblyVersions, MsftVersionsFileName);

        string diff = BaselineHelper.DiffFiles(MsftVersionsFileName, UbVersionsFileName, OutputHelper);
        diff = SdkContentTests.RemoveDiffMarkers(diff);
        BaselineHelper.CompareBaselineContents($"MsftToUb_{packageName}.diff", diff, Config.LogsDirectory, OutputHelper, Config.WarnOnContentDiffs);
    }
}

