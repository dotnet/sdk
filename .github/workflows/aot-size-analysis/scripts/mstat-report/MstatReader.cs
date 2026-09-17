// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace MstatReport;

internal static class MstatReader
{
    private static readonly string[] s_requiredGlobals =
    [
        "Methods",
        "Types",
        "Blobs",
        "RvaFields",
        "FrozenObjects",
        "ManifestResources"
    ];

    public static MstatModel Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader peReader = new(stream, PEStreamOptions.PrefetchEntireImage);
        if (!peReader.HasMetadata)
        {
            throw new InvalidDataException("The input is not a managed MSTAT PE file.");
        }

        MetadataReader metadata = peReader.GetMetadataReader();
        AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
        if (assembly.Version.Major != 2)
        {
            throw new InvalidDataException(
                $"Unsupported MSTAT version {assembly.Version}; expected version 2.x.");
        }

        Dictionary<string, byte[]> globals = ReadGlobalMethods(peReader, metadata);
        foreach (string required in s_requiredGlobals)
        {
            if (!globals.ContainsKey(required))
            {
                throw new InvalidDataException($"Missing MSTAT global method '{required}'.");
            }
        }

        byte[] namesSection = ReadNamesSection(peReader);
        MetadataNameFormatter formatter = new(
            peReader.GetMetadata().GetContent(),
            namesSection);

        return new MstatModel
        {
            Version = assembly.Version,
            Names = formatter,
            Methods = ReadMethods(globals["Methods"]),
            Types = ReadTypes(globals["Types"]),
            Blobs = ReadBlobs(globals["Blobs"], metadata),
            RvaFields = ReadFields(globals["RvaFields"]),
            FrozenObjects = ReadFrozenObjects(globals["FrozenObjects"]),
            ManifestResources = ReadResources(globals["ManifestResources"], metadata),
            DeduplicatedMethods = globals.TryGetValue("DeduplicatedMethods", out byte[]? deduplicated)
                ? ReadDeduplicatedMethods(deduplicated)
                : []
        };
    }

    private static Dictionary<string, byte[]> ReadGlobalMethods(
        PEReader peReader,
        MetadataReader metadata)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        TypeDefinition module = metadata.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1));
        foreach (MethodDefinitionHandle handle in module.GetMethods())
        {
            MethodDefinition method = metadata.GetMethodDefinition(handle);
            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            string name = metadata.GetString(method.Name);
            byte[] il = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()
                ?? throw new InvalidDataException($"Global method '{name}' has no IL.");
            result[name] = il;
        }

        return result;
    }

    private static byte[] ReadNamesSection(PEReader peReader)
    {
        SectionHeader? section = peReader.PEHeaders.SectionHeaders
            .FirstOrDefault(static section => section.Name == ".names");
        if (section is null)
        {
            throw new InvalidDataException("The MSTAT PE file has no .names section.");
        }

        SectionHeader value = section.Value;
        return peReader.GetSectionData(value.VirtualAddress)
            .GetContent(0, value.VirtualSize)
            .ToArray();
    }

    private static List<MethodRecord> ReadMethods(byte[] bytes)
    {
        var reader = new MstatIlReader(bytes, "Methods");
        var result = new List<MethodRecord>();
        while (!reader.End)
        {
            result.Add(new MethodRecord(
                reader.ReadToken(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()));
        }

        return result;
    }

    private static List<TypeRecord> ReadTypes(byte[] bytes)
    {
        var reader = new MstatIlReader(bytes, "Types");
        var result = new List<TypeRecord>();
        while (!reader.End)
        {
            result.Add(new TypeRecord(reader.ReadToken(), reader.ReadInt32(), reader.ReadInt32()));
        }

        return result;
    }

    private static List<BlobRecord> ReadBlobs(byte[] bytes, MetadataReader metadata)
    {
        var reader = new MstatIlReader(bytes, "Blobs");
        var result = new List<BlobRecord>();
        while (!reader.End)
        {
            result.Add(new BlobRecord(
                metadata.GetUserString(MetadataTokens.UserStringHandle(reader.ReadUserStringToken())),
                reader.ReadInt32()));
        }

        return result;
    }

    private static List<FieldRecord> ReadFields(byte[] bytes)
    {
        var reader = new MstatIlReader(bytes, "RvaFields");
        var result = new List<FieldRecord>();
        while (!reader.End)
        {
            result.Add(new FieldRecord(reader.ReadToken(), reader.ReadInt32(), reader.ReadInt32()));
        }

        return result;
    }

    private static List<FrozenObjectRecord> ReadFrozenObjects(byte[] bytes)
    {
        var reader = new MstatIlReader(bytes, "FrozenObjects");
        var result = new List<FrozenObjectRecord>();
        while (!reader.End)
        {
            int objectType = reader.ReadToken();
            int size = reader.ReadInt32();
            int nameOffset = reader.ReadInt32();
            int? owner = reader.NextIsToken ? reader.ReadToken() : ReadZeroOwner(reader);
            result.Add(new FrozenObjectRecord(objectType, size, nameOffset, owner));
        }

        return result;

        static int? ReadZeroOwner(MstatIlReader reader)
        {
            if (reader.ReadInt32() != 0)
            {
                throw new InvalidDataException("FrozenObjects owner sentinel must be zero.");
            }

            return null;
        }
    }

    private static List<ResourceRecord> ReadResources(byte[] bytes, MetadataReader metadata)
    {
        var reader = new MstatIlReader(bytes, "ManifestResources");
        var result = new List<ResourceRecord>();
        while (!reader.End)
        {
            int assemblyToken = reader.ReadInt32();
            string name = metadata.GetUserString(
                MetadataTokens.UserStringHandle(reader.ReadUserStringToken()));
            result.Add(new ResourceRecord(assemblyToken, name, reader.ReadInt32()));
        }

        return result;
    }

    private static List<DeduplicatedMethodRecord> ReadDeduplicatedMethods(byte[] bytes)
    {
        var reader = new MstatIlReader(bytes, "DeduplicatedMethods");
        var result = new List<DeduplicatedMethodRecord>();
        while (!reader.End)
        {
            int method = reader.ReadToken();
            int count = reader.ReadInt32();
            if (count < 0)
            {
                throw new InvalidDataException("A deduplicated method target count is negative.");
            }

            var targets = new List<DeduplicatedMethodTarget>(count);
            for (int i = 0; i < count; i++)
            {
                targets.Add(new DeduplicatedMethodTarget(reader.ReadToken(), reader.ReadInt32()));
            }

            result.Add(new DeduplicatedMethodRecord(method, targets));
        }

        return result;
    }
}

internal sealed class MstatIlReader
{
    private readonly byte[] _bytes;
    private readonly string _section;
    private int _offset;

    public MstatIlReader(byte[] bytes, string section)
    {
        _bytes = bytes;
        _section = section;
    }

    public bool End => _offset == _bytes.Length;

    public bool NextIsToken => !End && _bytes[_offset] == 0xd0;

    public int ReadToken()
    {
        RequireByte(0xd0, "ldtoken");
        return ReadRawInt32();
    }

    public int ReadUserStringToken()
    {
        RequireByte(0x72, "ldstr");
        int token = ReadRawInt32();
        if ((token & unchecked((int)0xff000000)) != 0x70000000)
        {
            throw Error($"0x{token:x8} is not a user-string token.");
        }

        return token;
    }

    public int ReadInt32()
    {
        byte opcode = ReadByte();
        return opcode switch
        {
            0x15 => -1,
            >= 0x16 and <= 0x1e => opcode - 0x16,
            0x1f => unchecked((sbyte)ReadByte()),
            0x20 => ReadRawInt32(),
            _ => throw Error($"Expected an ldc.i4 instruction, found opcode 0x{opcode:x2}.")
        };
    }

    private void RequireByte(byte expected, string instruction)
    {
        byte actual = ReadByte();
        if (actual != expected)
        {
            throw Error($"Expected {instruction} (0x{expected:x2}), found 0x{actual:x2}.");
        }
    }

    private byte ReadByte()
    {
        if (_offset >= _bytes.Length)
        {
            throw Error("Unexpected end of IL.");
        }

        return _bytes[_offset++];
    }

    private int ReadRawInt32()
    {
        if (_offset > _bytes.Length - sizeof(int))
        {
            throw Error("Unexpected end of IL.");
        }

        int value = BitConverter.ToInt32(_bytes, _offset);
        _offset += sizeof(int);
        return value;
    }

    private InvalidDataException Error(string message) =>
        new($"Invalid {_section} IL at offset 0x{_offset:x}: {message}");
}
