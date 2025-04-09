using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

public class BaselineEntry
{
    [XmlIgnore]
    /// <summary>
    /// Regex used to match the ID of the asset.
    /// </summary>
    public Regex IdMatch { get; set; }
    [XmlIgnore]
    /// <summary>
    /// Issue type to baseline
    /// </summary>
    public IssueType? IssueType { get; set; }
    [XmlIgnore]
    /// <summary>
    /// Regex used to match the description of the asset.
    /// </summary>
    public Regex DescriptionMatch { get; set; }
    
    [XmlIgnore]
    public bool? AllowRevisionOnlyVariance { get; set; }

    [XmlAttribute]
    /// <summary>
    /// Justification for the baseline.
    /// </summary>
    public string Justification { get; set; }
}

public class Baseline
{
    private List<BaselineEntry> _entries;
    public Baseline(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new ArgumentException($"Baseline file not found: {filePath}");
        }

        string jsonContent = File.ReadAllText(filePath);

        // Configure JSON deserialization options with custom converters
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new RegexJsonConverter(),
                new JsonStringEnumConverter()
            }
        };

        // Deserialize the JSON into a list of BaselineEntry objects
        _entries = JsonSerializer.Deserialize<List<BaselineEntry>>(jsonContent, options) ?? new List<BaselineEntry>();

        // Validate the baseline entries. An entry must have a Justification and an IssueType,
        // and must have at least one of IdMatch or DescriptionMatch.

        foreach (var entry in _entries)
        {
            if (string.IsNullOrEmpty(entry.Justification))
            {
                throw new ArgumentException("Justification cannot be null or empty.");
            }
            if (entry.IssueType == null)
            {
                throw new ArgumentException("IssueType cannot be null.");
            }
            if (entry.IdMatch == null && entry.DescriptionMatch == null)
            {
                throw new ArgumentException("At least one of IdMatch or DescriptionMatch must be provided.");
            }
        }
    }

    static Regex revisionVarianceRegex = new Regex(@"Version=(\d+\.\d+\.\d+)\.\d+.*Version=\1\.\d+");
    static Regex noRevisionVarianceRegex = new Regex(@"Version=(\d+\.\d+\.\d+\.\d+),.*Version=\1");

    // Check which baseline entries match against the given asset issue.
    public List<BaselineEntry> GetMatchingBaselineEntries(Issue assetIssue, AssetMapping assetMapping)
    {
        var matchingEntries = new List<BaselineEntry>();
        foreach (var entry in _entries)
        {
            if (entry.IssueType == assetIssue.IssueType &&
                (entry.IdMatch == null || entry.IdMatch.IsMatch(assetMapping.Id)) &&
                (entry.DescriptionMatch == null || entry.DescriptionMatch.IsMatch(assetIssue.Description)))
            {
                if (entry.AllowRevisionOnlyVariance == null ||
                    (entry.AllowRevisionOnlyVariance.Value && revisionVarianceRegex.IsMatch(assetIssue.Description)) ||
                    (!entry.AllowRevisionOnlyVariance.Value && noRevisionVarianceRegex.IsMatch(assetIssue.Description)))
                {
                    // If the entry allows assembly revision variance, add it to the matching entries
                    matchingEntries.Add(entry);
                }
            }
        }
        return matchingEntries;
    }

    // Custom JSON converter for Regex objects
    private class RegexJsonConverter : JsonConverter<Regex>
    {
        public override Regex Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string pattern = reader.GetString();
            return pattern != null ? new Regex(pattern, RegexOptions.Compiled) : null;
        }

        public override void Write(Utf8JsonWriter writer, Regex value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString());
        }
    }
}
