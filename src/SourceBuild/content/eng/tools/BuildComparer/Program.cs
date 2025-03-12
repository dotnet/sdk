using Microsoft.DotNet.VersionTools.BuildManifest;
using NuGet.Packaging;
using System.Collections.Immutable;
using System.CommandLine;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using System.Xml.Serialization;

/// <summary>
/// Defines the type of asset being processed in the build comparison tool.
/// </summary>
public enum AssetType
{
    /// <summary>
    /// Represents a random non-package file in the build.
    /// </summary>
    Blob,
    
    /// <summary>
    /// Represents a NuGet package asset.
    /// </summary>
    Package,
    
    /// <summary>
    /// Represents an asset of unknown type.
    /// </summary>
    Unknown
}

/// <summary>
/// Contains the results of a build comparison between Microsoft and VMR builds.
/// </summary>
public class ComparisonReport
{
    /// <summary>
    /// Gets the number of assets with identified issues.
    /// </summary>
    [XmlAttribute("IssueCount")]
    public int IssueCount { get => AssetsWithIssues.Count; }

    /// <summary>
    /// Gets the number of assets with evaluation errors.
    /// </summary>
    [XmlAttribute("ErrorCount")]
    public int ErrorCount { get => AssetsWithErrors.Count; }

    /// <summary>
    /// Gets the total number of assets analyzed in the report.
    /// </summary>
    [XmlAttribute("TotalCount")]
    public int TotalCount { get => AssetsWithIssues.Count + AssetsWithoutIssues.Count; }
    
    /// <summary>
    /// Gets or sets the list of assets that have issues.
    /// </summary>
    public List<AssetMapping> AssetsWithIssues { get; set; }
    
    /// <summary>
    /// Gets or sets the list of assets that have evaluation errors.
    /// </summary>
    public List<AssetMapping> AssetsWithErrors { get; set; }
    
    /// <summary>
    /// Gets or sets the list of assets without any identified issues.
    /// </summary>
    public List<AssetMapping> AssetsWithoutIssues { get; set; }
}

/// <summary>
/// Represents the mapping between base build and VMR build for a specific asset.
/// </summary>
public class AssetMapping
{
    /// <summary>
    /// Gets or sets the identifier of the asset.
    /// </summary>
    [XmlAttribute("Id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the type of the asset.
    /// </summary>
    [XmlAttribute("Type")]
    public AssetType AssetType { get; set; } = AssetType.Unknown;
    
    /// <summary>
    /// Gets a value indicating whether a corresponding element was found in the diff manifest.
    /// </summary>
    [XmlIgnore]
    public bool DiffElementFound { get => DiffManifestElement != null; }
    
    /// <summary>
    /// Gets a value indicating whether a corresponding file was found in the diff build.
    /// </summary>
    [XmlIgnore]
    public bool DiffFileFound { get => DiffFilePath != null; }

    /// <summary>
    /// Gets or sets the path to the diff file.
    /// </summary>
    [XmlElement("DiffFile")]
    public string DiffFilePath { get; set; }
    
    /// <summary>
    /// Gets or sets the XML element from the diff manifest.
    /// </summary>
    [XmlIgnore]
    public XElement DiffManifestElement { get; set; }

    /// <summary>
    /// Gets or sets the path to the base build file.
    /// </summary>
    [XmlElement("BaseFile")]
    public string BaseBuildFilePath { get; set; }

    /// <summary>
    /// Gets or sets the XML element from the base build manifest.
    /// </summary>
    [XmlIgnore]
    public XElement BaseBuildManifestElement
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the list of errors encountered during evaluation.
    /// </summary>
    public List<string> EvaluationErrors { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of issues identified for this asset.
    /// </summary>
    public List<Issue> Issues { get; set; } = new List<Issue>();
}

/// <summary>
/// Defines types of issues that can be identified during asset comparison.
/// </summary>
public enum IssueType
{
    /// <summary>
    /// Indicates a shipping asset is missing in the VMR build.
    /// </summary>
    MissingShipping,
    
    /// <summary>
    /// Indicates a non-shipping asset is missing in the VMR build.
    /// </summary>
    MissingNonShipping,
    
    /// <summary>
    /// Indicates an asset is classified differently between base and VMR builds.
    /// </summary>
    MisclassifiedAsset,
    
    /// <summary>
    /// Indicates a version mismatch between assemblies in base and VMR builds.
    /// </summary>
    AssemblyVersionMismatch,
    MissingPackageContent,
    ExtraPackageContent,
}

/// <summary>
/// Represents an issue identified during asset comparison.
/// </summary>
public class Issue
{
    /// <summary>
    /// Gets or sets the type of issue.
    /// </summary>
    [XmlAttribute("Type")]
    public IssueType IssueType { get; set; }
    
    /// <summary>
    /// Gets or sets a description of the issue.
    /// </summary>
    [XmlAttribute("Description")]
    public string Description { get; set; }
}

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
            DefaultValueFactory = _ => 16,
            Required = true
        };
        var rootCommand = new CliRootCommand(description: "Tool for comparing Microsoft builds with VMR builds.")
        {
            vmrManifestPathArgument,
            vmrAssetBasePathArgument,
            msftAssetBasePathArgument,
            outputFileArgument,
            parallelismArgument
        };

        rootCommand.Description = "Compares build manifests and outputs missing or misclassified assets.";

        var result = rootCommand.Parse(args);
        var comparer = new Program(result.GetValue(vmrManifestPathArgument),
                                    result.GetValue(vmrAssetBasePathArgument),
                                    result.GetValue(msftAssetBasePathArgument),
                                    result.GetValue(outputFileArgument),
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
                    int parallelTasks)
    {
        _vmrManifestPath = vmrManifestPath;
        _vmrBuildAssetBasePath = vmrAssetBasePath;
        _baseBuildAssetBasePath = baseBuildAssetBasePath;
        _outputFilePath = outputFilePath;
        _throttle = new SemaphoreSlim(parallelTasks, parallelTasks);
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
            GenerateReport();

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

        // Generate the AssetReportType by grouping the asset mappings by whether they
        // have issues
        _comparisonReport.AssetsWithErrors = _assetMappings.Where(a => a.EvaluationErrors.Count > 0).OrderByDescending(a => a.EvaluationErrors.Count).ToList();
        _comparisonReport.AssetsWithIssues = _assetMappings.Where(a => a.Issues.Count > 0).OrderByDescending(a => a.Issues.Count).ToList();
        _comparisonReport.AssetsWithoutIssues = _assetMappings.Where(a => a.Issues.Count == 0).ToList();

        // Serialize all asset mappings to xml
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ComparisonReport));
        using (var stream = new FileStream(_outputFilePath, FileMode.Create))
        {
            serializer.Serialize(stream, _comparisonReport);
            stream.Close();
        }
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

    private async void ComparePackageFileLists(AssetMapping mapping, PackageArchiveReader testPackageReader, PackageArchiveReader baselinePackageReader)
    {
        IEnumerable<string> baselineFiles = (await baselinePackageReader.GetFilesAsync(CancellationToken.None));
        IEnumerable<string> testFiles = (await testPackageReader.GetFilesAsync(CancellationToken.None));

        var missingFiles = baselineFiles.Except(testFiles).ToList();

        foreach (var missingFile in missingFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.MissingPackageContent,
                Description = $"Package '{mapping.Id}' is missing the following files in the VMR: {string.Join(", ", missingFile)}"
            });
        }

        // Compare the other way, and identify content in the VMR that is not in the baseline
        var extraFiles = testFiles.Except(baselineFiles).ToList();

        foreach (var extraFile in extraFiles)
        {
            mapping.Issues.Add(new Issue
            {
                IssueType = IssueType.ExtraPackageContent,
                Description = $"Package '{mapping.Id}' has extra files in the VMR: {string.Join(", ", extraFile)}"
            });
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
                using var baselineStream = await ReadPackageEntryToStream(baselinePackageReader, fileName);
                using var testStream = await ReadPackageEntryToStream(testPackageReader, fileName);

                AssemblyName baselineAssemblyName = GetAssemblyName(baselineStream, fileName);
                AssemblyName testAssemblyName = GetAssemblyName(testStream, fileName);

                if ((baselineAssemblyName == null) != (testAssemblyName == null))
                {
                    mapping.Issues.Add(new Issue
                    {
                        IssueType = IssueType.AssemblyVersionMismatch,
                        Description = $"Assembly '{fileName}' in package '{mapping.Id}' has different versions in the VMR and base build."
                    });
                }
                else if (baselineAssemblyName == null && testAssemblyName == null)
                {
                    continue;
                }

                if (baselineAssemblyName.ToString() != testAssemblyName.ToString())
                {
                    mapping.Issues.Add(new Issue
                    {
                        IssueType = IssueType.AssemblyVersionMismatch,
                        Description = $"Assembly '{fileName}' in package '{mapping.Id}' has different versions in the VMR and base build. VMR version: {baselineAssemblyName}, base build version: {testAssemblyName}"
                    });
                }
            }
            catch (Exception e)
            {
                mapping.EvaluationErrors.Add(e.ToString());
            }
        }
    }

    private static async Task<Stream> ReadPackageEntryToStream(PackageArchiveReader packageArchiveReader, string fileName)
    {
        var outputStream = new MemoryStream();
        var entryStream = packageArchiveReader.GetEntry(fileName).Open();
        await entryStream.CopyToAsync(outputStream, CancellationToken.None);
        await entryStream.FlushAsync(CancellationToken.None);
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
