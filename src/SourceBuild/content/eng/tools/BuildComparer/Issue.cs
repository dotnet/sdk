using System.Xml.Serialization;
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

    /// <summary>
    /// Matching baseline entries for this issue.
    /// </summary>
    public BaselineEntry Baseline { get; set; }
}
