// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MstatReport;

internal static class ReportBuilder
{
    public static SizeReport Build(MstatModel mstat, string source)
    {
        var tree = new MstatTreeBuilder(mstat.Names);

        foreach (TypeRecord record in mstat.Types)
        {
            TypeDisplay type = mstat.Names.GetType(record.Token);
            MutableSizeNode node = tree.GetType(type);
            node.AddBytes(record.Size, mstat.Names.GetSerializedName(record.NameOffset));
            tree.Remember(record.Token, node);
        }

        foreach (MethodRecord record in mstat.Methods)
        {
            MemberDisplay member = mstat.Names.GetMember(record.Token);
            MutableSizeNode sizedNode = GetMethodNode(tree, member);
            sizedNode.AddBytes(
                record.PhysicalSize,
                mstat.Names.GetSerializedName(record.NameOffset));
            tree.Remember(record.Token, sizedNode);
        }

        foreach (FieldRecord record in mstat.RvaFields)
        {
            MemberDisplay member = mstat.Names.GetMember(record.Token);
            MutableSizeNode field = tree.GetType(member.Owner).GetOrAdd(
                $"field:{member.Text}",
                member.Text,
                "RVA field");
            field.AddBytes(record.Size, mstat.Names.GetSerializedName(record.NameOffset));
            tree.Remember(record.Token, field);
        }

        foreach (FrozenObjectRecord record in mstat.FrozenObjects)
        {
            TypeDisplay owner = mstat.Names.GetType(record.OwningTypeToken ?? record.ObjectTypeToken);
            string objectType = mstat.Names.GetType(record.ObjectTypeToken).Text;
            string label = mstat.Names.GetSerializedName(record.NameOffset);
            MutableSizeNode frozen = tree.GetType(owner).GetOrAdd(
                $"frozen:{record.NameOffset}",
                $"Frozen {objectType}",
                "frozen object");
            frozen.AddBytes(record.Size, label);
        }

        foreach (ResourceRecord record in mstat.ManifestResources)
        {
            MutableSizeNode assembly = tree.GetAssembly(mstat.Names.GetAssemblyName(record.AssemblyToken));
            MutableSizeNode resources = assembly.GetOrAdd(
                "namespace:<manifest resources>",
                "<manifest resources>",
                "namespace");
            resources.GetOrAdd(
                $"resource:{record.Name}",
                record.Name,
                "manifest resource").AddBytes(record.Size, null);
        }

        List<BlobRecord> uniqueBlobs = mstat.Blobs
            .Where(blob => !IsCompatibilityDuplicate(blob.Name, mstat.Version))
            .ToList();
        if (uniqueBlobs.Count > 0)
        {
            MutableSizeNode blobs = tree.GetAssembly("<compiler>").GetOrAdd(
                "namespace:<compiler blobs>",
                "<compiler blobs>",
                "namespace");
            foreach (BlobRecord record in uniqueBlobs)
            {
                blobs.GetOrAdd($"blob:{record.Name}", record.Name, "compiler blob")
                    .AddBytes(record.Size, null);
            }
        }

        foreach (DeduplicatedMethodRecord record in mstat.DeduplicatedMethods)
        {
            if (!tree.TryGetRemembered(record.MethodToken, out MutableSizeNode? method))
            {
                MemberDisplay display = mstat.Names.GetMember(record.MethodToken);
                method = GetMethodNode(tree, display);
                tree.Remember(record.MethodToken, method);
            }

            foreach (DeduplicatedMethodTarget target in record.Targets)
            {
                string targetName = mstat.Names.GetSerializedName(target.NameOffset);
                method!.AddDeduplicatedTarget(targetName);
            }
        }

        SizeNode root = tree.Freeze(Path.GetFileNameWithoutExtension(source));
        return new SizeReport
        {
            Name = Path.GetFileNameWithoutExtension(source),
            Source = source,
            MstatVersion = mstat.Version,
            AttributedSize = root.Size,
            Root = root,
            DeduplicatedMethodCount = mstat.DeduplicatedMethods.Count
        };
    }

    private static bool IsCompatibilityDuplicate(string name, Version version) =>
        version >= new Version(2, 1) &&
        name is "FieldRvaData" or "ArrayOfFrozenObjects" or "ResourceData";

    private static MutableSizeNode GetMethodNode(
        MstatTreeBuilder tree,
        MemberDisplay member)
    {
        MutableSizeNode definition = tree.GetType(member.Owner).GetOrAdd(
            $"member:{member.IsField}:{member.DefinitionText ?? member.Text}",
            member.DefinitionText ?? member.Text,
            member.IsField ? "field" : "method");
        return member.IsMethodInstantiation
            ? definition.GetOrAdd(
                $"method-instantiation:{member.Text}",
                member.Text,
                "method instantiation")
            : definition;
    }
}

internal sealed class MstatTreeBuilder
{
    private readonly MetadataNameFormatter _names;
    private readonly MutableSizeNode _root = new("root", "NativeAOT image", "image");
    private readonly Dictionary<string, MutableSizeNode> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<int, MutableSizeNode> _tokenNodes = [];

    public MstatTreeBuilder(MetadataNameFormatter names)
    {
        _names = names;
    }

    public MutableSizeNode GetAssembly(string name) =>
        _root.GetOrAdd($"assembly:{name}", name, "assembly");

    public MutableSizeNode GetType(TypeDisplay type)
    {
        string key = type.DefinitionIdentity;
        if (!_types.TryGetValue(key, out MutableSizeNode? definition))
        {
            MutableSizeNode assembly = GetAssembly(type.Assembly);
            string namespaceName = string.IsNullOrEmpty(type.Namespace)
                ? "<global namespace>"
                : type.Namespace;
            MutableSizeNode ns = assembly.GetOrAdd(
                $"namespace:{namespaceName}",
                namespaceName,
                "namespace");
            definition = ns.GetOrAdd(
                $"type:{type.DefinitionName}",
                type.DefinitionName,
                "type");
            _types.Add(key, definition);
        }

        if (!type.IsInstantiation)
        {
            return definition;
        }

        return definition.GetOrAdd(
            $"type-instantiation:{type.Text}",
            type.Text,
            "type instantiation");
    }

    public void Remember(int token, MutableSizeNode node)
    {
        _tokenNodes[token] = node;
    }

    public bool TryGetRemembered(int token, out MutableSizeNode? node) =>
        _tokenNodes.TryGetValue(token, out node);

    public SizeNode Freeze(string rootName)
    {
        _root.Name = rootName;
        return _root.Freeze();
    }
}

internal sealed class MutableSizeNode
{
    private readonly Dictionary<string, MutableSizeNode> _children = new(StringComparer.Ordinal);
    private readonly List<string> _childOrder = [];
    private List<string>? _deduplicatedWith;
    private string? _mstatLabel;
    private bool _hasSplitPhysicalRecords;

    public MutableSizeNode(string key, string name, string kind)
    {
        Key = key;
        Name = name;
        Kind = kind;
    }

    public string Key { get; }
    public string Name { get; set; }
    public string Kind { get; }
    public long ExclusiveSize { get; private set; }

    public MutableSizeNode GetOrAdd(string key, string name, string kind)
    {
        if (!_children.TryGetValue(key, out MutableSizeNode? node))
        {
            node = new MutableSizeNode(key, name, kind);
            _children.Add(key, node);
            _childOrder.Add(key);
        }

        return node;
    }

    public void AddBytes(int size, string? mstatLabel)
    {
        if (size < 0)
        {
            throw new InvalidDataException($"MSTAT size for '{Name}' is negative.");
        }

        if (_hasSplitPhysicalRecords && mstatLabel is not null)
        {
            AddPhysicalRecord(size, mstatLabel);
            return;
        }

        if (_mstatLabel is not null &&
            mstatLabel is not null &&
            _mstatLabel != mstatLabel)
        {
            string previousLabel = _mstatLabel;
            long previousSize = ExclusiveSize;
            ExclusiveSize = 0;
            _mstatLabel = null;
            _hasSplitPhysicalRecords = true;
            AddPhysicalRecord(previousSize, previousLabel);
            AddPhysicalRecord(size, mstatLabel);
            return;
        }

        ExclusiveSize = checked(ExclusiveSize + size);
        if (mstatLabel is not null)
        {
            _mstatLabel = mstatLabel;
        }
    }

    public void AddDeduplicatedTarget(string name)
    {
        _deduplicatedWith ??= [];
        _deduplicatedWith.Add(name);
    }

    private void AddPhysicalRecord(long size, string mstatLabel)
    {
        MutableSizeNode physicalNode = GetOrAdd(
            $"physical:{mstatLabel}",
            Name,
            $"{Kind} physical node");
        physicalNode.ExclusiveSize = checked(physicalNode.ExclusiveSize + size);
        physicalNode._mstatLabel = mstatLabel;
    }

    public SizeNode Freeze()
    {
        List<SizeNode> children = _childOrder
            .Select(key => _children[key].Freeze())
            .Where(static child => child.Size > 0 || child.DeduplicatedWith is not null)
            .OrderByDescending(static child => child.Size)
            .ThenBy(static child => child.Name, StringComparer.Ordinal)
            .ToList();
        long size = checked(ExclusiveSize + children.Sum(static child => child.Size));
        return new SizeNode
        {
            Name = Name,
            Kind = Kind,
            Size = size,
            ExclusiveSize = ExclusiveSize,
            Children = children.Count == 0 ? null : children,
            DeduplicatedWith = _deduplicatedWith,
            MstatLabel = _mstatLabel
        };
    }
}
