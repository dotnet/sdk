using Microsoft.DotNet.VersionTools.BuildManifest;
using NuGet.Packaging;
using System.Collections.Immutable;
using System.CommandLine;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using System.Xml.Serialization;

public enum AssetType
{
    Blob,
    Package,
    Unknown
}

public class ComparisonReport
{
    [XmlAttribute("IssueCount")]
    public int IssueCount { get => AssetsWithIssues.Count; }

    [XmlAttribute("ErrorCount")]
    public int ErrorCount { get => AssetsWithErrors.Count; }

    [XmlAttribute("TotalCount")]
    public int TotalCount { get => AssetsWithIssues.Count + AssetsWithoutIssues.Count; }
    public List<AssetMapping> AssetsWithIssues { get; set; }
    public List<AssetMapping> AssetsWithErrors { get; set; }
    public List<AssetMapping> AssetsWithoutIssues { get; set; }
}
public class AssetMapping
{
    [XmlAttribute("Id")]
    public string Id { get; set; }

    [XmlAttribute("Type")]
    public AssetType AssetType { get; set; } = AssetType.Unknown;
    [XmlIgnore]
    public bool DiffElementFound { get => DiffManifestElement != null; }
    [XmlIgnore]
    public bool DiffFileFound { get => DiffFilePath != null; }

    [XmlElement("DiffFile")]
    public string DiffFilePath { get; set; }
    [XmlIgnore]
    public XElement DiffManifestElement { get; set; }

    [XmlElement("BaseFile")]
    public string BaseBuildFilePath { get; set; }

    [XmlIgnore]
    public XElement BaseBuildManifestElement
    {
        get; set;
    }

    public List<string> EvaluationErrors { get; set; } = new List<string>();

    public List<Issue> Issues { get; set; } = new List<Issue>();
}

public enum IssueType
{
    MissingShipping,
    MissingNonShipping,
    MisclassifiedAsset,
    AssemblyVersionMismatch,
}

public class Issue
{
    [XmlAttribute("Type")]
    public IssueType IssueType { get; set; }
    [XmlAttribute("Description")]
    public string Description { get; set; }
}

public class Program
{
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

    string _vmrManifestPath;
    string _vmrBuildAssetBasePath;
    string _baseBuildAssetBasePath;
    string _outputFilePath;
    SemaphoreSlim _throttle;
    ComparisonReport _comparisonReport = new ComparisonReport();
    List<AssetMapping> _assetMappings = new List<AssetMapping>();

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

    private async Task<int> CompareBuilds()
    {
        try
        {
            GenerateAssetMappings();
            await EvaluateMappings();
            GenerateReport();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private async Task EvaluateMappings()
    {
        var tasks = new Task[] {
                EvaluatePackages(_assetMappings.Where(mapping => mapping.AssetType == AssetType.Package)),
                EvaluateBlobs(_assetMappings.Where(mapping => mapping.AssetType == AssetType.Blob)) };

        await Task.WhenAll(tasks);
    }

    private void GenerateAssetMappings()
    {
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

            var repoBuildMergeManifestContent = XDocument.Load(repoMergedManifestPath);
            _assetMappings.AddRange(MapFilesForManifest(vmrMergedManifestContent,
                                                       baseDirectory,
                                                       _vmrBuildAssetBasePath,
                                                       repoMergedManifestPath,
                                                       repoBuildMergeManifestContent));
        }
    }

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


    private async Task EvaluatePackages(IEnumerable<AssetMapping> packageMappings)
    {
        var packageEvaluationTasks = packageMappings.Select(mapping => EvaluatePackage(mapping)).ToArray();

        await Task.WhenAll(packageEvaluationTasks).ConfigureAwait(false);
    }

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
            await CompareAssemblyVersions(mapping);
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

    static readonly ImmutableArray<string> IncludedFileExtensions = [".dll", ".exe"];

    public async Task CompareAssemblyVersions(AssetMapping mapping)
    {
        var diffNugetPackagePath = mapping.DiffFilePath;
        var baselineNugetPackagePath = mapping.BaseBuildFilePath;
        var packageName = mapping.Id;

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

                    IEnumerable<string> baselineFiles = (await baselinePackageReader.GetFilesAsync(CancellationToken.None)).Where(f => IncludedFileExtensions.Contains(Path.GetExtension(f)));
                    IEnumerable<string> testFiles = (await testPackageReader.GetFilesAsync(CancellationToken.None)).Where(f => IncludedFileExtensions.Contains(Path.GetExtension(f)));
                    foreach (var fileName in baselineFiles.Intersect(testFiles))
                    {
                        try
                        {
                            AssemblyName baselineAssemblyName = null;
                            AssemblyName testAssemblyName = null;

                            using (var baselineStream = await ReadEntryToStream(baselinePackageReader, fileName))
                            using (var testStream = await ReadEntryToStream(testPackageReader, fileName))
                            {
                                baselineAssemblyName = GetAssemblyName(baselineStream, fileName);
                                testAssemblyName = GetAssemblyName(testStream, fileName);
                            }

                            if ((baselineAssemblyName == null) != (testAssemblyName == null))
                            {
                                mapping.Issues.Add(new Issue
                                {
                                    IssueType = IssueType.AssemblyVersionMismatch,
                                    Description = $"Assembly '{fileName}' in package '{packageName}' has different versions in the VMR and base build."
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
                                    Description = $"Assembly '{fileName}' in package '{packageName}' has different versions in the VMR and base build. VMR version: {baselineAssemblyName}, base build version: {testAssemblyName}"
                                });
                            }
                        }
                        catch (Exception e)
                        {
                            mapping.EvaluationErrors.Add(e.ToString());
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            mapping.EvaluationErrors.Add(e.ToString());
        }

        static async Task<Stream> ReadEntryToStream(PackageArchiveReader packageArchiveReader, string fileName)
        {
            var outputStream = new MemoryStream();
            var entryStream = packageArchiveReader.GetEntry(fileName).Open();
            await entryStream.CopyToAsync(outputStream, CancellationToken.None);
            await entryStream.FlushAsync(CancellationToken.None);
            outputStream.Position = 0;
            return outputStream;
        }

        static AssemblyName GetAssemblyName(Stream stream, string fileName)
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
    }

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

    private async Task EvaluateBlobs(IEnumerable<AssetMapping> assetMappings)
    {
        var blobs = assetMappings.Where(a => a.AssetType == AssetType.Blob);
        var tasks = blobs.Select(mapping => EvaluateBlob(mapping)).ToArray();

        await Task.WhenAll(tasks);
        
    }

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

    private List<AssetMapping> MapFilesForManifest(XDocument vmrMergedManifestContent, string baseDirectory, string diffDirectory, string repoMergedManifestPath, XDocument repoBuildMergeManifestContent)
    {
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
        string strippedBaseBlobFileName = baseBlobFileName.Replace(baseVersion, string.Empty);

        var diffBlobElement = diffMergedManifestContent.Descendants("Blob")
            .FirstOrDefault(p =>
            {
                string diffBlobId = p.Attribute("Id")?.Value;
                string diffBlobFileName = Path.GetFileName(diffBlobId);
                string diffVersion = VersionIdentifier.GetVersion(diffBlobId);
                string strippedDiffBlobFileName = diffBlobFileName.Replace(diffVersion, string.Empty);
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
