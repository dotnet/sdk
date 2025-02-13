// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
#if WASM_TASKS
internal sealed class StaticWebAsset : IEquatable<StaticWebAsset>, IComparable<StaticWebAsset>
#else
public sealed class StaticWebAsset : IEquatable<StaticWebAsset>, IComparable<StaticWebAsset>
#endif
{
    public StaticWebAsset()
    {
    }

    public StaticWebAsset(StaticWebAsset asset)
    {
        Identity = asset.Identity;
        SourceType = asset.SourceType;
        SourceId = asset.SourceId;
        ContentRoot = asset.ContentRoot;
        BasePath = asset.BasePath;
        RelativePath = asset.RelativePath;
        AssetKind = asset.AssetKind;
        AssetMode = asset.AssetMode;
        AssetRole = asset.AssetRole;
        AssetMergeBehavior = asset.AssetMergeBehavior;
        AssetMergeSource = asset.AssetMergeSource;
        RelatedAsset = asset.RelatedAsset;
        AssetTraitName = asset.AssetTraitName;
        AssetTraitValue = asset.AssetTraitValue;
        CopyToOutputDirectory = asset.CopyToOutputDirectory;
        CopyToPublishDirectory = asset.CopyToPublishDirectory;
        OriginalItemSpec = asset.OriginalItemSpec;
    }

    public string Identity { get; set; }

    public string SourceId { get; set; }

    public string SourceType { get; set; }

    public string ContentRoot { get; set; }

    public string BasePath { get; set; }

    public string RelativePath { get; set; }

    public string AssetKind { get; set; }

    public string AssetMode { get; set; }

    public string AssetRole { get; set; }

    public string AssetMergeBehavior { get; set; }

    public string AssetMergeSource { get; set; }

    public string RelatedAsset { get; set; }

    public string AssetTraitName { get; set; }

    public string AssetTraitValue { get; set; }

    public string Fingerprint { get; set; }

    public string Integrity { get; set; }

    public string CopyToOutputDirectory { get; set; }

    public string CopyToPublishDirectory { get; set; }

    public string OriginalItemSpec { get; set; }

    public static StaticWebAsset FromTaskItem(ITaskItem item)
    {
        var result = FromTaskItemCore(item);

        result.Normalize();
        result.Validate();

        return result;
    }

    // Iterate over the list of assets with the same relative path and choose the one closest to the asset kind we've been given:
    // The given asset kind here will always be Build or Publish.
    // We want either the asset(s) of the given kind or the asset(s) with Kind=All.
    // Either we have an asset of a given kind or we have an asset of kind All.
    // The result must always contain a single asset, but we want to return a list so that we can report errors
    // if we find more than one asset of the same kind.
    // It is only valid to have more than one asset if they are of different kinds.
    // Specific kinds take precedence over 'All' which is the default and is used as a fallback.
    internal static void ChooseNearestAssetKind(List<StaticWebAsset> group, string assetKind)
    {
        var foundKind = false;
        for (var i = group.Count - 1; i >= 0; i--)
        {
            var item = group[i];

            switch (item.HasKind(assetKind))
            {
                case true:
                    // We found an item of the given kind, we can remove all the other items that we've inspected before
                    if (!foundKind)
                    {
                        foundKind = true;
                        group.RemoveRange(i + 1, group.Count - i - 1);
                    }
                    break;
                case false when !foundKind && item.IsBuildAndPublish():
                    // We found an item of kind 'All' and we haven't found an item of the given kind yet, so we preserve it
                    break;
                case false:
                    // We found an item of a different kind, we can remove it
                    group.RemoveAt(i);
                    break;
            }
        }
    }

    internal static bool ValidateAssetGroup(string path, IReadOnlyList<StaticWebAsset> group, out string reason)
    {
        StaticWebAsset prototypeItem = null;
        StaticWebAsset build = null;
        StaticWebAsset publish = null;
        StaticWebAsset all = null;
        foreach (var item in group)
        {
            prototypeItem ??= item;
            if (!prototypeItem.HasSourceId(item.SourceId))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For assets '{prototypeItem}' and '{item}' from different projects.";
                return false;
            }

            build ??= item.IsBuildOnly() ? item : build;
            if (build != null && item.IsBuildOnly() && !ReferenceEquals(build, item))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For 'Build' assets '{build}' and '{item}'.";
                return false;
            }

            publish ??= item.IsPublishOnly() ? item : publish;
            if (publish != null && item.IsPublishOnly() && !ReferenceEquals(publish, item))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For 'Publish' assets '{publish}' and '{item}'.";
                return false;
            }

            all ??= item.IsBuildAndPublish() ? item : all;
            if (all != null && item.IsBuildAndPublish() && !ReferenceEquals(all, item))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For 'All' assets '{all}' and '{item}'.";
                return false;
            }
        }
        reason = null;
        return true;
    }

    private bool HasKind(string assetKind) =>
        AssetKinds.IsKind(AssetKind, assetKind);

    public static StaticWebAsset FromV1TaskItem(ITaskItem item)
    {
        var result = FromTaskItemCore(item);
        result.ApplyDefaults();
        result.OriginalItemSpec = item.GetMetadata("FullPath");

        result.Normalize();
        result.Validate();

        return result;
    }

    private static StaticWebAsset FromTaskItemCore(ITaskItem item) =>
        new()
        {
            // Register the identity as the full path since assets might have come
            // from packages and other sources and the identity (which is typically
            // just the relative path from the project) is not enough to locate them.
            Identity = item.GetMetadata("FullPath"),
            SourceType = item.GetMetadata(nameof(SourceType)),
            SourceId = item.GetMetadata(nameof(SourceId)),
            ContentRoot = item.GetMetadata(nameof(ContentRoot)),
            BasePath = item.GetMetadata(nameof(BasePath)),
            RelativePath = item.GetMetadata(nameof(RelativePath)),
            AssetKind = item.GetMetadata(nameof(AssetKind)),
            AssetMode = item.GetMetadata(nameof(AssetMode)),
            AssetRole = item.GetMetadata(nameof(AssetRole)),
            AssetMergeSource = item.GetMetadata(nameof(AssetMergeSource)),
            AssetMergeBehavior = item.GetMetadata(nameof(AssetMergeBehavior)),
            RelatedAsset = item.GetMetadata(nameof(RelatedAsset)),
            AssetTraitName = item.GetMetadata(nameof(AssetTraitName)),
            AssetTraitValue = item.GetMetadata(nameof(AssetTraitValue)),
            Fingerprint = item.GetMetadata(nameof(Fingerprint)),
            Integrity = item.GetMetadata(nameof(Integrity)),
            CopyToOutputDirectory = item.GetMetadata(nameof(CopyToOutputDirectory)),
            CopyToPublishDirectory = item.GetMetadata(nameof(CopyToPublishDirectory)),
            OriginalItemSpec = item.GetMetadata(nameof(OriginalItemSpec)),
        };

    public void ApplyDefaults()
    {
        CopyToOutputDirectory = string.IsNullOrEmpty(CopyToOutputDirectory) ? AssetCopyOptions.Never : CopyToOutputDirectory;
        CopyToPublishDirectory = string.IsNullOrEmpty(CopyToPublishDirectory) ? AssetCopyOptions.PreserveNewest : CopyToPublishDirectory;
        (Fingerprint, Integrity) = ComputeFingerprintAndIntegrity();
        AssetKind = !string.IsNullOrEmpty(AssetKind) ? AssetKind : !ShouldCopyToPublishDirectory() ? AssetKinds.Build : AssetKinds.All;
        AssetMode = string.IsNullOrEmpty(AssetMode) ? AssetModes.All : AssetMode;
        AssetRole = string.IsNullOrEmpty(AssetRole) ? AssetRoles.Primary : AssetRole;
    }

    private (string Fingerprint, string Integrity) ComputeFingerprintAndIntegrity() =>
        (Fingerprint, Integrity) switch
        {
            ("", "") => ComputeFingerprintAndIntegrity(Identity, OriginalItemSpec),
            (not null, not null) => (Fingerprint, Integrity),
            _ => ComputeFingerprintAndIntegrity(Identity, OriginalItemSpec)
        };

    internal static (string fingerprint, string integrity) ComputeFingerprintAndIntegrity(string identity, string originalItemSpec)
    {
        var fileInfo = ResolveFile(identity, originalItemSpec);
        return ComputeFingerprintAndIntegrity(fileInfo);
    }

    internal static (string fingerprint, string integrity) ComputeFingerprintAndIntegrity(FileInfo fileInfo)
    {
        using var file = fileInfo.OpenRead();

#if NET6_0_OR_GREATER
        var hash = SHA256.HashData(file);
#else
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(file);
#endif
        return (FileHasher.ToBase36(hash), Convert.ToBase64String(hash));
    }

    internal static string ComputeIntegrity(string identity, string originalItemSpec)
    {
        var fileInfo = ResolveFile(identity, originalItemSpec);
        return ComputeIntegrity(fileInfo);
    }

    internal static string ComputeIntegrity(FileInfo fileInfo)
    {
        using var file = fileInfo.OpenRead();

#if NET6_0_OR_GREATER
        var hash = SHA256.HashData(file);
#else
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(file);
#endif
        return Convert.ToBase64String(hash);
    }

    public string ComputeTargetPath(string pathPrefix, char separator) => CombineNormalizedPaths(
            pathPrefix,
            IsDiscovered() || IsComputed() ? "" : BasePath,
            RelativePath, separator);

    public static string CombineNormalizedPaths(string prefix, string basePath, string route, char separator)
    {
        var normalizedPrefix = prefix != null ? Normalize(prefix) : "";
        var computedBasePath = basePath is null or "/" ? "" : basePath;
        return Path.Combine(normalizedPrefix, computedBasePath, route)
            .Replace('/', separator)
            .Replace('\\', separator)
            .TrimStart(separator);
    }

    public ITaskItem ToTaskItem()
    {
        var result = new TaskItem(Identity);
        result.SetMetadata(nameof(SourceType), SourceType);
        result.SetMetadata(nameof(SourceId), SourceId);
        result.SetMetadata(nameof(ContentRoot), ContentRoot);
        result.SetMetadata(nameof(BasePath), BasePath);
        result.SetMetadata(nameof(RelativePath), RelativePath);
        result.SetMetadata(nameof(AssetKind), AssetKind);
        result.SetMetadata(nameof(AssetMode), AssetMode);
        result.SetMetadata(nameof(AssetRole), AssetRole);
        result.SetMetadata(nameof(AssetMergeSource), AssetMergeSource);
        result.SetMetadata(nameof(AssetMergeBehavior), AssetMergeBehavior);
        result.SetMetadata(nameof(RelatedAsset), RelatedAsset);
        result.SetMetadata(nameof(AssetTraitName), AssetTraitName);
        result.SetMetadata(nameof(AssetTraitValue), AssetTraitValue);
        result.SetMetadata(nameof(Fingerprint), Fingerprint);
        result.SetMetadata(nameof(Integrity), Integrity);
        result.SetMetadata(nameof(CopyToOutputDirectory), CopyToOutputDirectory);
        result.SetMetadata(nameof(CopyToPublishDirectory), CopyToPublishDirectory);
        result.SetMetadata(nameof(OriginalItemSpec), OriginalItemSpec);
        return result;
    }

    public void Validate()
    {
        switch (SourceType)
        {
            case SourceTypes.Discovered:
            case SourceTypes.Computed:
            case SourceTypes.Project:
            case SourceTypes.Package:
                break;
            default:
                throw new InvalidOperationException($"Unknown source type '{SourceType}' for '{Identity}'.");
        }
        ;

        if (string.IsNullOrEmpty(SourceId))
        {
            throw new InvalidOperationException($"The '{nameof(SourceId)}' for the asset must be defined for '{Identity}'.");
        }

        if (string.IsNullOrEmpty(ContentRoot))
        {
            throw new InvalidOperationException($"The '{nameof(ContentRoot)}' for the asset must be defined for '{Identity}'.");
        }

        if (string.IsNullOrEmpty(BasePath))
        {
            throw new InvalidOperationException($"The '{nameof(BasePath)}' for the asset must be defined for '{Identity}'.");
        }

        if (string.IsNullOrEmpty(RelativePath))
        {
            throw new InvalidOperationException($"The '{nameof(RelativePath)}' for the asset must be defined for '{Identity}'.");
        }

        if (string.IsNullOrEmpty(OriginalItemSpec))
        {
            throw new InvalidOperationException($"The '{nameof(OriginalItemSpec)}' for the asset must be defined for '{Identity}'.");
        }

        switch (AssetKind)
        {
            case AssetKinds.All:
            case AssetKinds.Build:
            case AssetKinds.Publish:
                break;
            default:
                throw new InvalidOperationException($"Unknown Asset kind '{AssetKind}' for '{Identity}'.");
        }
        ;

        switch (AssetMode)
        {
            case AssetModes.All:
            case AssetModes.CurrentProject:
            case AssetModes.Reference:
                break;
            default:
                throw new InvalidOperationException($"Unknown Asset mode '{AssetMode}' for '{Identity}'.");
        }
        ;

        switch (AssetRole)
        {
            case AssetRoles.Primary:
            case AssetRoles.Related:
            case AssetRoles.Alternative:
                break;
            default:
                throw new InvalidOperationException($"Unknown Asset role '{AssetRole}' for '{Identity}'.");
        }
        ;

        if (!IsPrimaryAsset() && string.IsNullOrEmpty(RelatedAsset))
        {
            throw new InvalidOperationException($"Related asset for '{AssetRole}' asset '{Identity}' is not defined.");
        }

        if (IsAlternativeAsset() && (string.IsNullOrEmpty(AssetTraitName) || string.IsNullOrEmpty(AssetTraitValue)))
        {
            throw new InvalidOperationException($"Alternative asset '{Identity}' does not define an asset trait name or value.");
        }

        if (string.IsNullOrEmpty(Fingerprint))
        {
            throw new InvalidOperationException($"Fingerprint for '{Identity}' is not defined.");
        }

        if (string.IsNullOrEmpty(Integrity))
        {
            throw new InvalidOperationException($"Integrity for '{Identity}' is not defined.");
        }
    }

    internal static StaticWebAsset FromProperties(
        string identity,
        string sourceId,
        string sourceType,
        string basePath,
        string relativePath,
        string contentRoot,
        string assetKind,
        string assetMode,
        string assetRole,
        string assetMergeSource,
        string relatedAsset,
        string assetTraitName,
        string assetTraitValue,
        string fingerprint,
        string integrity,
        string copyToOutputDirectory,
        string copyToPublishDirectory,
        string originalItemSpec)
    {
        var result = new StaticWebAsset
        {
            Identity = identity,
            SourceId = sourceId,
            SourceType = sourceType,
            ContentRoot = contentRoot,
            BasePath = basePath,
            RelativePath = relativePath,
            AssetKind = assetKind,
            AssetMode = assetMode,
            AssetRole = assetRole,
            AssetMergeSource = assetMergeSource,
            RelatedAsset = relatedAsset,
            AssetTraitName = assetTraitName,
            AssetTraitValue = assetTraitValue,
            Fingerprint = fingerprint,
            Integrity = integrity,
            CopyToOutputDirectory = copyToOutputDirectory,
            CopyToPublishDirectory = copyToPublishDirectory,
            OriginalItemSpec = originalItemSpec
        };

        result.ApplyDefaults();

        result.Normalize();
        result.Validate();

        return result;
    }

    internal bool HasSourceId(string source) =>
        HasSourceId(SourceId, source);

    public void Normalize()
    {
        ContentRoot = !string.IsNullOrEmpty(ContentRoot) ? NormalizeContentRootPath(ContentRoot) : ContentRoot;
        BasePath = Normalize(BasePath);
        RelativePath = Normalize(RelativePath, allowEmpyPath: true);
        RelatedAsset = !string.IsNullOrEmpty(RelatedAsset) ? Path.GetFullPath(RelatedAsset) : RelatedAsset;
    }

    // Normalizes the given path to a content root path in the way we expect it:
    // * Converts the path to absolute with Path.GetFullPath(path) which takes care of normalizing
    //   the directory separators to use Path.DirectorySeparator
    // * Appends a trailing directory separator at the end.
    public static string NormalizeContentRootPath(string path)
        => Path.GetFullPath(path) +
        // We need to do .ToString because there is no EndsWith overload for chars in .net472
        (path.EndsWith(Path.DirectorySeparatorChar.ToString()), path.EndsWith(Path.AltDirectorySeparatorChar.ToString())) switch
        {
            (true, _) => "",
            (false, true) => "", // Path.GetFullPath will have normalized it to Path.DirectorySeparatorChar.
            (false, false) => Path.DirectorySeparatorChar
        };

    public bool IsComputed()
        => string.Equals(SourceType, SourceTypes.Computed, StringComparison.Ordinal);

    public bool IsDiscovered()
        => string.Equals(SourceType, SourceTypes.Discovered, StringComparison.Ordinal);

    public bool IsProject()
        => string.Equals(SourceType, SourceTypes.Project, StringComparison.Ordinal);

    public bool IsPackage()
        => string.Equals(SourceType, SourceTypes.Package, StringComparison.Ordinal);

    public bool IsBuildOnly()
        => string.Equals(AssetKind, AssetKinds.Build, StringComparison.Ordinal);

    public bool IsPublishOnly()
        => string.Equals(AssetKind, AssetKinds.Publish, StringComparison.Ordinal);

    public bool IsBuildAndPublish()
        => string.Equals(AssetKind, AssetKinds.All, StringComparison.Ordinal);

    public bool IsForCurrentProjectOnly()
        => string.Equals(AssetMode, AssetModes.CurrentProject, StringComparison.Ordinal);

    public bool IsForReferencedProjectsOnly()
        => string.Equals(AssetMode, AssetModes.Reference, StringComparison.Ordinal);

    public bool IsForCurrentAndReferencedProjects()
        => string.Equals(AssetMode, AssetModes.All, StringComparison.Ordinal);

    public bool IsPrimaryAsset()
        => string.Equals(AssetRole, AssetRoles.Primary, StringComparison.Ordinal);

    public bool IsRelatedAsset()
        => string.Equals(AssetRole, AssetRoles.Related, StringComparison.Ordinal);

    public bool IsAlternativeAsset()
        => string.Equals(AssetRole, AssetRoles.Alternative, StringComparison.Ordinal);

    public bool ShouldCopyToOutputDirectory()
        => !string.Equals(CopyToOutputDirectory, AssetCopyOptions.Never, StringComparison.Ordinal);

    public bool ShouldCopyToPublishDirectory()
        => !string.Equals(CopyToPublishDirectory, AssetCopyOptions.Never, StringComparison.Ordinal);

    public bool HasContentRoot(string path) =>
        string.Equals(ContentRoot, NormalizeContentRootPath(path), StringComparison.Ordinal);

    public static string Normalize(string path, bool allowEmpyPath = false)
    {
        var normalizedPath = path.Replace('\\', '/').Trim('/');
        return !allowEmpyPath && normalizedPath.Equals("") ? "/" : normalizedPath;
    }

    public static string ComputeAssetRelativePath(ITaskItem asset, out string metadataProperty)
    {
        var relativePath = asset.GetMetadata("RelativePath");
        if (!string.IsNullOrEmpty(relativePath))
        {
            metadataProperty = "RelativePath";
            return relativePath;
        }

        var targetPath = asset.GetMetadata("TargetPath");
        if (!string.IsNullOrEmpty(targetPath))
        {
            metadataProperty = "TargetPath";
            return targetPath;
        }

        var linkPath = asset.GetMetadata("Link");
        if (!string.IsNullOrEmpty(linkPath))
        {
            metadataProperty = "Link";
            return linkPath;
        }

        metadataProperty = null;
        return asset.ItemSpec;
    }

    // Compares all fields in this order
    // Identity
    // SourceType
    // SourceId
    // ContentRoot
    // BasePath
    // RelativePath
    // AssetKind
    // AssetMode
    // AssetRole
    // AssetMergeSource
    // AssetMergeBehavior
    // RelatedAsset
    // AssetTraitName
    // AssetTraitValue
    // Fingerprint
    // Integrity
    // CopyToOutputDirectory
    // CopyToPublishDirectory
    // OriginalItemSpec

    public int CompareTo(StaticWebAsset other)
    {
        var result = string.Compare(Identity, other.Identity, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(SourceType, other.SourceType, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(SourceId, other.SourceId, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(ContentRoot, other.ContentRoot, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(BasePath, other.BasePath, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(RelativePath, other.RelativePath, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetKind, other.AssetKind, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetMode, other.AssetMode, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetRole, other.AssetRole, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetMergeSource, other.AssetMergeSource, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetMergeBehavior, other.AssetMergeBehavior, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(RelatedAsset, other.RelatedAsset, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetTraitName, other.AssetTraitName, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(AssetTraitValue, other.AssetTraitValue, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(Integrity, other.Integrity, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(CopyToOutputDirectory, other.CopyToOutputDirectory, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(CopyToPublishDirectory, other.CopyToPublishDirectory, StringComparison.Ordinal);
        if (result != 0)
        {
            return result;
        }

        result = string.Compare(OriginalItemSpec, other.OriginalItemSpec, StringComparison.Ordinal);
        return result;
    }

    public override bool Equals(object obj) => obj != null && Equals(obj as StaticWebAsset);

    public bool Equals(StaticWebAsset other) =>
        Identity == other.Identity &&
        SourceType == other.SourceType &&
        SourceId == other.SourceId &&
        ContentRoot == other.ContentRoot &&
        BasePath == other.BasePath &&
        RelativePath == other.RelativePath &&
        AssetKind == other.AssetKind &&
        AssetMode == other.AssetMode &&
        AssetRole == other.AssetRole &&
        AssetMergeSource == other.AssetMergeSource &&
        AssetMergeBehavior == other.AssetMergeBehavior &&
        RelatedAsset == other.RelatedAsset &&
        AssetTraitName == other.AssetTraitName &&
        AssetTraitValue == other.AssetTraitValue &&
        Fingerprint == other.Fingerprint &&
        Integrity == other.Integrity &&
        CopyToOutputDirectory == other.CopyToOutputDirectory &&
        CopyToPublishDirectory == other.CopyToPublishDirectory &&
        OriginalItemSpec == other.OriginalItemSpec;

    public static class AssetModes
    {
        public const string CurrentProject = nameof(CurrentProject);
        public const string Reference = nameof(Reference);
        public const string All = nameof(All);
    }

    public static class AssetKinds
    {
        public const string Build = nameof(Build);
        public const string Publish = nameof(Publish);
        public const string All = nameof(All);

        public static bool IsPublish(string assetKind) => string.Equals(Publish, assetKind, StringComparison.Ordinal);
        public static bool IsBuild(string assetKind) => string.Equals(Build, assetKind, StringComparison.Ordinal);
        internal static bool IsKind(string candidate, string assetKind) => string.Equals(candidate, assetKind, StringComparison.Ordinal);
        internal static bool IsAll(string assetKind) => string.Equals(All, assetKind, StringComparison.Ordinal);
    }

    public static class SourceTypes
    {
        public const string Discovered = nameof(Discovered);
        public const string Computed = nameof(Computed);
        public const string Project = nameof(Project);
        public const string Package = nameof(Package);

        public static bool IsPackage(string sourceType) => string.Equals(Package, sourceType, StringComparison.Ordinal);
    }

    public static class AssetCopyOptions
    {
        public const string Never = nameof(Never);
        public const string PreserveNewest = nameof(PreserveNewest);
        public const string Always = nameof(Always);
    }

    public static class AssetRoles
    {
        public const string Primary = nameof(Primary);
        public const string Related = nameof(Related);
        public const string Alternative = nameof(Alternative);

        internal static bool IsPrimary(string assetRole)
            => string.Equals(assetRole, Primary, StringComparison.Ordinal);
    }

    public static class MergeBehaviors
    {
        public const string Exclude = nameof(Exclude);
        public const string PreferTarget = nameof(PreferTarget);
        public const string PreferSource = nameof(PreferSource);
        public const string None = nameof(None);
    }

    internal static bool HasSourceId(ITaskItem asset, string source) =>
        string.Equals(asset.GetMetadata(nameof(SourceId)), source, StringComparison.Ordinal);

    internal static bool HasSourceId(string candidate, string source) =>
        string.Equals(candidate, source, StringComparison.Ordinal);

    private string GetDebuggerDisplay() => ToString();

    public string ComputeLogicalPath() => CreatePathString("", '/');

    private string CreatePathString(string pathPrefix, char separator)
    {
        var prefix = pathPrefix != null ? Normalize(pathPrefix) : "";
        // These have been normalized already, so only contain forward slashes
        var computedBasePath = IsDiscovered() || IsComputed() ? "" : BasePath;
        if (computedBasePath == "/")
        {
            // We need to special case the base path "/" to make sure it gets correctly combined with the prefix
            computedBasePath = "";
        }

        var pathWithTokens = Path.Combine(prefix, computedBasePath, RelativePath)
            .Replace('/', separator)
            .Replace('\\', separator)
            .TrimStart(separator);
        return pathWithTokens;
    }

    public string ComputeTargetPath(string pathPrefix, char separator, StaticWebAssetTokenResolver providedTokens)
    {
        var pathWithTokens = CreatePathString(pathPrefix, separator);
        return ReplaceTokens(pathWithTokens, providedTokens);
    }

    // Tokens in static web assets represent a similar concept to tokens within routing. They can be used to identify logical
    // values that need to be replaced by well-known strings. The format for defining a token in static web assets is as follows
    // #[.{tokenName}].
    // # is used to make sure we never interpret any valid file path as a token (since # is not allowed to appear in file systems)
    // [] delimit the token expression.
    // Inside the [] there is a token expression that is represented as an interpolated string where {} delimit the variables and
    // the content inside the name of the value they need to be replaced with.
    // The expression inside the `[]` can contain any character that can appear in the file system, for example, to indicate that
    // a fixed prefix needs to be added.
    // For the time being the implementation unconditionally resolves and implements any token expression, but in the future,
    // we will support features like `?` to indicate that an entire token expression is optional (this indicates that something
    // can be referenced with or without the expression. For example file[.{integrity}]?.js will mean, the file can be addressed as
    // file.js (no integrity  suffix) or file.asdfasdf.js where '.asdfasdf' is the integrity suffix.
    // The reason we want to plan for this is that we don't have the ability to post process all content from the app (CSS files, JS, etc.)
    // to replace the original paths with the replaced paths. This means some files should be served in their original formats so that they
    // work with the content that we couldn't post process, and with the post processed format, so that they can benefit from fingerprinting
    // and other features. This is why we want to bake into the format itself the information that specifies under which paths the file will
    // be available at runtime so that tasks/tools can operate independently and produce correct results.
    // The current token we support is the 'fingerprint' token, which computes a web friendly version of the hash of the file suitable
    // to be embedded in other contexts.
    // We might include other tokens in the future, like `[{basepath}]` to give a file the ability to have its path be relative to the consuming
    // project base path, etc.
    public string ReplaceTokens(string pathWithTokens, StaticWebAssetTokenResolver tokens)
    {
        var pattern = StaticWebAssetPathPattern.Parse(pathWithTokens, Identity);
        return pattern.ReplaceTokens(this, tokens, applyPreferences: true).Path;
    }

    public string ComputePathWithoutTokens(string pathWithTokens)
    {
        var pattern = StaticWebAssetPathPattern.Parse(pathWithTokens, Identity);
        return pattern.ComputePatternLabel();
    }

    public override string ToString() =>
        $"Identity: {Identity}, " +
        $"SourceType: {SourceType}, " +
        $"SourceId: {SourceId}, " +
        $"ContentRoot: {ContentRoot}, " +
        $"BasePath: {BasePath}, " +
        $"RelativePath: {RelativePath}, " +
        $"AssetKind: {AssetKind}, " +
        $"AssetMode: {AssetMode}, " +
        $"AssetRole: {AssetRole}, " +
        $"AssetRole: {AssetMergeSource}, " +
        $"AssetRole: {AssetMergeBehavior}, " +
        $"RelatedAsset: {RelatedAsset}, " +
        $"AssetTraitName: {AssetTraitName}, " +
        $"AssetTraitValue: {AssetTraitValue}, " +
        $"Fingerprint: {Fingerprint}, " +
        $"Integrity: {Integrity}, " +
        $"CopyToOutputDirectory: {CopyToOutputDirectory}, " +
        $"CopyToPublishDirectory: {CopyToPublishDirectory}, " +
        $"OriginalItemSpec: {OriginalItemSpec}";

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        var hash = new HashCode();
        hash.Add(Identity);
        hash.Add(SourceType);
        hash.Add(SourceId);
        hash.Add(ContentRoot);
        hash.Add(BasePath);
        hash.Add(RelativePath);
        hash.Add(AssetKind);
        hash.Add(AssetMode);
        hash.Add(AssetRole);
        hash.Add(AssetMergeSource);
        hash.Add(AssetMergeBehavior);
        hash.Add(RelatedAsset);
        hash.Add(AssetTraitName);
        hash.Add(AssetTraitValue);
        hash.Add(Fingerprint);
        hash.Add(Integrity);
        hash.Add(CopyToOutputDirectory);
        hash.Add(CopyToPublishDirectory);
        hash.Add(OriginalItemSpec);
        return hash.ToHashCode();
#else
        var hashCode = 1447485498;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Identity);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SourceType);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SourceId);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ContentRoot);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BasePath);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(RelativePath);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetKind);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetMode);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetRole);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetMergeSource);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetMergeBehavior);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(RelatedAsset);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetTraitName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetTraitValue);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Fingerprint);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Integrity);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(CopyToOutputDirectory);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(CopyToPublishDirectory);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(OriginalItemSpec);
        return hashCode;
#endif
    }

    internal IEnumerable<StaticWebAssetResolvedRoute> ComputeRoutes()
    {
        var tokenResolver = StaticWebAssetTokenResolver.Instance;
        var pattern = StaticWebAssetPathPattern.Parse(RelativePath, Identity);
        foreach (var expandedPattern in pattern.ExpandPatternExpression())
        {
            var (path, tokens) = expandedPattern.ReplaceTokens(this, tokenResolver);
            yield return new StaticWebAssetResolvedRoute(pattern.ComputePatternLabel(), path, tokens);
        }
    }

    internal string EmbedTokens(string relativePath)
    {
        var pattern = StaticWebAssetPathPattern.Parse(relativePath, Identity);
        var resolver = StaticWebAssetTokenResolver.Instance;
        pattern.EmbedTokens(this, resolver);
        return pattern.RawPattern.ToString();
    }

    internal FileInfo ResolveFile() => ResolveFile(Identity, OriginalItemSpec);

    internal static FileInfo ResolveFile(string identity, string originalItemSpec)
    {
        var fileInfo = new FileInfo(identity);
        if (fileInfo.Exists)
        {
            return fileInfo;
        }
        fileInfo = new FileInfo(originalItemSpec);
        if (fileInfo.Exists)
        {
            return fileInfo;
        }

        throw new InvalidOperationException($"No file exists for the asset at either location '{identity}' or '{originalItemSpec}'.");
    }

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    internal sealed class StaticWebAssetResolvedRoute(string pathLabel, string path, Dictionary<string, string> tokens)
    {
        public string PathLabel { get; set; } = pathLabel;
        public string Path { get; set; } = path;
        public Dictionary<string, string> Tokens { get; set; } = tokens;

        public void Deconstruct(out string pathLabel, out string path, out Dictionary<string, string> tokens)
        {
            pathLabel = PathLabel;
            path = Path;
            tokens = Tokens;
        }

        private string GetDebuggerDisplay() =>
            $"Label: {PathLabel}, Route: {Path}, Tokens: {string.Join(", ", Tokens.Select(t => $"{t.Key}={t.Value}"))}";
    }
}
