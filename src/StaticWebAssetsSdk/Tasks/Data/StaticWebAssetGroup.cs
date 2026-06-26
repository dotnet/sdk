// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

internal sealed class StaticWebAssetGroup
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
}
