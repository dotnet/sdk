// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest;
using NuGet.Packaging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.CommandLine;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

public class AssetComparer : BuildComparer
{
    public AssetComparer(
        bool clean,
        AssetType? assetType,
        string vmrManifestPath,
        string vmrAssetBasePath,
        string baseBuildAssetBasePath,
        string issuesReportPath,
        string noIssuesReportPath,
        int parallelTasks,
        string baselineFilePath)
        : base(
            clean,
            assetType,
            vmrManifestPath,
            vmrAssetBasePath,
            baseBuildAssetBasePath,
            issuesReportPath,
            noIssuesReportPath,
            parallelTasks,
            baselineFilePath,
            issuesToReport: new List<IssueType>
            {
                IssueType.MissingShipping,
                IssueType.MissingNonShipping,
                IssueType.MisclassifiedAsset,
                IssueType.AssemblyVersionMismatch,
                IssueType.PackageTFMs,
                IssueType.PackageDependencies,
                IssueType.PackageMetadataDifference,
                IssueType.MissingPackageContent,
                IssueType.ExtraPackageContent
            })
    { }

    /// <summary>
    /// Evaluates a single asset mapping for issues based on its type (Package or Blob).
    /// </summary>
    /// <param name="mapping">Asset mapping to evaluate</param>
    protected override async Task EvaluateAsset(AssetMapping mapping)
    {
        if (mapping.AssetType == AssetType.Package)
        {
            await EvaluatePackage(mapping);
        }
        else if (mapping.AssetType == AssetType.Blob)
        {
            await EvaluateBlob(mapping);
        }
    }

    /// <summary>
    /// Evaluates a single package mapping for issues.
    /// </summary>
    /// <param name="mapping">The package asset mapping to evaluate.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    private async Task EvaluatePackage(AssetMapping mapping)
    {
        try
        {
            await _throttle.WaitAsync();

            Console.WriteLine($"Evaluating '{mapping.Id}.");

            // Filter away mappings that we do not care about
            if (mapping.IsIgnorablePackage)
            {
                return;
            }

            // Check if the package is missing in the VMR
            else if (!mapping.DiffElementFound)
            {
                mapping.Issues.Add(new Issue
                {
                    IssueType = mapping.BaseBuildManifestElement.Attribute("NonShipping")?.Value == "true" ? IssueType.MissingNonShipping : IssueType.MissingShipping,
                    Description = $"Package '{mapping.Id}' is missing in the VMR."
                });

                return;
            }

            EvaluateClassification(mapping);
            await EvaluatePackageContents(mapping);
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
        finally
        {
            _throttle.Release();
        }
    }

    static readonly ImmutableArray<string> IncludedAssemblyNameCheckFileExtensions = [".dll", ".exe"];


    /// <summary>
    /// Evaluate the contents of a mapping between two packages.
    /// </summary>
    /// <param name="mapping">Package mapping to evaluate</param>
    public async Task EvaluatePackageContents(AssetMapping mapping)
    {
        var diffNugetPackagePath = mapping.DiffFilePath;
        var baselineNugetPackagePath = mapping.BaseBuildFilePath;

        // If either of the paths don't exist, we can't run this comparison
        if (diffNugetPackagePath == null || baselineNugetPackagePath == null)
        {
            return;
        }

        try
        {
            using (PackageArchiveReader diffPackageReader = new PackageArchiveReader(File.OpenRead(diffNugetPackagePath)))
            {
                using (PackageArchiveReader baselinePackageReader = new PackageArchiveReader(baselineNugetPackagePath))
                {
                    await ComparePackageFileLists(mapping, diffPackageReader, baselinePackageReader);
                    await ComparePackageAssemblyVersions(mapping, diffPackageReader, baselinePackageReader);
                    await ComparePackageMetadata(mapping, diffPackageReader, baselinePackageReader);
                }
            }
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
    }

    /// <summary>
    /// Compare nuspecs for meaningful equality
    /// </summary>
    /// <param name="mapping">Mapping to compare</param>
    /// <param name="diffPackageReader">Diff (VMR) package reader</param>
    /// <param name="baselinePackageReader">Baseline package reader</param>
    private async Task ComparePackageMetadata(AssetMapping mapping, PackageArchiveReader diffPackageReader, PackageArchiveReader baselinePackageReader)
    {
        try
        {
            var diffNuspecReader = await diffPackageReader.GetNuspecReaderAsync(CancellationToken.None);
            var baseNuspecReader = await baselinePackageReader.GetNuspecReaderAsync(CancellationToken.None);

            // Compare basic fields
            ComparePackageMetadataStringField(mapping, "Authors", baseNuspecReader.GetAuthors(), diffNuspecReader.GetAuthors());
            ComparePackageMetadataStringField(mapping, "ProjectUrl", baseNuspecReader.GetProjectUrl()?.ToString(), diffNuspecReader.GetProjectUrl()?.ToString());
            ComparePackageMetadataStringField(mapping, "LicenseUrl", baseNuspecReader.GetLicenseUrl()?.ToString(), diffNuspecReader.GetLicenseUrl()?.ToString());
            ComparePackageMetadataStringField(mapping, "Copyright", baseNuspecReader.GetCopyright(), diffNuspecReader.GetCopyright());
            ComparePackageMetadataStringField(mapping, "Tags", baseNuspecReader.GetTags(), diffNuspecReader.GetTags());

            // Compare target frameworks
            var baseGroups = baseNuspecReader.GetDependencyGroups().ToList();
            var diffGroups = diffNuspecReader.GetDependencyGroups().ToList();

            var baseTfms = new HashSet<string>(baseGroups.Select(g => g.TargetFramework.GetShortFolderName()), StringComparer.OrdinalIgnoreCase);
            var diffTfms = new HashSet<string>(diffGroups.Select(g => g.TargetFramework.GetShortFolderName()), StringComparer.OrdinalIgnoreCase);

            if (!baseTfms.SetEquals(diffTfms))
            {
                mapping.Issues.Add(new Issue
                {
                    IssueType = IssueType.PackageTFMs,
                    Description = $"Package target frameworks differ: base={string.Join(",", baseTfms)}, diff={string.Join(",", diffTfms)}"
                });
            }

            // Compare dependencies within matching TFMs (ignore version differences)
            foreach (var tfm in baseTfms.Intersect(diffTfms, StringComparer.OrdinalIgnoreCase))
            {
                var baseDeps = baseGroups.First(g => g.TargetFramework.GetShortFolderName().Equals(tfm, StringComparison.OrdinalIgnoreCase)).Packages;
                var diffDeps = diffGroups.First(g => g.TargetFramework.GetShortFolderName().Equals(tfm, StringComparison.OrdinalIgnoreCase)).Packages;

                var baseDepIds = new HashSet<string>(baseDeps.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
                var diffDepIds = new HashSet<string>(diffDeps.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);

                if (!baseDepIds.SetEquals(diffDepIds))
                {
                    var missingInDiff = baseDepIds.Except(diffDepIds);
                    var extraInDiff = diffDepIds.Except(baseDepIds);

                    mapping.Issues.Add(new Issue
                    {
                        IssueType = IssueType.PackageDependencies,
                        Description = $"Package dependencies differ in TFM '{tfm}'. "
                                     + $"Missing from diff: {string.Join(", ", missingInDiff)}; "
                                     + $"Extra in diff: {string.Join(", ", extraInDiff)}."
                    });
                }
            }
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
    }

    private void ComparePackageMetadataStringField(AssetMapping mapping, string fieldName, string baseValue, string diffValue)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(baseValue ?? string.Empty, diffValue ?? string.Empty))
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.PackageMetadataDifference, // Reuse or define new IssueType if needed
                Description = $"Package nuspec '{fieldName}': base='{baseValue}' vs diff='{diffValue}'"
            });
        }
    }

    /// <summary>
    /// Compare the file lists of packages, identifying missing and extra files.
    /// </summary>
    /// <param name="mapping">Asset mapping to compare lists for</param>
    /// <param name="diffPackageReader">Diff (VMR) package reader</param>
    /// <param name="basePackageReader">Baseline (old build) package reader</param>
    private async Task ComparePackageFileLists(AssetMapping mapping, PackageArchiveReader diffPackageReader, PackageArchiveReader basePackageReader)
    {
        IEnumerable<string> baselineFiles = (await basePackageReader.GetFilesAsync(CancellationToken.None));
        IEnumerable<string> testFiles = (await diffPackageReader.GetFilesAsync(CancellationToken.None));

        // Strip down the baseline and test files to remove version numbers.
        var strippedBaselineFiles = baselineFiles.Select(f => f.RemoveVersionsNormalized()).ToList();
        var strippedTestFiles = testFiles.Select(f => f.RemoveVersionsNormalized()).ToList();

        var missingFiles = Utils.RemovePackageFilesToIgnore(strippedBaselineFiles.Except(strippedTestFiles));

        foreach (var missingFile in missingFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.MissingPackageContent,
                Description = missingFile,
            });
        }

        // Compare the other way, and identify content in the VMR that is not in the baseline
        var extraFiles = Utils.RemovePackageFilesToIgnore(strippedTestFiles.Except(strippedBaselineFiles));

        foreach (var extraFile in extraFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.ExtraPackageContent,
                Description = extraFile
            });
        }
    }

    /// <summary>
    /// Compares the assembly versions of the files in the test and baseline packages.
    /// </summary>
    /// <param name="mapping">Mapping to evaluate</param>
    /// <param name="diffPackageReader">Diff (VMR) package reader</param>
    /// <param name="basePackageReader">Baseline (old build) package reader</param>
    private static async Task ComparePackageAssemblyVersions(AssetMapping mapping, PackageArchiveReader diffPackageReader, PackageArchiveReader basePackageReader)
    {
        IEnumerable<string> baselineFiles = (await basePackageReader.GetFilesAsync(CancellationToken.None)).Where(f => IncludedAssemblyNameCheckFileExtensions.Contains(Path.GetExtension(f)));
        IEnumerable<string> testFiles = (await diffPackageReader.GetFilesAsync(CancellationToken.None)).Where(f => IncludedAssemblyNameCheckFileExtensions.Contains(Path.GetExtension(f)));
        foreach (var fileName in baselineFiles.Intersect(testFiles))
        {
            try
            {
                using var baselineStream = await CopyStreamToSeekableStreamAsync(basePackageReader.GetEntry(fileName).Open());
                using var testStream = await CopyStreamToSeekableStreamAsync(diffPackageReader.GetEntry(fileName).Open());

                CompareAssemblyVersions(mapping, fileName, baselineStream, testStream);
            }
            catch (Exception e)
            {
                mapping.EvaluationErrors.Add(e.ToString());
            }
        }
    }

    /// <summary>
    /// Copies a stream from an archive to a seekable stream (MemoryStream).
    /// </summary>
    /// <param name="stream">Stream to copy</param>
    /// <returns>Memory stream containing the stream contents.</returns>
    private static async Task<Stream> CopyStreamToSeekableStreamAsync(Stream stream)
    {
        var outputStream = new MemoryStream();
        await stream.CopyToAsync(outputStream, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        outputStream.Position = 0;
        return outputStream;
    }

    private static AssemblyName GetAssemblyName(Stream stream, string fileName)
    {
        using (var peReader = new PEReader(stream))
        {
            if (!peReader.HasMetadata)
            {
                return null;
            }

            var metadataReader = peReader.GetMetadataReader();
            var assemblyDefinition = metadataReader.GetAssemblyDefinition();
            var assemblyName = assemblyDefinition.GetAssemblyName();

            return assemblyName;
        }
    }

    /// <summary>
    /// Evaluates the classification of an asset mapping. Is it correctly marked shipping or non-shipping?
    /// </summary>
    /// <param name="mapping">Mapping to evaluate</param>
    private static void EvaluateClassification(AssetMapping mapping)
    {
        // Check for misclassification
        bool isBaseShipping = mapping.BaseBuildManifestElement.Attribute("NonShipping")?.Value != "true";
        bool isDiffShipping = mapping.DiffManifestElement.Attribute("NonShipping")?.Value != "true";

        if (isBaseShipping != isDiffShipping)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.MisclassifiedAsset,
                Description = $"Asset '{mapping.Id}' is misclassified in the VMR. Base build is {(isBaseShipping ? "shipping" : "nonshipping")} and VMR build is {(isDiffShipping ? "shipping" : "nonshipping")}"
            });
        }
    }

    /// <summary>
    /// Evaluates a single blob mapping for issues.
    /// </summary>
    /// <param name="mapping">Blob mapping to evaluate</param>
    private async Task EvaluateBlob(AssetMapping mapping)
    {
        try
        {
            await _throttle.WaitAsync();

            Console.WriteLine($"Evaluating '{mapping.Id}'");

            // Filter away mappings that we do not care about
            if (mapping.IsIgnorableBlob)
            {
                return;
            }

            // Check if the package is missing in the VMR
            if (!mapping.DiffElementFound)
            {
                mapping.Issues.Add(new Issue
                {
                    IssueType = mapping.BaseBuildManifestElement.Attribute("NonShipping")?.Value == "true" ? IssueType.MissingNonShipping : IssueType.MissingShipping,
                    Description = $"Blob '{mapping.Id}' is missing in the VMR."
                });
                return;
            }

            // Asset is found. Perform tests.
            EvaluateClassification(mapping);
            await EvaluateBlobContents(mapping);
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
        finally
        {
            _throttle.Release();
        }
    }

    public async Task EvaluateBlobContents(AssetMapping mapping)
    {
        // Switch on the file type, and call a helper based on the type

        if (mapping.Id.EndsWith(".zip"))
        {
            await CompareZipArchiveContents(mapping);
        }
        else if (mapping.Id.EndsWith(".tar.gz") || mapping.Id.EndsWith(".tgz"))
        {
            await CompareTarArchiveContents(mapping);
        }
    }
    private async Task CompareTarArchiveContents(AssetMapping mapping)
    {
        var diffTarPath = mapping.DiffFilePath;
        var baselineTarPath = mapping.BaseBuildFilePath;
        // If either of the paths don't exist, we can't run this comparison
        if (diffTarPath == null || baselineTarPath == null)
        {
            return;
        }

        try
        {
            // Get the file lists for the baseline and diff tar files
            IEnumerable<string> baselineFiles = GetTarGzArchiveFileList(baselineTarPath);
            IEnumerable<string> diffFiles = GetTarGzArchiveFileList(diffTarPath);

            // Compare file lists
            CompareBlobArchiveFileLists(mapping, baselineFiles, diffFiles);

            // Compare assembly versions
            await CompareTarGzAssemblyVersions(mapping, baselineFiles, diffFiles);
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
    }

    /// <summary>
    /// Retrieve the file list from a tar.gz file.
    /// </summary>
    /// <param name="archivePath"></param>
    /// <returns></returns>
    private List<string> GetTarGzArchiveFileList(string archivePath)
    {
        List<string> entries = new();
        using (FileStream fileStream = File.OpenRead(archivePath))
        {
            using (GZipStream gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (TarReader reader = new TarReader(gzipStream))
            {
                TarEntry entry;
                while ((entry = reader.GetNextEntry()) != null)
                {
                    entries.Add(entry.Name);
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Compare the assembly versions in a tar.gz file.
    /// </summary>
    /// <param name="mapping">Mapping to compare</param>
    /// <param name="baselineFiles">Files existing in the baseline archive</param>
    /// <param name="diffFiles">Files existing in the diff archive</param>
    private async Task CompareTarGzAssemblyVersions(AssetMapping mapping, IEnumerable<string> baselineFiles, IEnumerable<string> diffFiles)
    {
        // Get the list of common files and create a map of file->stream
        var strippedBaselineFiles = baselineFiles.Select(f => f.RemoveVersionsNormalized()).ToList();
        var strippedDiffFiles = diffFiles.Select(f => f.RemoveVersionsNormalized()).ToList();

        var commonFiles = strippedBaselineFiles.Intersect(strippedDiffFiles).ToHashSet();

        var baselineStreams = new Dictionary<string, Stream>();
        var diffStreams = new Dictionary<string, Stream>();

        using (FileStream baseStream = File.OpenRead(mapping.BaseBuildFilePath))
        {
            using (FileStream diffStream = File.OpenRead(mapping.DiffFilePath))
            {
                using (GZipStream baseGzipStream = new GZipStream(baseStream, CompressionMode.Decompress))
                using (TarReader baseReader = new TarReader(baseGzipStream))
                {
                    using (GZipStream diffGzipStream = new GZipStream(diffStream, CompressionMode.Decompress))
                    using (TarReader diffReader = new TarReader(diffGzipStream))
                    {
                        string nextBaseEntry = null;
                        string nextDiffEntry = null;
                        do
                        {
                            nextBaseEntry = await WalkNextCommon(commonFiles, baseReader, baselineStreams);
                            if (nextBaseEntry != null)
                            {
                                CompareAvailableStreams(mapping, baselineStreams, diffStreams, nextBaseEntry);
                            }

                            nextDiffEntry = await WalkNextCommon(commonFiles, diffReader, diffStreams);
                            if (nextDiffEntry != null)
                            {
                                CompareAvailableStreams(mapping, baselineStreams, diffStreams, nextDiffEntry);
                            }
                        }
                        while (nextBaseEntry != null || nextDiffEntry != null);

                        // If there are any remaining streams, create an evaluation error
                        if (baselineStreams.Count > 0 || diffStreams.Count > 0)
                        {
                            mapping.EvaluationErrors.Add("Failed to compare all tar entries.");
                        }
                    }
                }
            }
        }

        // Walk the tar to the next entry that exists in both the base and the diff
        static async Task<string> WalkNextCommon(HashSet<string> commonFiles, TarReader reader, Dictionary<string, Stream> streams)
        {
            TarEntry baseEntry;
            while ((baseEntry = reader.GetNextEntry()) != null && baseEntry.DataStream != null)
            {
                string entryStripped = baseEntry.Name.RemoveVersionsNormalized();
                // If the element lives in the common files hash set, then copy it to a memory stream.
                // Do not close the stream.
                if (commonFiles.Contains(entryStripped))
                {
                    streams[entryStripped] = await CopyStreamToSeekableStreamAsync(baseEntry.DataStream);
                    return entryStripped;
                }
            }
            return null;
        }

        // Given we have a new entry that is common between base and diff, attempt to do some comparisons.
        void CompareAvailableStreams(AssetMapping mapping, Dictionary<string, Stream> baselineStreams, Dictionary<string, Stream> diffStreams,
             string entry)
        {
            if (baselineStreams.TryGetValue(entry, out var baselineFileStream) &&
                diffStreams.TryGetValue(entry, out var diffFileStream))
            {
                CompareAssemblyVersions(mapping, entry, baselineFileStream, diffFileStream);
                baselineFileStream.Dispose();
                diffFileStream.Dispose();
                baselineStreams.Remove(entry);
                diffStreams.Remove(entry);
            }
        }
    }

    private async Task CompareZipArchiveContents(AssetMapping mapping)
    {
        var diffZipPath = mapping.DiffFilePath;
        var baselineZipPath = mapping.BaseBuildFilePath;
        // If either of the paths don't exist, we can't run this comparison
        if (diffZipPath == null || baselineZipPath == null)
        {
            return;
        }
        try
        {
            using (var diffStream = File.OpenRead(diffZipPath))
            using (var baselineStream = File.OpenRead(baselineZipPath))
            {
                using (var diffArchive = new ZipArchive(diffStream, ZipArchiveMode.Read))
                using (var baselineArchive = new ZipArchive(baselineStream, ZipArchiveMode.Read))
                {
                    CompareZipFileLists(mapping, diffArchive, baselineArchive);
                    await CompareZipAssemblyVersions(mapping, diffArchive, baselineArchive);
                }
            }
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
    }

    private void CompareZipFileLists(AssetMapping mapping, ZipArchive diffArchive, ZipArchive baselineArchive)
    {
        IEnumerable<string> baselineFiles = baselineArchive.Entries.Select(e => e.FullName);
        IEnumerable<string> diffFiles = diffArchive.Entries.Select(e => e.FullName);

        CompareBlobArchiveFileLists(mapping, baselineFiles, diffFiles);
    }

    private async Task CompareZipAssemblyVersions(AssetMapping mapping, ZipArchive diffArchive, ZipArchive baselineArchive)
    {
        IEnumerable<string> baselineFiles = baselineArchive.Entries.Select(e => e.FullName).Where(f => IncludedAssemblyNameCheckFileExtensions.Contains(Path.GetExtension(f)));
        IEnumerable<string> diffFiles = diffArchive.Entries.Select(e => e.FullName).Where(f => IncludedAssemblyNameCheckFileExtensions.Contains(Path.GetExtension(f)));
        foreach (var fileName in baselineFiles.Intersect(diffFiles))
        {
            try
            {
                using var baselineStream = await CopyStreamToSeekableStreamAsync(baselineArchive.GetEntry(fileName).Open());
                using var testStream = await CopyStreamToSeekableStreamAsync(diffArchive.GetEntry(fileName).Open());
                
                CompareAssemblyVersions(mapping, fileName, baselineStream, testStream);
            }
            catch (Exception e)
            {
                mapping.EvaluationErrors.Add(e.ToString());
            }
        }
    }

    private static void CompareAssemblyVersions(AssetMapping mapping, string fileName, Stream baselineStream, Stream testStream)
    {
        AssemblyName baselineAssemblyName = null;
        try
        {
            baselineAssemblyName = GetAssemblyName(baselineStream, fileName);
        }
        catch (BadImageFormatException)
        {
            // Assume the file is not an assembly, and then don't attempt for the test assembly
            return;
        }
        AssemblyName testAssemblyName = GetAssemblyName(testStream, fileName);
        if ((baselineAssemblyName == null) != (testAssemblyName == null))
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.AssemblyVersionMismatch,
                Description = $"Assembly '{fileName}' in {mapping.AssetType.ToString().ToLowerInvariant()} '{mapping.Id}' has different but unknown versions in the VMR and base build."
            });
        }
        else if (baselineAssemblyName == null && testAssemblyName == null)
        {
            return;
        }

        if (baselineAssemblyName.ToString() != testAssemblyName.ToString())
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.AssemblyVersionMismatch,
                Description = $"Assembly '{fileName}' in {mapping.AssetType.ToString().ToLowerInvariant()} '{mapping.Id}'. " +
                    $"VMR version: {baselineAssemblyName}, base build version: {testAssemblyName}"
            });
        }
    }

    private static void CompareBlobArchiveFileLists(AssetMapping mapping, IEnumerable<string> baselineFiles, IEnumerable<string> diffFiles)
    {
        // Because these typically contain version numbers in their paths, we need to go and remove those.

        var strippedBaselineFiles = baselineFiles.Select(f => f.RemoveVersionsNormalized()).ToList();
        var strippedDiffFiles = diffFiles.Select(f => f.RemoveVersionsNormalized()).ToList();

        var missingFiles = strippedBaselineFiles.Except(strippedDiffFiles);
        foreach (var missingFile in missingFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.MissingPackageContent,
                Description = missingFile
            });
        }
        // Compare the other way, and identify content in the VMR that is not in the baseline
        var extraFiles = strippedDiffFiles.Except(strippedBaselineFiles);
        foreach (var extraFile in extraFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.ExtraPackageContent,
                Description = extraFile
            });
        }
    }
}
