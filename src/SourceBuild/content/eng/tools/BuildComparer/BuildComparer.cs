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

public abstract class BuildComparer
{
    /// <summary>
    /// Path to the VMR manifest file.
    /// </summary>
    protected string _vmrManifestPath;
    
    /// <summary>
    /// Base path for VMR build assets.
    /// </summary>
    protected string _vmrBuildAssetBasePath;
    
    /// <summary>
    /// Base path for Microsoft build assets.
    /// </summary>
    protected string _baseBuildAssetBasePath;
    
    /// <summary>
    /// Path where the comparison report for issues will be saved.
    /// </summary>
    private string _issuesReportPath;
    
    /// <summary>
    /// Path where the comparison report for no issues will be saved.
    /// </summary>
    private string _noIssuesReportPath;
    
    /// <summary>
    /// Semaphore used to control parallel processing.
    /// </summary>
    protected SemaphoreSlim _throttle;
    
    /// <summary>
    /// Report containing the results of the comparison.
    /// </summary>
    protected ComparisonReport _comparisonReport = new ComparisonReport();
    
    /// <summary>
    /// List of all asset mappings between base and VMR builds.
    /// </summary>
    protected List<AssetMapping> _assetMappings = new List<AssetMapping>();

    /// <summary>
    /// Baseline used to filter issues in the comparison report.
    /// </summary>
    protected Baseline _baseline;

    /// <summary>
    /// The issues to report on.
    /// </summary>
    List<IssueType> _issuesToReport;

    protected BuildComparer(
        string vmrManifestPath,
        string vmrAssetBasePath,
        string baseBuildAssetBasePath,
        string issuesReportPath,
        string noIssuesReportPath,
        int parallelTasks,
        string baselineFilePath,
        List<IssueType> issuesToReport)
    {
        _vmrManifestPath = vmrManifestPath;
        _vmrBuildAssetBasePath = vmrAssetBasePath;
        _baseBuildAssetBasePath = baseBuildAssetBasePath;
        _issuesReportPath = issuesReportPath;
        _noIssuesReportPath = noIssuesReportPath;
        _throttle = new SemaphoreSlim(parallelTasks, parallelTasks);
        _issuesToReport = issuesToReport;

        if (!string.IsNullOrEmpty(baselineFilePath))
        {
            _baseline = new Baseline(baselineFilePath);
        }
    }

    /// <summary>
    /// Compares builds and returns a task with the result.
    /// </summary>
    public async Task<int> Compare()
    {
        try
        {
            GenerateAssetMappings();
            await EvaluateAssets();
            ApplyBaseline();
            GenerateReport(_issuesToReport);

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.ToString()}");
            return 1;
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

    protected abstract Task EvaluateAsset(AssetMapping mapping);

    /// <summary>
    /// Generates asset mappings between base builds and VMR builds.
    /// </summary>
    /// <remarks>
    /// Walks through each repository's merged manifest and maps files between
    /// the base build and VMR build based on asset IDs.
    /// </remarks>
    protected void GenerateAssetMappings()
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
    /// Applies the baseline to the asset mappings.
    /// </summary>
    protected void ApplyBaseline()
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
    /// Generates the final comparison report and saves it to the specified output file.
    /// </summary>
    protected void GenerateReport(List<IssueType> issueTypes)
    {
        // Create two separate reports
        var issuesReport = new ComparisonReport();
        var noIssuesReport = new ComparisonReport();

        // Assets with errors go to the issues report
        var assetsWithErrors = _assetMappings
            .Where(mapping => mapping.EvaluationErrors.Any())
            .ToList();

        // Process each asset mapping to potentially split between reports
        var assetsForReport = _assetMappings
            .Where(mapping => !mapping.EvaluationErrors.Any())
            .SelectMany(mapping =>
            {
                var nonBaselinedIssues = mapping.Issues.Any(i => i.Baseline == null);
                var baselinedIssues = mapping.Issues.Any(i => i.Baseline != null);

                if (nonBaselinedIssues && baselinedIssues)
                {
                    // If it has both non-baselined and baselined issues, create a copy for the issues report
                    var nonBaselinedMapping = CloneAssetMappingWithFilteredIssues(mapping, i => i.Baseline == null);
                    var baselinedMapping = CloneAssetMappingWithFilteredIssues(mapping, i => i.Baseline != null);
                    return new[] { nonBaselinedMapping, baselinedMapping };
                }
                else
                {
                    // If it has no issues at all, it goes to the no-issues report as is
                    return new[] { mapping };
                }
            })
            .ToList();

        // Populate the issues report
        issuesReport.AssetsWithIssues = assetsForReport
            .Where(mapping => mapping.Issues.Any(i => i.Baseline == null))
            .OrderByDescending(mapping => mapping.Issues.Count)
            .ToList();
        issuesReport.AssetsWithErrors = assetsWithErrors;
        issuesReport.AssetsWithoutIssues = new List<AssetMapping>();

        // Populate the no-issues report
        noIssuesReport.AssetsWithIssues = assetsForReport
            .Where(mapping => mapping.Issues.All(i => i.Baseline != null))
            .ToList();
        noIssuesReport.AssetsWithoutIssues = assetsForReport
            .Where(mapping => !mapping.Issues.Any())
            .ToList();
        noIssuesReport.AssetsWithErrors = new List<AssetMapping>();

        // Create directories if they don't exist
        Directory.CreateDirectory(Path.GetDirectoryName(_issuesReportPath));
        Directory.CreateDirectory(Path.GetDirectoryName(_noIssuesReportPath));

        // Serialize reports to XML
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ComparisonReport));
        using (var stream = new FileStream(_issuesReportPath, FileMode.Create))
        {
            serializer.Serialize(stream, issuesReport);
        }

        using (var stream = new FileStream(_noIssuesReportPath, FileMode.Create))
        {
            serializer.Serialize(stream, noIssuesReport);
        }

        // Update console output for both reports
        Console.WriteLine($"Issues report saved to {_issuesReportPath}");
        Console.WriteLine($"No-issues report saved to {_noIssuesReportPath}");
        Console.WriteLine($"Errors: {assetsWithErrors.Count}");
        Console.WriteLine($"Non-baselined issues: {issuesReport.AssetsWithIssues.Sum(m => m.Issues.Count)}");
        Console.WriteLine($"Baselined issues: {noIssuesReport.AssetsWithIssues.Sum(m => m.Issues.Count)}");

        // Print detailed issue counts by type
        var allAssetWithIssues = assetsForReport.Where(mapping => mapping.Issues.Any()).ToList();

        var issueCountsByType = allAssetWithIssues
            .SelectMany(mapping => mapping.Issues)
            .Where(issue => issue.Baseline == null)
            .GroupBy(issue => issue.IssueType)
            .ToDictionary(group => group.Key, group => group.Count());

        var baselinedIssueCountsByType = allAssetWithIssues
            .SelectMany(mapping => mapping.Issues)
            .Where(issue => issue.Baseline != null)
            .GroupBy(issue => issue.IssueType)
            .ToDictionary(group => group.Key, group => group.Count());

        Console.WriteLine("Detailed issue counts by type:");
        foreach (var issueType in Enum.GetValues(typeof(IssueType)).Cast<IssueType>().Where(issueTypes.Contains))
        {
            issueCountsByType.TryGetValue(issueType, out int issueCount);
            baselinedIssueCountsByType.TryGetValue(issueType, out int baselinedIssueCount);
            Console.WriteLine($"  {issueType}: Issues w/o Baseline = {issueCount}, Baselined issues = {baselinedIssueCount}");
        }
    }

    /// <summary>
    /// Clones an asset mapping with filtered issues.
    /// </summary>
    /// <param name="original">Original asset mapping.</param>
    /// <param name="issueFilter">Filter function for issues.</param>
    /// <returns>Cloned asset mapping with filtered issues.</returns>
    private static AssetMapping CloneAssetMappingWithFilteredIssues(AssetMapping original, Func<Issue, bool> issueFilter)
    {
        return new AssetMapping
        {
            Id = original.Id,
            AssetType = original.AssetType,
            DiffFilePath = original.DiffFilePath,
            DiffManifestElement = original.DiffManifestElement,
            BaseBuildFilePath = original.BaseBuildFilePath,
            BaseBuildManifestElement = original.BaseBuildManifestElement,
            EvaluationErrors = original.EvaluationErrors,
            Issues = original.Issues.Where(issueFilter).ToList()
        };
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

        string baseVersion;
        // Special case for symbols packages, since we can open them. There are some blobs where we have trouble
        // identifying the version by a string parse because it doesn't have a proper pre-release label. e.g. assets/symbols/csc.ARM64.Symbols.4.14.0-3.25174.10.symbols.nupkg
        if (baseFilePath != null && baseBlobId.EndsWith(".symbols.nupkg"))
        {
            baseVersion = _nupkgInfoFactory.CreateNupkgInfo(baseFilePath).Version;
        }
        else
        {
            baseVersion = VersionIdentifier.GetVersion(baseBlobId);
        }

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
                string diffFilePath = Path.Combine(diffPath, "BlobArtifacts", diffBlobFileName);

                // Because the version identifier isn't perfect, we special case a couple artifacts where it's easier to
                // just open a nupkg and get the version from it.
                string diffVersion = null;
                if (diffFilePath.EndsWith(".symbols.nupkg"))
                {
                    if (!_symbolNupkgBlobVersionHelper.TryGetValue(diffBlobId, out diffVersion) && File.Exists(diffFilePath))
                    {
                        diffVersion = _nupkgInfoFactory.CreateNupkgInfo(diffFilePath).Version;
                        _symbolNupkgBlobVersionHelper.TryAdd(diffBlobId, diffVersion);
                    }
                }

                if (diffVersion == null)
                {
                    diffVersion = VersionIdentifier.GetVersion(diffBlobId);
                }
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

    private static string CalculateShippingPathElement(XElement baseElement)
    {
        bool baseBlobIsShipping = baseElement.Attribute("NonShipping")?.Value != "true";
        return baseBlobIsShipping ? "shipping" : "nonshipping";
    }

    private NupkgInfoFactory _nupkgInfoFactory = new NupkgInfoFactory(new PackageArchiveReaderFactory());
    private ConcurrentDictionary<string, string> _symbolNupkgBlobVersionHelper = new ConcurrentDictionary<string, string>();
}
