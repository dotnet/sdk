﻿using Microsoft.DotNet.VersionTools.BuildManifest;
using NuGet.Packaging;
using System.Collections.Immutable;
using System.CommandLine;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

/// <summary>
/// Tool for comparing Microsoft builds with VMR (Virtual Mono Repo) builds.
/// Identifies missing assets, misclassified assets, and assembly version mismatches.
/// </summary>
public class Program
{
    /// <summary>
    /// Entry point for the build comparison tool.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Return code indicating success (0) or failure (non-zero).</returns>
    static int Main(string[] args)
    {
        var vmrManifestPathArgument = new CliOption<string>("-vmrManifestPath")
        {
            Description = "Path to the manifest file",
            Required = true
        };
        var vmrAssetBasePathArgument = new CliOption<string>("-vmrAssetBasePath")
        {
            Description = "Path to the manifest file",
            Required = true
        };
        var msftAssetBasePathArgument = new CliOption<string>("-msftAssetBasePath")
        {
            Description = "Path to the asset base path",
            Required = true
        };
        var outputFileArgument = new CliOption<string>("-report")
        {
            Description = "Path to output xml file.",
            Required = true
        };
        var parallelismArgument = new CliOption<int>("-parallel")
        {
            Description = "Amount of parallelism used while analyzing the builds.",
            DefaultValueFactory = _ => 8,
            Required = true
        };
        var baselineArgument = new CliOption<string>("-baseline")
        {
            Description = "Path to the baseline build manifest.",
            Required = true
        };
        var rootCommand = new CliRootCommand(description: "Tool for comparing Microsoft builds with VMR builds.")
        {
            vmrManifestPathArgument,
            vmrAssetBasePathArgument,
            msftAssetBasePathArgument,
            outputFileArgument,
            baselineArgument,
            parallelismArgument
        };

        rootCommand.Description = "Compares build manifests and outputs missing or misclassified assets.";

        var result = rootCommand.Parse(args);
        var comparer = new Program(result.GetValue(vmrManifestPathArgument),
                                    result.GetValue(vmrAssetBasePathArgument),
                                    result.GetValue(msftAssetBasePathArgument),
                                    result.GetValue(outputFileArgument),
                                    result.GetValue(baselineArgument),
                                    result.GetValue(parallelismArgument));



        return (int)comparer.CompareBuilds().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Path to the VMR manifest file.
    /// </summary>
    private string _vmrManifestPath;
    
    /// <summary>
    /// Base path for VMR build assets.
    /// </summary>
    private string _vmrBuildAssetBasePath;
    
    /// <summary>
    /// Base path for Microsoft build assets.
    /// </summary>
    private string _baseBuildAssetBasePath;
    
    /// <summary>
    /// Path where the comparison report will be saved.
    /// </summary>
    private string _outputFilePath;
    
    /// <summary>
    /// Semaphore used to control parallel processing.
    /// </summary>
    private SemaphoreSlim _throttle;
    
    /// <summary>
    /// Report containing the results of the comparison.
    /// </summary>
    private ComparisonReport _comparisonReport = new ComparisonReport();
    
    /// <summary>
    /// List of all asset mappings between base and VMR builds.
    /// </summary>
    private List<AssetMapping> _assetMappings = new List<AssetMapping>();

    private Baseline _baseline;

    /// <summary>
    /// Initializes a new instance of the Program class with specified parameters.
    /// </summary>
    /// <param name="vmrManifestPath">Path to the VMR manifest file.</param>
    /// <param name="vmrAssetBasePath">Base path for VMR build assets.</param>
    /// <param name="baseBuildAssetBasePath">Base path for Microsoft build assets.</param>
    /// <param name="outputFilePath">Path where the comparison report will be saved.</param>
    /// <param name="parallelTasks">Number of tasks to run in parallel.</param>
    private Program(string vmrManifestPath,
                    string vmrAssetBasePath,
                    string baseBuildAssetBasePath,
                    string outputFilePath,
                    string baselineFilePath,
                    int parallelTasks)
    {
        _vmrManifestPath = vmrManifestPath;
        _vmrBuildAssetBasePath = vmrAssetBasePath;
        _baseBuildAssetBasePath = baseBuildAssetBasePath;
        _outputFilePath = outputFilePath;
        _throttle = new SemaphoreSlim(parallelTasks, parallelTasks);

        if (!string.IsNullOrEmpty(baselineFilePath))
        {
            _baseline = new Baseline(baselineFilePath);
        }
    }

    /// <summary>
    /// Executes the build comparison process.
    /// </summary>
    /// <returns>Task representing the asynchronous operation with a return code: 0 for success, 1 for failure.</returns>
    private async Task<int> CompareBuilds()
    {
        try
        {
            GenerateAssetMappings();
            await EvaluateAssets();
            ApplyBaselines();
            GenerateReport();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.ToString()}");
            return 1;
        }
    }

    private void ApplyBaselines()
    {
        if (_baseline == null)
        {
            return;
        }

        Console.WriteLine($"Applying baseline.");

        foreach (var mapping in _assetMappings)
        {
            foreach (var issue in mapping.Issues)
            {
                issue.Baseline = _baseline.GetMatchingBaselineEntries(issue, mapping).FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Evaluates all asset mappings by processing packages and blobs in parallel.
    /// </summary>
    /// <returns>Task representing the asynchronous operation.</returns>
    private async Task EvaluateAssets()
    {
        var evaluationTasks = _assetMappings.Select(mapping => Task.Run(async () =>
        {
            await EvaluateAsset(mapping);
        }));

        await Task.WhenAll(evaluationTasks);
    }

    /// <summary>
    /// Generates asset mappings between base builds and VMR builds.
    /// </summary>
    /// <remarks>
    /// Walks through each repository's merged manifest and maps files between
    /// the base build and VMR build based on asset IDs.
    /// </remarks>
    private void GenerateAssetMappings()
    {
        Console.WriteLine($"Loading VMR manifest from {_vmrManifestPath}");

        // Load the XML file
        XDocument vmrMergedManifestContent = XDocument.Load(_vmrManifestPath);

        // Get all files in the assets folder, including subfolders
        var allFiles = Directory.GetFiles(_baseBuildAssetBasePath, "*", SearchOption.AllDirectories);

        // Walk the top-level directories of the asset base path, and find the MergedManifest under each
        // one. The MergedManifest.xml contains the list of outputs produced by the repo.

        foreach (var baseDirectory in Directory.GetDirectories(_baseBuildAssetBasePath, "*", SearchOption.TopDirectoryOnly))
        {
            // Find the merged manifest underneath this directory
            // (e.g. <assetBasePath>/arcade/nonshipping/<version>>/MergedManifest.xml)

            string repoMergedManifestPath = Directory.GetFiles(baseDirectory,
                "MergedManifest.xml", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (repoMergedManifestPath == null)
            {
                Console.WriteLine($"Failed to find merged manifest for {baseDirectory}");
                continue;
            }

            _assetMappings.AddRange(MapFilesForManifest(vmrMergedManifestContent,
                                                       baseDirectory,
                                                       _vmrBuildAssetBasePath,
                                                       repoMergedManifestPath));
        }
    }

    /// <summary>
    /// Generates the final comparison report and saves it to the specified output file.
    /// </summary>
    private void GenerateReport()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_outputFilePath));

        // Bucketize asset mappings
        _comparisonReport.AssetsWithoutIssues = _assetMappings
            .Where(mapping => !mapping.Issues.Any(i => i.Baseline == null) && !mapping.EvaluationErrors.Any())
            .ToList();

        _comparisonReport.AssetsWithErrors = _assetMappings
            .Where(mapping => mapping.EvaluationErrors.Any())
            .ToList();

        _comparisonReport.AssetsWithIssues = _assetMappings
            .Where(mapping => mapping.Issues.Any(issue => issue.Baseline == null))
            .OrderByDescending(mapping => mapping.Issues.Count(issue => issue.Baseline == null))
            .ToList();

        // Sort issues within each mapping
        foreach (var mapping in _comparisonReport.AssetsWithIssues)
        {
            mapping.Issues = mapping.Issues
                .OrderBy(issue => issue.Baseline != null)
                .ThenBy(issue => issue.IssueType)
                .ToList();
        }

        // Serialize all asset mappings to xml
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ComparisonReport));
        using (var stream = new FileStream(_outputFilePath, FileMode.Create))
        {
            serializer.Serialize(stream, _comparisonReport);
            stream.Close();
        }

        Console.WriteLine($"Comparison report saved to {_outputFilePath}");
        Console.WriteLine($"Errors: {_comparisonReport.ErrorCount}");
        Console.WriteLine($"Issues: {_comparisonReport.IssueCount}");
        Console.WriteLine($"Baselined issues: {_comparisonReport.BaselineCount}");
    }

    /// <summary>
    /// Evaluates a single asset mapping for issues based on its type (Package or Blob).
    /// </summary>
    /// <param name="mapping"></param>
    /// <returns></returns>
    private async Task EvaluateAsset(AssetMapping mapping)
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
            if (mapping.Id.Contains("Microsoft.SourceBuild.Intermediate"))
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
    /// <param name="mapping"></param>
    /// <returns></returns>
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
            using (PackageArchiveReader testPackageReader = new PackageArchiveReader(File.OpenRead(diffNugetPackagePath)))
            {
                using (PackageArchiveReader baselinePackageReader = new PackageArchiveReader(baselineNugetPackagePath))
                {
                    ComparePackageFileLists(mapping, testPackageReader, baselinePackageReader);
                    await ComparePackageAssemblyVersions(mapping, testPackageReader, baselinePackageReader);
                }
            }
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }
    }

    /// <summary>
    /// Compare the file lists of packages, identifying missing and extra files.
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="testPackageReader"></param>
    /// <param name="baselinePackageReader"></param>
    private async void ComparePackageFileLists(AssetMapping mapping, PackageArchiveReader testPackageReader, PackageArchiveReader baselinePackageReader)
    {
        IEnumerable<string> baselineFiles = (await baselinePackageReader.GetFilesAsync(CancellationToken.None));
        IEnumerable<string> testFiles = (await testPackageReader.GetFilesAsync(CancellationToken.None));

        // Strip down the baseline and test files to remove version numbers.
        var strippedBaselineFiles = baselineFiles.Select(f => RemoveVersionsNormalized(f)).ToList();
        var strippedTestFiles = testFiles.Select(f => RemoveVersionsNormalized(f)).ToList();

        var missingFiles = RemovePackageFilesToIgnore(strippedBaselineFiles.Except(strippedTestFiles));

        foreach (var missingFile in missingFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.MissingPackageContent,
                Description = missingFile,
            });
        }

        // Compare the other way, and identify content in the VMR that is not in the baseline
        var extraFiles = RemovePackageFilesToIgnore(strippedTestFiles.Except(strippedBaselineFiles));

        foreach (var extraFile in extraFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.ExtraPackageContent,
                Description = extraFile
            });
        }

        static IEnumerable<string> RemovePackageFilesToIgnore(IEnumerable<string> files)
        {
            return files.Where(f => !f.EndsWith(".signature.p7s", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Compares the assembly versions of the files in the test and baseline packages.
    /// </summary>
    /// <param name="mapping">Mapping to evaluate</param>
    /// <param name="packageName"></param>
    /// <param name="testPackageReader"></param>
    /// <param name="baselinePackageReader"></param>
    /// <returns></returns>
    private static async Task ComparePackageAssemblyVersions(AssetMapping mapping, PackageArchiveReader testPackageReader, PackageArchiveReader baselinePackageReader)
    {
        IEnumerable<string> baselineFiles = (await baselinePackageReader.GetFilesAsync(CancellationToken.None)).Where(f => IncludedAssemblyNameCheckFileExtensions.Contains(Path.GetExtension(f)));
        IEnumerable<string> testFiles = (await testPackageReader.GetFilesAsync(CancellationToken.None)).Where(f => IncludedAssemblyNameCheckFileExtensions.Contains(Path.GetExtension(f)));
        foreach (var fileName in baselineFiles.Intersect(testFiles))
        {
            try
            {
                using var baselineStream = await CopyStreamToSeekableStreamAsync(baselinePackageReader.GetEntry(fileName).Open());
                using var testStream = await CopyStreamToSeekableStreamAsync(testPackageReader.GetEntry(fileName).Open());

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
    /// <param name="stream"></param>
    /// <returns></returns>
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
    /// <param name="mapping"></param>
    /// <returns></returns>
    private async Task EvaluateBlob(AssetMapping mapping)
    {
        try
        {
            await _throttle.WaitAsync();

            Console.WriteLine($"Evaluating '{mapping.Id}'");

            // Filter away mappings that we do not care about
            if (mapping.Id.Contains(".wixpack.zip"))
            {
                return;
            }
            if (mapping.Id.Contains("MergedManifest.xml"))
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
    /// This method is called "USE ALL AVAILABLE MEMORY"
    /// </summary>
    /// <param name="mapping"></param>
    /// <param name="baselineFiles"></param>
    /// <param name="diffFiles"></param>
    /// <returns></returns>
    private async Task CompareTarGzAssemblyVersions(AssetMapping mapping, IEnumerable<string> baselineFiles, IEnumerable<string> diffFiles)
    {
        // Get the list of common files and create a map of file->stream
        var strippedBaselineFiles = baselineFiles.Select(f => RemoveVersionsNormalized(f)).ToList();
        var strippedDiffFiles = diffFiles.Select(f => RemoveVersionsNormalized(f)).ToList();

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
                string entryStripped = RemoveVersionsNormalized(baseEntry.Name);
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

    private static string RemoveVersionsNormalized(string path)
    {
        string strippedPath = path.Replace("\\", "//");
        string prevPath = path;
        do
        {
            prevPath = strippedPath;
            strippedPath = VersionIdentifier.RemoveVersions(strippedPath);
        } while (prevPath != strippedPath);

        return strippedPath;
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
                Description = $"Assembly '{fileName}' in blob '{mapping.Id}' has different versions in the VMR and base build."
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
                Description = $"Assembly '{fileName}' in blob '{mapping.Id}' has different versions in the VMR and base build. VMR version: {baselineAssemblyName}, base build version: {testAssemblyName}"
            });
        }
    }

    private static void CompareBlobArchiveFileLists(AssetMapping mapping, IEnumerable<string> baselineFiles, IEnumerable<string> diffFiles)
    {
        // Because these typically contain version numbers in their paths, we need to go and remove those.

        var strippedBaselineFiles = baselineFiles.Select(f => RemoveVersionsNormalized(f)).ToList();
        var strippedDiffFiles = diffFiles.Select(f => RemoveVersionsNormalized(f)).ToList();

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

    /// <summary>
    /// Maps files in the base build to the VMR build based on the merged manifest.
    /// </summary>
    /// <param name="vmrMergedManifestContent">VMR manifest</param>
    /// <param name="baseDirectory">Base build directory</param>
    /// <param name="diffDirectory">Base VMR build directory</param>
    /// <param name="repoMergedManifestPath">Path to merged manifest for the base build</param>
    /// <returns>List of asset mappings for the baseline manifest.</returns>
    private List<AssetMapping> MapFilesForManifest(XDocument vmrMergedManifestContent, string baseDirectory, string diffDirectory, string repoMergedManifestPath)
    {
        var repoBuildMergeManifestContent = XDocument.Load(repoMergedManifestPath);

        List<AssetMapping> assetMappings = new();
        Console.WriteLine($"Mapping base build outputs in {repoMergedManifestPath} to VMR.");

        // For each top level element, switch on the name of the element. Could be Blob or Package
        foreach (var element in repoBuildMergeManifestContent.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "Blob":
                    assetMappings.Add(MapBlob(vmrMergedManifestContent, element, baseDirectory, diffDirectory));
                    break;
                case "Package":
                    assetMappings.Add(MapPackage(vmrMergedManifestContent, element, baseDirectory, diffDirectory));
                    break;
                case "Pdb":
                    // NYI
                    // assetMappings.Add(MapPdb(vmrMergedManifestContent, element, baseDirectory, diffDirectory));
                    break;
                case "Build":
                case "SigningInformation":
                case "FileSignInfo":
                case "FileExtensionSignInfo":
                case "StrongNameSignInfo":
                case "CertificatesSignInfo":
                    // Nothing to do
                    break;
                default:
                    Console.WriteLine("Unknown type of top level element in repo merged manifest: " + element.Name);
                    break;
            }
        }

        return assetMappings;
    }

    private AssetMapping MapBlob(XDocument diffMergedManifestContent, XElement baseElement, string basePath, string diffPath)
    {
        string baseBlobId = baseElement.Attribute("Id")?.Value;
        string baseBlobFileName = Path.GetFileName(baseBlobId);
        string baseShippingPathElement = CalculateShippingPathElement(baseElement);
        string baseFilePath = Path.Combine(basePath, baseShippingPathElement, (!baseBlobId.StartsWith("assets") ? "assets" : ""), baseBlobId);

        if (!File.Exists(baseFilePath))
        {
            // Find the diff file path
            baseFilePath = null;
        }

        // To attempt to find the matching element, we use the version identifier to remove
        // the version number from the file name, then search for a file with the same name
        // in the target manifest. Use the version identifier on the full ID because it's
        // smarter in some cases using that.

        string baseVersion = VersionIdentifier.GetVersion(baseBlobId);
        string strippedBaseBlobFileName = baseBlobFileName;
        if (baseVersion != null)
        {
            strippedBaseBlobFileName = strippedBaseBlobFileName.Replace(baseVersion, string.Empty);
        }

        var diffBlobElement = diffMergedManifestContent.Descendants("Blob")
            .FirstOrDefault(p =>
            {
                string diffBlobId = p.Attribute("Id")?.Value;
                string diffBlobFileName = Path.GetFileName(diffBlobId);
                string diffVersion = VersionIdentifier.GetVersion(diffBlobId);
                string strippedDiffBlobFileName = diffBlobFileName;
                if (diffVersion != null)
                {
                    strippedDiffBlobFileName = strippedDiffBlobFileName.Replace(diffVersion, string.Empty);
                }
                return strippedBaseBlobFileName.Equals(strippedDiffBlobFileName, StringComparison.OrdinalIgnoreCase);
            });

        string diffFilePath = null;
        if (diffBlobElement != null)
        {
            diffFilePath = Path.Combine(diffPath, "BlobArtifacts", Path.GetFileName(diffBlobElement.Attribute("Id")?.Value));
            if (!File.Exists(diffFilePath))
            {
                diffFilePath = null;
            }
        }

        return new AssetMapping
        {
            Id = baseBlobId,
            DiffFilePath = diffFilePath,
            DiffManifestElement = diffBlobElement,
            BaseBuildFilePath = baseFilePath,
            BaseBuildManifestElement = baseElement,
            AssetType = AssetType.Blob
        };
    }

    private static string CalculateShippingPathElement(XElement baseElement)
    {
        bool baseBlobIsShipping = baseElement.Attribute("NonShipping")?.Value != "true";
        return baseBlobIsShipping ? "shipping" : "nonshipping";
    }

    private AssetMapping MapPackage(XDocument diffMergedManifestContent, XElement baseElement, string basePath, string diffPath)
    {
        string packageId = baseElement.Attribute("Id")?.Value;
        string basePackageVersion = baseElement.Attribute("Version")?.Value;
        bool basePackageIsShipping = baseElement.Attribute("NonShipping")?.Value != "true";
        string basePackageShippingPathElement = basePackageIsShipping ? "shipping" : "nonshipping";
        string baseFilePath = Path.Combine(basePath, basePackageShippingPathElement, "packages", $"{packageId}.{basePackageVersion}.nupkg");

        if (!File.Exists(baseFilePath))
        {
            // Find the diff file path
            baseFilePath = null;
        }

        var diffPackageElement = diffMergedManifestContent.Descendants("Package")
            .FirstOrDefault(p => p.Attribute("Id")?.Value == packageId);

        string diffFilePath = null;
        if (diffPackageElement != null)
        {
            string diffPackageVersion = diffPackageElement.Attribute("Version")?.Value;
            bool diffPackageIsShipping = diffPackageElement.Attribute("NonShipping")?.Value != "true";
            string diffPackageShippingPathElement = diffPackageIsShipping ? "shipping" : "nonshipping";
            diffFilePath = Path.Combine(diffPath, "PackageArtifacts", $"{packageId}.{diffPackageVersion}.nupkg");
            if (!File.Exists(diffFilePath))
            {
                diffFilePath = null;
            }
        }

        return new AssetMapping
        {
            Id = packageId,
            DiffFilePath = diffFilePath,
            DiffManifestElement = diffPackageElement,
            BaseBuildFilePath = baseFilePath,
            BaseBuildManifestElement = baseElement,
            AssetType = AssetType.Package
        };
    }
}
