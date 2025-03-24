
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
    PackageMetadataDifference,
    PackageTFMs,
    PackageDependencies,
}
