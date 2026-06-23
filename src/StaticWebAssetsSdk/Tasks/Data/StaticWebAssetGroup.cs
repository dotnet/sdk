// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public sealed class StaticWebAssetGroup : IEquatable<StaticWebAssetGroup>
{
    private ITaskItem _originalItem;
    private string _name;
    private string _value;
    private string _sourceId;
    private bool? _deferred;

    public string Name
    {
        get => _name ??= _originalItem?.ItemSpec;
        set => _name = value;
    }

    public string Value
    {
        get => _value ??= _originalItem?.GetMetadata(nameof(Value));
        set => _value = value;
    }

    public string SourceId
    {
        get => _sourceId ??= _originalItem?.GetMetadata(nameof(SourceId));
        set => _sourceId = value;
    }

    public bool Deferred
    {
        get
        {
            _deferred ??= string.Equals(_originalItem?.GetMetadata(nameof(Deferred)), "true", StringComparison.OrdinalIgnoreCase);
            return _deferred.Value;
        }
        set => _deferred = value;
    }

    public static StaticWebAssetGroup FromTaskItem(ITaskItem item)
    {
        var group = new StaticWebAssetGroup { _originalItem = item };

        if (string.IsNullOrEmpty(group.Name))
        {
            throw new InvalidOperationException("A StaticWebAssetGroup is missing a required Name.");
        }

        if (string.IsNullOrEmpty(group.SourceId))
        {
            throw new InvalidOperationException($"Group '{group.Name}' is missing a required SourceId.");
        }

        return group;
    }

    public static Dictionary<(string SourceId, string Name), StaticWebAssetGroup> FromItemGroup(ITaskItem[] items)
    {
        if (items == null || items.Length == 0)
        {
            return new();
        }

        var result = new Dictionary<(string SourceId, string Name), StaticWebAssetGroup>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var group = FromTaskItem(items[i]);
            result[(group.SourceId, group.Name)] = group;
        }

        return result;
    }

    // Materializes the groups into an array suitable for persisting in the manifest,
    // ensuring each member is fully realized (no lazy ITaskItem reference is retained).
    public static StaticWebAssetGroup[] FromItemGroupToArray(ITaskItem[] items)
    {
        if (items == null || items.Length == 0)
        {
            return [];
        }

        var groups = FromItemGroup(items);
        var result = new StaticWebAssetGroup[groups.Count];
        var index = 0;
        foreach (var group in groups.Values)
        {
            result[index++] = new StaticWebAssetGroup
            {
                Name = group.Name,
                Value = group.Value,
                SourceId = group.SourceId,
                Deferred = group.Deferred,
            };
        }

        Array.Sort(result, static (l, r) =>
        {
            var bySource = string.CompareOrdinal(l.SourceId, r.SourceId);
            return bySource != 0 ? bySource : string.CompareOrdinal(l.Name, r.Name);
        });

        return result;
    }

    public ITaskItem ToTaskItem()
    {
        var item = new TaskItem(Name);
        item.SetMetadata(nameof(Value), Value ?? "");
        item.SetMetadata(nameof(SourceId), SourceId ?? "");
        item.SetMetadata(nameof(Deferred), Deferred ? "true" : "false");
        return item;
    }

    public bool Equals(StaticWebAssetGroup other) =>
        other != null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Value, other.Value, StringComparison.Ordinal)
        && string.Equals(SourceId, other.SourceId, StringComparison.Ordinal)
        && Deferred == other.Deferred;

    public override bool Equals(object obj) => Equals(obj as StaticWebAssetGroup);

    public override int GetHashCode()
    {
#if NET6_0_OR_GREATER
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(Value);
        hash.Add(SourceId);
        hash.Add(Deferred);
        return hash.ToHashCode();
#else
        var hashCode = 1845001352;
        hashCode = hashCode * -1521134295 + (Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0);
        hashCode = hashCode * -1521134295 + (Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0);
        hashCode = hashCode * -1521134295 + (SourceId != null ? StringComparer.Ordinal.GetHashCode(SourceId) : 0);
        hashCode = hashCode * -1521134295 + Deferred.GetHashCode();
        return hashCode;
#endif
    }
}
