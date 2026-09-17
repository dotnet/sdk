// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MstatReport;

internal sealed class MstatModel
{
    public required Version Version { get; init; }
    public required MetadataNameFormatter Names { get; init; }
    public required List<MethodRecord> Methods { get; init; }
    public required List<TypeRecord> Types { get; init; }
    public required List<BlobRecord> Blobs { get; init; }
    public required List<FieldRecord> RvaFields { get; init; }
    public required List<FrozenObjectRecord> FrozenObjects { get; init; }
    public required List<ResourceRecord> ManifestResources { get; init; }
    public required List<DeduplicatedMethodRecord> DeduplicatedMethods { get; init; }
}

internal sealed record MethodRecord(
    int Token,
    int CodeSize,
    int GcInfoSize,
    int EhInfoSize,
    int NameOffset)
{
    public int PhysicalSize => checked(CodeSize + GcInfoSize + EhInfoSize);
}

internal sealed record TypeRecord(int Token, int Size, int NameOffset);

internal sealed record BlobRecord(string Name, int Size);

internal sealed record FieldRecord(int Token, int Size, int NameOffset);

internal sealed record FrozenObjectRecord(
    int ObjectTypeToken,
    int Size,
    int NameOffset,
    int? OwningTypeToken);

internal sealed record ResourceRecord(int AssemblyToken, string Name, int Size);

internal sealed record DeduplicatedMethodRecord(
    int MethodToken,
    List<DeduplicatedMethodTarget> Targets);

internal sealed record DeduplicatedMethodTarget(int MethodToken, int NameOffset);
