// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticWebAssets.Tasks.Utils;
using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
#if WASM_TASKS
internal sealed class StaticWebAsset : IEquatable<StaticWebAsset>, IComparable<StaticWebAsset>, ITaskItem2
#else
public sealed class StaticWebAsset : IEquatable<StaticWebAsset>, IComparable<StaticWebAsset>, ITaskItem2
#endif
{
    public const string DateTimeAssetFormat = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";

    private bool _modified;
    private ITaskItem _originalItem;
    private string _identity;
    private string _sourceId;
    private string _sourceType;
    private string _contentRoot;
    private string _basePath;
    private string _relativePath;
    private string _assetKind;
    private string _assetMode;
    private string _assetRole;
    private string _assetMergeBehavior;
    private string _assetMergeSource;
    private string _relatedAsset;
    private string _assetTraitName;
    private string _assetTraitValue;
    private string _fingerprint;
    private string _integrity;
    private string _copyToOutputDirectory;
    private string _copyToPublishDirectory;
    private string _originalItemSpec;
    private long _fileLength = -1;
    private DateTimeOffset _lastWriteTime = DateTimeOffset.MinValue;
    private Dictionary<string, string> _additionalCustomMetadata;
    private string _fileLengthString;
    private string _lastWriteTimeString;

    public StaticWebAsset()
    {
    }

    public StaticWebAsset(StaticWebAsset asset)
    {
        _identity = asset.Identity;
        _sourceType = asset.SourceType;
        _sourceId = asset.SourceId;
        _contentRoot = asset.ContentRoot;
        _basePath = asset.BasePath;
        _relativePath = asset.RelativePath;
        _assetKind = asset.AssetKind;
        _assetMode = asset.AssetMode;
        _assetRole = asset.AssetRole;
        _assetMergeBehavior = asset.AssetMergeBehavior;
        _assetMergeSource = asset.AssetMergeSource;
        _relatedAsset = asset.RelatedAsset;
        _assetTraitName = asset.AssetTraitName;
        _assetTraitValue = asset.AssetTraitValue;
        _copyToOutputDirectory = asset.CopyToOutputDirectory;
        _copyToPublishDirectory = asset.CopyToPublishDirectory;
        _originalItemSpec = asset.OriginalItemSpec;
        _fileLength = asset.FileLength;
        _lastWriteTime = asset.LastWriteTime;
        _fingerprint = asset.Fingerprint;
        _integrity = asset.Integrity;
    }

    private string GetOriginalItemMetadata(string name) => _originalItem?.GetMetadata(name);

    public string Identity
    {
        get
        {
            return _identity ??=
                // Register the identity as the full path since assets might have come
                // from packages and other sources and the identity (which is typically
                // just the relative path from the project) is not enough to locate them.
                GetOriginalItemMetadata("FullPath");
        }

        set
        {
            _modified = true;
            _identity = value;
        }
    }

    public string SourceId
    {
        get => _sourceId ??= GetOriginalItemMetadata(nameof(SourceId));
        set
        {
            _modified = true;
            _sourceId = value;
        }
    }

    public string SourceType
    {
        get => _sourceType ??= GetOriginalItemMetadata(nameof(SourceType));
        set
        {
            _modified = true;
            _sourceType = value;
        }
    }

    public string ContentRoot
    {
        get => _contentRoot ??= GetOriginalItemMetadata(nameof(ContentRoot));
        set
        {
            _modified = true;
            _contentRoot = value;
        }
    }

    public string BasePath
    {
        get => _basePath ??= GetOriginalItemMetadata(nameof(BasePath));
        set
        {
            _modified = true;
            _basePath = value;
        }
    }

    public string RelativePath
    {
        get => _relativePath ??= GetOriginalItemMetadata(nameof(RelativePath));
        set
        {
            _modified = true;
            _relativePath = value;
        }
    }

    public string AssetKind
    {
        get => _assetKind ??= GetOriginalItemMetadata(nameof(AssetKind));
        set
        {
            _modified = true;
            _assetKind = value;
        }
    }

    public string AssetMode
    {
        get => _assetMode ??= GetOriginalItemMetadata(nameof(AssetMode));
        set
        {
            _modified = true;
            _assetMode = value;
        }
    }

    public string AssetRole
    {
        get => _assetRole ??= GetOriginalItemMetadata(nameof(AssetRole));
        set
        {
            _modified = true;
            _assetRole = value;
        }
    }

    public string AssetMergeBehavior
    {
        get => _assetMergeBehavior ??= GetOriginalItemMetadata(nameof(AssetMergeBehavior));
        set
        {
            _modified = true;
            _assetMergeBehavior = value;
        }
    }

    public string AssetMergeSource
    {
        get => _assetMergeSource ??= GetOriginalItemMetadata(nameof(AssetMergeSource));
        set
        {
            _modified = true;
            _assetMergeSource = value;
        }
    }

    public string RelatedAsset
    {
        get => _relatedAsset ??= GetOriginalItemMetadata(nameof(RelatedAsset));
        set
        {
            _modified = true;
            _relatedAsset = value;
        }
    }

    public string AssetTraitName
    {
        get => _assetTraitName ??= GetOriginalItemMetadata(nameof(AssetTraitName));
        set
        {
            _modified = true;
            _assetTraitName = value;
        }
    }

    public string AssetTraitValue
    {
        get => _assetTraitValue ??= GetOriginalItemMetadata(nameof(AssetTraitValue));
        set
        {
            _modified = true;
            _assetTraitValue = value;
        }
    }

    public string Fingerprint
    {
        get => _fingerprint ??= GetOriginalItemMetadata(nameof(Fingerprint));
        set
        {
            _modified = true;
            _fingerprint = value;
        }
    }

    public string Integrity
    {
        get => _integrity ??= GetOriginalItemMetadata(nameof(Integrity));
        set
        {
            _modified = true;
            _integrity = value;
        }
    }

    public string CopyToOutputDirectory
    {
        get => _copyToOutputDirectory ??= GetOriginalItemMetadata(nameof(CopyToOutputDirectory));
        set
        {
            _modified = true;
            _copyToOutputDirectory = value;
        }
    }

    public string CopyToPublishDirectory
    {
        get => _copyToPublishDirectory ??= GetOriginalItemMetadata(nameof(CopyToPublishDirectory));
        set
        {
            _modified = true;
            _copyToPublishDirectory = value;
        }
    }

    public string OriginalItemSpec
    {
        get => _originalItemSpec ??= GetOriginalItemMetadata(nameof(OriginalItemSpec));
        set
        {
            _modified = true;
            _originalItemSpec = value;
        }
    }

    internal string FileLengthString => _fileLengthString ??= GetOriginalItemMetadata(nameof(FileLength));

    public long FileLength
    {
        get
        {
            if (_fileLength < 0)
            {
                var fileLengthString = FileLengthString;
                _fileLength = !string.IsNullOrEmpty(fileLengthString) &&
                    long.TryParse(fileLengthString, NumberStyles.None, CultureInfo.InvariantCulture, out var fileLength)
                    ? fileLength
                    : -1;
            }
            return _fileLength;
        }
        set
        {
            _fileLengthString = null;
            _modified = true;
            _fileLength = value;
        }
    }

    internal string LastWriteTimeString => _lastWriteTimeString ??= GetOriginalItemMetadata(nameof(LastWriteTime));

    public DateTimeOffset LastWriteTime
    {
        get
        {
            if (_lastWriteTime == DateTimeOffset.MinValue)
            {
                var lastWriteTimeString = LastWriteTimeString;
                _lastWriteTime = !string.IsNullOrEmpty(lastWriteTimeString) &&
                    DateTimeOffset.TryParse(lastWriteTimeString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastWriteTime)
                    ? lastWriteTime
                    : DateTimeOffset.MinValue;
            }
            return _lastWriteTime;
        }
        set
        {
            _lastWriteTimeString = null;
            _modified = true;
            _lastWriteTime = value;
        }
    }

    public static StaticWebAsset FromTaskItem(ITaskItem item, bool validate = false)
    {
        var result = FromTaskItemCore(item);

        result.Normalize();
        if (validate)
        {
            result.Validate();
        }

        return result;
    }

    // Iterate over the list of assets with the same Identity and choose the one closest to the asset kind we've been given:
    // The given asset kind here will always be Build or Publish.
    // We need to iterate over the assets, the moment we detect one asset for our specific kind, we return that asset
    // While we iterate over the list of assets we keep any asset of the `All` kind we find on a variable.
    // * If we find a more specific asset, we will ignore it in favor of the specific one.
    // * If we don't find a more specific (Build or Publish) asset we will return the `All` asset.
    // We assume that the manifest is correct and don't try to deal with errors at this level, if for some reason we find more
    // than one type of asset we will just return all of them.
    // One exception to this is the `All` kind of assets, where we will just return the first two we find. The reason for it is
    // to avoid having to allocate a buffer to collect all the `All` assets.
    internal static IEnumerable<StaticWebAsset> ChooseNearestAssetKind(IEnumerable<StaticWebAsset> group, string assetKind)
    {
        StaticWebAsset allKindAssetCandidate = null;

        var ignoreAllKind = false;
        foreach (var item in group)
        {
            if (item.HasKind(assetKind))
            {
                // The moment we find a Build or Publish asset, we start ignoring the
                // All assets
                ignoreAllKind = true;

                // We still return multiple Build or Publish items if they are present
                // But we won't error out if there are multiple All assets as long as there
                // is a single Build or Publish asset present.
                yield return item;
            }
            else if (!ignoreAllKind && item.IsBuildAndPublish())
            {
                if (allKindAssetCandidate != null)
                {
                    // At this point we have more than one `All` asset, which is an error
                    yield return allKindAssetCandidate;
                    yield return item;
                    yield break;
                }
                allKindAssetCandidate = item;
            }
        }

        if (!ignoreAllKind)
        {
            yield return allKindAssetCandidate;
        }
    }

    internal static bool ValidateAssetGroup(string path, (StaticWebAsset First, StaticWebAsset Second, IReadOnlyList<StaticWebAsset> Others) group, out string reason)
    {
        var prototypeItem = group.First;
        StaticWebAsset build = null;
        StaticWebAsset publish = null;
        StaticWebAsset all = null;

        if (group.Second == null)
        {
            // Most common case, only one asset for the given path
            reason = null;
            return true;
        }

        // Check First against Second for source ID conflict
        if (!prototypeItem.HasSourceId(group.Second.SourceId))
        {
            reason = $"Conflicting assets with the same target path '{path}'. For assets '{prototypeItem}' and '{group.Second}' from different projects.";
            return false;
        }

        // Process First
        build = group.First.IsBuildOnly() ? group.First : build;
        publish = group.First.IsPublishOnly() ? group.First : publish;
        all = group.First.IsBuildAndPublish() ? group.First : all;

        // Process Second
        if (build != null && group.Second.IsBuildOnly() && !ReferenceEquals(build, group.Second))
        {
            reason = $"Conflicting assets with the same target path '{path}'. For 'Build' assets '{build}' and '{group.Second}'.";
            return false;
        }
        build ??= group.Second.IsBuildOnly() ? group.Second : build;

        if (publish != null && group.Second.IsPublishOnly() && !ReferenceEquals(publish, group.Second))
        {
            reason = $"Conflicting assets with the same target path '{path}'. For 'Publish' assets '{publish}' and '{group.Second}'.";
            return false;
        }
        publish ??= group.Second.IsPublishOnly() ? group.Second : publish;

        if (all != null && group.Second.IsBuildAndPublish() && !ReferenceEquals(all, group.Second))
        {
            reason = $"Conflicting assets with the same target path '{path}'. For 'All' assets '{all}' and '{group.Second}'.";
            return false;
        }
        all ??= group.Second.IsBuildAndPublish() ? group.Second : all;

        if (group.Others == null || group.Others.Count == 0)
        {
            reason = null;
            return true;
        }

        // Process rest of the items
        foreach (var item in group.Others)
        {
            if (!prototypeItem.HasSourceId(item.SourceId))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For assets '{prototypeItem}' and '{item}' from different projects.";
                return false;
            }

            if (build != null && item.IsBuildOnly() && !ReferenceEquals(build, item))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For 'Build' assets '{build}' and '{item}'.";
                return false;
            }
            build ??= item.IsBuildOnly() ? item : build;

            if (publish != null && item.IsPublishOnly() && !ReferenceEquals(publish, item))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For 'Publish' assets '{publish}' and '{item}'.";
                return false;
            }
            publish ??= item.IsPublishOnly() ? item : publish;

            if (all != null && item.IsBuildAndPublish() && !ReferenceEquals(all, item))
            {
                reason = $"Conflicting assets with the same target path '{path}'. For 'All' assets '{all}' and '{item}'.";
                return false;
            }
            all ??= item.IsBuildAndPublish() ? item : all;
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
        result.OriginalItemSpec = string.IsNullOrEmpty(result.OriginalItemSpec) ? item.GetMetadata("FullPath") : result.OriginalItemSpec;

        result.Normalize();
        result.Validate();

        return result;
    }

    private static StaticWebAsset FromTaskItemCore(ITaskItem item)
    {
        return new()
        {
            _originalItem = item,
        };
    }

    public void ApplyDefaults()
    {
        CopyToOutputDirectory = string.IsNullOrEmpty(CopyToOutputDirectory) ? AssetCopyOptions.Never : CopyToOutputDirectory;
        CopyToPublishDirectory = string.IsNullOrEmpty(CopyToPublishDirectory) ? AssetCopyOptions.PreserveNewest : CopyToPublishDirectory;
        AssetKind = !string.IsNullOrEmpty(AssetKind) ? AssetKind : !ShouldCopyToPublishDirectory() ? AssetKinds.Build : AssetKinds.All;
        AssetMode = string.IsNullOrEmpty(AssetMode) ? AssetModes.All : AssetMode;
        AssetRole = string.IsNullOrEmpty(AssetRole) ? AssetRoles.Primary : AssetRole;
        if (string.IsNullOrEmpty(Fingerprint) || string.IsNullOrEmpty(Integrity) || FileLength == -1 || LastWriteTime == DateTimeOffset.MinValue)
        {
            var file = ResolveFile(Identity, OriginalItemSpec);
            (Fingerprint, Integrity) = string.IsNullOrEmpty(Fingerprint) || string.IsNullOrEmpty(Integrity) ?
                ComputeFingerprintAndIntegrityIfNeeded(file) : (Fingerprint, Integrity);
            FileLength = FileLength == -1 ? file.Length : FileLength;
            LastWriteTime = LastWriteTime == DateTimeOffset.MinValue ? file.LastWriteTimeUtc : LastWriteTime;
        }
    }

    private (string Fingerprint, string Integrity) ComputeFingerprintAndIntegrityIfNeeded(FileInfo file) =>
        (Fingerprint, Integrity) switch
        {
            ("", "") => ComputeFingerprintAndIntegrity(file),
            (not null, not null) => (Fingerprint, Integrity),
            _ => ComputeFingerprintAndIntegrity(file)
        };

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
        if (!_modified && _originalItem != null)
        {
            // We haven't modified the item, we can just return the original item.
            // This is still interesting because MSBuild can optimize things and avoid
            // additional copies.
            return _originalItem;
        }
        // We can always return ourselves, any property that wasn't modified we will copy from the original item if exists.
        return this;
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

        switch (AssetMode)
        {
            case AssetModes.All:
            case AssetModes.CurrentProject:
            case AssetModes.Reference:
                break;
            default:
                throw new InvalidOperationException($"Unknown Asset mode '{AssetMode}' for '{Identity}'.");
        }

        switch (AssetRole)
        {
            case AssetRoles.Primary:
            case AssetRoles.Related:
            case AssetRoles.Alternative:
                break;
            default:
                throw new InvalidOperationException($"Unknown Asset role '{AssetRole}' for '{Identity}'.");
        }

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

        if (FileLength < 0)
        {
            throw new InvalidOperationException($"File length for '{Identity}' is not defined.");
        }

        if (LastWriteTime == DateTimeOffset.MinValue)
        {
            throw new InvalidOperationException($"Last write time for '{Identity}' is not defined.");
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
        string originalItemSpec,
        long fileLength,
        DateTimeOffset lastWriteTime)
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
            OriginalItemSpec = originalItemSpec,
            FileLength = fileLength,
            LastWriteTime = lastWriteTime,
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

        result = FileLength.CompareTo(other.FileLength);
        if (result != 0)
        {
            return result;
        }

        result = LastWriteTime.CompareTo(other.LastWriteTime);
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
        FileLength == other.FileLength &&
        LastWriteTime == other.LastWriteTime &&
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
        $"FileLength: {FileLength}, " +
        $"LastWriteTime: {LastWriteTime}, " +
        $"CopyToOutputDirectory: {CopyToOutputDirectory}, " +
        $"CopyToPublishDirectory: {CopyToPublishDirectory}, " +
        $"OriginalItemSpec: {OriginalItemSpec}";

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        var hash = new HashCode();
        hash.Add(Identity);
        hash.Add(SourceType);
        hash.Add(FileLength);
        hash.Add(LastWriteTime);
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
        hashCode = hashCode * -1521134295 + FileLength.GetHashCode();
        hashCode = hashCode * -1521134295 + LastWriteTime.GetHashCode();
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

    internal void ComputeRoutes(List<StaticWebAssetResolvedRoute> routes)
    {
        routes.Clear();
        var tokenResolver = StaticWebAssetTokenResolver.Instance;
        var pattern = StaticWebAssetPathPattern.Parse(RelativePath, Identity);
        foreach (var expandedPattern in pattern.ExpandPatternExpression())
        {
            var (path, tokens) = expandedPattern.ReplaceTokens(this, tokenResolver);
            routes.Add(new StaticWebAssetResolvedRoute(pattern.ComputePatternLabel(), path, tokens));
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

    internal static Dictionary<string, StaticWebAsset> ToAssetDictionary(ITaskItem[] candidateAssets, bool validate = false)
    {
        var dictionary = new Dictionary<string, StaticWebAsset>(candidateAssets.Length);
        for (var i = 0; i < candidateAssets.Length; i++)
        {
            var candidateAsset = FromTaskItem(candidateAssets[i], validate);
            dictionary.Add(candidateAsset.Identity, candidateAsset);
        }

        return dictionary;
    }

    internal static StaticWebAsset[] FromTaskItemGroup(ITaskItem[] candidateAssets, bool validate = false)
    {
        var result = new StaticWebAsset[candidateAssets.Length];
        for (var i = 0; i != result.Length; i++)
        {
            var candidateAsset = FromTaskItem(candidateAssets[i], validate);
            result[i] = candidateAsset;
        }
        return result;
    }

    internal static Dictionary<string, (StaticWebAsset, List<StaticWebAsset>)> AssetsByTargetPath(ITaskItem[] assets, string source, string assetKind)
    {
        // We return either the selected asset or a list with all the candidates that were found to be ambiguous
        var result = new Dictionary<string, (StaticWebAsset selected, List<StaticWebAsset> all)>();
        for (var i = 0; i < assets.Length; i++)
        {
            var candidate = assets[i];
            if (!HasSourceId(candidate, source))
            {
                continue;
            }
            if (HasOppositeKind(candidate, assetKind))
            {
                continue;
            }
            var asset = FromTaskItem(candidate);
            var key = asset.ComputeTargetPath("", '/');
            if (!result.TryGetValue(key, out var existing))
            {
                result[key] = (asset, null);
            }
            else
            {
                var (existingAsset, all) = existing;
                if (existingAsset == null)
                {
                    Debug.Assert(all != null);
                    // We are going to error out, just add to the list
                    all.Add(asset);
                }
                else if (existingAsset.AssetKind == asset.AssetKind)
                {
                    // We have an ambiguity because there are either two Build, Publish or All assets
                    result[key] = (null, [existingAsset, asset]);
                }
                else if (existingAsset.IsBuildAndPublish() && !asset.IsBuildAndPublish())
                {
                    // There is an All asset overriden by a Build or Publish asset.
                    result[key] = (asset, null);
                }
            }
        }
        return result;
    }

    private static bool HasOppositeKind(ITaskItem candidate, string assetKind)
    {
        var candidateKind = candidate.GetMetadata(nameof(AssetKind));
        return candidateKind switch
        {
            AssetKinds.Publish => assetKind switch
            {
                AssetKinds.Build => true,
                _ => false,
            },
            AssetKinds.Build => assetKind switch
            {
                AssetKinds.Publish => true,
                _ => false,
            },
            _ => false
        };
    }

    // We provide the minimal ITaskItem2 implementation so that we can return StaticWebAsset instances without having to convert them
    // to task items. This is because the underlying implementation uses an immutable dictionary and every call to SetMetadata results
    // in a new allocation.
    // When the task returns, MSBuild will convert the task into a ProjectItem instance and will copy the custom metadata, at which point
    // it will get rid of the instance that we returned and won't use it any longer.
    // For that reason, and since we control inside the tasks how this is used, we can safely ignore the pieces that MSBuild won't call.
    #region ITaskItem2 implementation

    string ITaskItem2.EvaluatedIncludeEscaped { get => Identity; set => Identity = value; }
    string ITaskItem.ItemSpec { get => Identity; set => Identity = value; }

    private static readonly string[] _defaultPropertyNames = [
        nameof(SourceId),
        nameof(SourceType),
        nameof(ContentRoot),
        nameof(BasePath),
        nameof(RelativePath),
        nameof(AssetKind),
        nameof(AssetMode),
        nameof(AssetRole),
        nameof(AssetMergeBehavior),
        nameof(AssetMergeSource),
        nameof(RelatedAsset),
        nameof(AssetTraitName),
        nameof(AssetTraitValue),
        nameof(Fingerprint),
        nameof(Integrity),
        nameof(CopyToOutputDirectory),
        nameof(CopyToPublishDirectory),
        nameof(OriginalItemSpec),
        nameof(FileLength),
        nameof(LastWriteTime)
    ];

    ICollection ITaskItem.MetadataNames
    {
        get
        {
            if (_additionalCustomMetadata == null)
            {
                return _defaultPropertyNames;
            }

            var result = new List<string>(_defaultPropertyNames.Length + _additionalCustomMetadata.Count);
            result.AddRange(_defaultPropertyNames);

            foreach (var kvp in _additionalCustomMetadata)
            {
                result.Add(kvp.Key);
            }

            return result;
        }
    }

    int ITaskItem.MetadataCount => _defaultPropertyNames.Length + (_additionalCustomMetadata?.Count ?? 0);

    string ITaskItem2.GetMetadataValueEscaped(string metadataName)
    {
        return metadataName switch
        {
            // These two are special and aren't "Real metadata"
            "FullPath" => Identity ?? "",
            nameof(Identity) => Identity ?? "",
            // These are common metadata
            nameof(SourceId) => SourceId ?? "",
            nameof(SourceType) => SourceType ?? "",
            nameof(ContentRoot) => ContentRoot ?? "",
            nameof(BasePath) => BasePath ?? "",
            nameof(RelativePath) => RelativePath ?? "",
            nameof(AssetKind) => AssetKind ?? "",
            nameof(AssetMode) => AssetMode ?? "",
            nameof(AssetRole) => AssetRole ?? "",
            nameof(AssetMergeBehavior) => AssetMergeBehavior ?? "",
            nameof(AssetMergeSource) => AssetMergeSource ?? "",
            nameof(RelatedAsset) => RelatedAsset ?? "",
            nameof(AssetTraitName) => AssetTraitName ?? "",
            nameof(AssetTraitValue) => AssetTraitValue ?? "",
            nameof(Fingerprint) => Fingerprint ?? "",
            nameof(Integrity) => Integrity ?? "",
            nameof(CopyToOutputDirectory) => CopyToOutputDirectory ?? "",
            nameof(CopyToPublishDirectory) => CopyToPublishDirectory ?? "",
            nameof(OriginalItemSpec) => OriginalItemSpec ?? "",
            nameof(FileLength) => GetFileLengthAsString() ?? "",
            nameof(LastWriteTime) => GetLastWriteTimeAsString() ?? "",
            _ => _additionalCustomMetadata?.TryGetValue(metadataName, out var value) == true ? (value ?? "") : "",
        };
    }

    private string GetFileLengthAsString() =>
        FileLength == -1 ? (FileLengthString ?? "") : FileLength.ToString(CultureInfo.InvariantCulture);

    private string GetLastWriteTimeAsString() =>
        LastWriteTime == DateTimeOffset.MinValue ? (LastWriteTimeString ?? "") : LastWriteTime.ToString(DateTimeAssetFormat, CultureInfo.InvariantCulture);

    void ITaskItem2.SetMetadataValueLiteral(string metadataName, string metadataValue)
    {
        metadataValue ??= "";
        switch (metadataName)
        {
            case nameof(SourceId):
                SourceId = metadataValue;
                break;
            case nameof(SourceType):
                SourceType = metadataValue;
                break;
            case nameof(ContentRoot):
                ContentRoot = metadataValue;
                break;
            case nameof(BasePath):
                BasePath = metadataValue;
                break;
            case nameof(RelativePath):
                RelativePath = metadataValue;
                break;
            case nameof(AssetKind):
                AssetKind = metadataValue;
                break;
            case nameof(AssetMode):
                AssetMode = metadataValue;
                break;
            case nameof(AssetRole):
                AssetRole = metadataValue;
                break;
            case nameof(AssetMergeBehavior):
                AssetMergeBehavior = metadataValue;
                break;
            case nameof(AssetMergeSource):
                AssetMergeSource = metadataValue;
                break;
            case nameof(RelatedAsset):
                RelatedAsset = metadataValue;
                break;
            case nameof(AssetTraitName):
                AssetTraitName = metadataValue;
                break;
            case nameof(AssetTraitValue):
                AssetTraitValue = metadataValue;
                break;
            case nameof(Fingerprint):
                Fingerprint = metadataValue;
                break;
            case nameof(Integrity):
                Integrity = metadataValue;
                break;
            case nameof(CopyToOutputDirectory):
                CopyToOutputDirectory = metadataValue;
                break;
            case nameof(CopyToPublishDirectory):
                CopyToPublishDirectory = metadataValue;
                break;
            case nameof(OriginalItemSpec):
                OriginalItemSpec = metadataValue;
                break;
            case nameof(FileLength):
                _fileLengthString = metadataValue;
                _fileLength = -1;
                break;
            case nameof(LastWriteTime):
                _lastWriteTimeString = metadataValue;
                _lastWriteTime = DateTimeOffset.MinValue;
                break;
            default:
                _additionalCustomMetadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _additionalCustomMetadata[metadataName] = metadataValue;
                _modified = true;
                break;
        }
    }

    IDictionary ITaskItem2.CloneCustomMetadataEscaped()
    {
        var result = new Dictionary<string, string>(((ITaskItem)this).MetadataCount)
        {
            { nameof(SourceId), SourceId ?? "" },
            { nameof(SourceType), SourceType  ?? "" },
            { nameof(ContentRoot), ContentRoot  ?? "" },
            { nameof(BasePath), BasePath  ?? "" },
            { nameof(RelativePath), RelativePath  ?? "" },
            { nameof(AssetKind), AssetKind  ?? "" },
            { nameof(AssetMode), AssetMode  ?? "" },
            { nameof(AssetRole), AssetRole  ?? "" },
            { nameof(AssetMergeBehavior), AssetMergeBehavior  ?? "" },
            { nameof(AssetMergeSource), AssetMergeSource  ?? "" },
            { nameof(RelatedAsset), RelatedAsset  ?? "" },
            { nameof(AssetTraitName), AssetTraitName  ?? "" },
            { nameof(AssetTraitValue), AssetTraitValue  ?? "" },
            { nameof(Fingerprint), Fingerprint  ?? "" },
            { nameof(Integrity), Integrity  ?? "" },
            { nameof(CopyToOutputDirectory), CopyToOutputDirectory  ?? "" },
            { nameof(CopyToPublishDirectory), CopyToPublishDirectory  ?? "" },
            { nameof(OriginalItemSpec), OriginalItemSpec  ?? "" },
            { nameof(FileLength), GetFileLengthAsString() ?? "" },
            { nameof(LastWriteTime), GetLastWriteTimeAsString() ?? "" }
        };
        if (_additionalCustomMetadata != null)
        {
            foreach (var kvp in _additionalCustomMetadata)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    string ITaskItem.GetMetadata(string metadataName) => ((ITaskItem2)this).GetMetadataValueEscaped(metadataName);
    void ITaskItem.SetMetadata(string metadataName, string metadataValue) => ((ITaskItem2)this).SetMetadataValueLiteral(metadataName, metadataValue);

    void ITaskItem.RemoveMetadata(string metadataName) => _additionalCustomMetadata?.Remove(metadataName);

    void ITaskItem.CopyMetadataTo(ITaskItem destinationItem)
    {
        destinationItem.SetMetadata(nameof(SourceId), SourceId ?? "");
        destinationItem.SetMetadata(nameof(SourceType), SourceType ?? "");
        destinationItem.SetMetadata(nameof(ContentRoot), ContentRoot ?? "");
        destinationItem.SetMetadata(nameof(BasePath), BasePath ?? "");
        destinationItem.SetMetadata(nameof(RelativePath), RelativePath ?? "");
        destinationItem.SetMetadata(nameof(AssetKind), AssetKind ?? "");
        destinationItem.SetMetadata(nameof(AssetMode), AssetMode ?? "");
        destinationItem.SetMetadata(nameof(AssetRole), AssetRole ?? "");
        destinationItem.SetMetadata(nameof(AssetMergeBehavior), AssetMergeBehavior ?? "");
        destinationItem.SetMetadata(nameof(AssetMergeSource), AssetMergeSource ?? "");
        destinationItem.SetMetadata(nameof(RelatedAsset), RelatedAsset ?? "");
        destinationItem.SetMetadata(nameof(AssetTraitName), AssetTraitName ?? "");
        destinationItem.SetMetadata(nameof(AssetTraitValue), AssetTraitValue ?? "");
        destinationItem.SetMetadata(nameof(Fingerprint), Fingerprint ?? "");
        destinationItem.SetMetadata(nameof(Integrity), Integrity ?? "");
        destinationItem.SetMetadata(nameof(CopyToOutputDirectory), CopyToOutputDirectory ?? "");
        destinationItem.SetMetadata(nameof(CopyToPublishDirectory), CopyToPublishDirectory ?? "");
        destinationItem.SetMetadata(nameof(OriginalItemSpec), OriginalItemSpec ?? "");
        destinationItem.SetMetadata(nameof(FileLength), GetFileLengthAsString() ?? "");
        destinationItem.SetMetadata(nameof(LastWriteTime), GetLastWriteTimeAsString() ?? "");
        if (_additionalCustomMetadata != null)
        {
            foreach (var kvp in _additionalCustomMetadata)
            {
                destinationItem.SetMetadata(kvp.Key, kvp.Value ?? "");
            }
        }
    }

    IDictionary ITaskItem.CloneCustomMetadata() => ((ITaskItem2)this).CloneCustomMetadataEscaped();

    #endregion

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
