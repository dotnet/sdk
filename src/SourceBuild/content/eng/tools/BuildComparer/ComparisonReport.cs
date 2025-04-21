using System.Xml.Serialization;
/// <summary>
/// Contains the results of a build comparison between Microsoft and VMR builds.
/// </summary>
public class ComparisonReport
{
    /// <summary>
    /// Gets the number of assets with identified issues.
    /// </summary>
    public int IssueCount { get => AssetsWithIssues.Sum(a => a.Issues.Where(i => i.Baseline == null).Count()); }

    /// <summary>
    /// Gets the number of assets with evaluation errors.
    /// </summary>
    public int ErrorCount { get => AssetsWithErrors.Sum(a => a.EvaluationErrors.Count); }

    /// <summary>
    /// Gets the total number of assets analyzed in the report.
    /// </summary>
    public int BaselineCount { get => AssetsWithIssues.Sum(a => a.Issues.Where(i => i.Baseline != null).Count()) +
                                      AssetsWithErrors.Sum(a => a.Issues.Where(i => i.Baseline != null).Count()) +
                                      AssetsWithoutIssues.Sum(a => a.Issues.Where(i => i.Baseline != null).Count()); }
    /// <summary>
    /// Gets or sets the list of assets that have issues.
    /// </summary>
    public List<AssetMapping> AssetsWithIssues { get; set; }

    /// <summary>
    /// Gets or sets the list of assets that have issues.
    /// </summary>
    public List<AssetMapping> AssetsWithErrors { get; set; }

    /// <summary>
    /// Gets or sets the list of assets that have issues.
    /// </summary>
    public List<AssetMapping> AssetsWithoutIssues { get; set; }
}
