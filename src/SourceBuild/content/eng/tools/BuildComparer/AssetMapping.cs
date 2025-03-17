using System.Xml.Linq;
using System.Xml.Serialization;
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
