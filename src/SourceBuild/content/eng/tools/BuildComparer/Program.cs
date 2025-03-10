using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Xml.Serialization;

public enum AssetType
{
    Blob,
    Package,
    Unknown
}

public class AssetReport
{
    [XmlAttribute("IssueCount")]
    public int IssueCount { get => AssetsWithIssues.Count; }

    [XmlAttribute("TotalCount")]
    public int TotalCount { get => AssetsWithIssues.Count + AssetsWithoutIssues.Count; }
    public List<AssetMapping> AssetsWithIssues { get; set; }
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

    public List<Issue> Issues { get; set; } = new List<Issue>();
}

public enum IssueType
{
    MissingShipping,
    MissingNonShipping,
    MisclassifiedAsset,
}

public class Issue
{
    [XmlAttribute("Type")]
    public IssueType IssueType { get; set; }
    [XmlAttribute("Description")]
    public string Description { get; set; }
}

class Program
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
        var outputFilePathArgument = new CliOption<string>("-reportPath")
        {
            Description = "Path to directory containing report files.",
            Required = true
        };
        var rootCommand = new CliRootCommand(description: "Tool for comparing Microsoft builds with VMR builds.")
        {
            vmrManifestPathArgument,
            vmrAssetBasePathArgument,
            msftAssetBasePathArgument,
            outputFilePathArgument
        };

        rootCommand.Description = "Compares build manifests and outputs missing or misclassified assets.";

        bool compareResult = false; 
        rootCommand.SetAction((result) =>
        {
            var comparer = new Program(result.GetValue(vmrManifestPathArgument),
                                       result.GetValue(vmrAssetBasePathArgument),
                                       result.GetValue(msftAssetBasePathArgument),
                                       result.GetValue(outputFilePathArgument));
            compareResult = comparer.CompareBuilds();
        });

        rootCommand.Parse(args).Invoke();

        return (compareResult ? 0 : 1);
    }

    string _vmrManifestPath;
    string _vmrBuildAssetBasePath;
    string _baseBuildAssetBasePath;
    string _outputFilePath;

    private Program(string vmrManifestPath, string vmrAssetBasePath, string baseBuildAssetBasePath, string outputFilePath)
    {
        _vmrManifestPath = vmrManifestPath;
        _vmrBuildAssetBasePath = vmrAssetBasePath;
        _baseBuildAssetBasePath = baseBuildAssetBasePath;
        _outputFilePath = outputFilePath;
    }

    private bool CompareBuilds()
    {
        try
        {
            // Load the XML file
            XDocument vmrMergedManifestContent = XDocument.Load(_vmrManifestPath);

            // Get all files in the assets folder, including subfolders
            var allFiles = Directory.GetFiles(_baseBuildAssetBasePath, "*", SearchOption.AllDirectories);

            List<AssetMapping> assetMappings = new List<AssetMapping>();

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
                assetMappings.AddRange(MapFilesForManifest(vmrMergedManifestContent,
                                                           baseDirectory,
                                                           _vmrBuildAssetBasePath,
                                                           repoMergedManifestPath,
                                                           repoBuildMergeManifestContent));
            }

            // Now that we have the asset mappings, we can check for missing, misclassified, or incorrect assets
            EvaluatePackages(assetMappings);
            EvaluateBlobs(assetMappings);

            WriteResults(assetMappings);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return false;
        }

        return true;
    }

    private void WriteResults(List<AssetMapping> assetMappings)
    {
        Directory.CreateDirectory(_outputFilePath);

        // Generate the AssetReportType by grouping the asset mappings by whether they
        // have issues
        var assetReport = new AssetReport
        {
            AssetsWithIssues = assetMappings.Where(a => a.Issues.Count > 0).OrderByDescending(a => a.Issues.Count).ToList(),
            AssetsWithoutIssues = assetMappings.Where(a => a.Issues.Count == 0).ToList()
        };

        // Serialize all asset mappings to xml
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(AssetReport));
        using (var stream = new FileStream(Path.Combine(_outputFilePath, "BuildComparison.xml"), FileMode.Create))
        {
            serializer.Serialize(stream, assetReport);
            stream.Close();
        }
    }

    private void EvaluatePackages(List<AssetMapping> assetMappings)
    {
        foreach (var mapping in assetMappings.Where(a => a.AssetType == AssetType.Package))
        {
            // Filter away mappings that we do not care about
            if (mapping.Id.Contains("Microsoft.SourceBuild.Intermediate"))
            {
                continue;
            }

            // Check if the package is missing in the VMR
            if (!mapping.DiffElementFound)
            {
                mapping.Issues.Add(new Issue
                {
                    IssueType = mapping.BaseBuildManifestElement.Attribute("NonShipping")?.Value == "true" ? IssueType.MissingNonShipping : IssueType.MissingShipping,
                    Description = $"Package '{mapping.Id}' is missing in the VMR."
                });
            }
            else
            {
                // Asset is found. Perform tests.
                EvaluateClassification(mapping);
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

    private void EvaluateBlobs(List<AssetMapping> assetMappings)
    {
        foreach (var mapping in assetMappings.Where(a => a.AssetType == AssetType.Blob))
        {
            // Filter away mappings that we do not care about
            if (mapping.Id.Contains(".wixpack.zip"))
            {
                continue;
            }
            if (mapping.Id.Contains("MergedManifest.xml"))
            {
                continue;
            }

            // Check if the package is missing in the VMR
            if (!mapping.DiffElementFound)
            {
                mapping.Issues.Add(new Issue
                {
                    IssueType = mapping.BaseBuildManifestElement.Attribute("NonShipping")?.Value == "true" ? IssueType.MissingNonShipping : IssueType.MissingShipping,
                    Description = $"Blob '{mapping.Id}' is missing in the VMR."
                });
            }
            else
            {
                // Asset is found. Perform tests.
                EvaluateClassification(mapping);
            }
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
