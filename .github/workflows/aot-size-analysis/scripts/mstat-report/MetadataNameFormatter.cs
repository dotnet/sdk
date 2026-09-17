// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace MstatReport;

internal sealed class MetadataNameFormatter
{
    private readonly MetadataReaderProvider _metadataProvider;
    private readonly MetadataReader _metadata;
    private readonly byte[] _serializedNames;
    private readonly DisplayTypeProvider _provider;
    private readonly Dictionary<int, TypeDisplay> _types = [];
    private readonly Dictionary<int, MemberDisplay> _members = [];

    public MetadataNameFormatter(
        ImmutableArray<byte> metadataImage,
        byte[] serializedNames)
    {
        _metadataProvider = MetadataReaderProvider.FromMetadataImage(metadataImage);
        _metadata = _metadataProvider.GetMetadataReader();
        _serializedNames = serializedNames;
        _provider = new DisplayTypeProvider(_metadata);
    }

    public string GetSerializedName(int offset)
    {
        if ((uint)offset >= (uint)_serializedNames.Length)
        {
            throw new InvalidDataException(
                $".names offset {offset} is outside the {_serializedNames.Length}-byte section.");
        }

        unsafe
        {
            fixed (byte* bytes = _serializedNames)
            {
                var reader = new BlobReader(bytes, _serializedNames.Length)
                {
                    Offset = offset
                };
                return reader.ReadSerializedString()
                    ?? throw new InvalidDataException($".names offset {offset} contains null.");
            }
        }
    }

    public TypeDisplay GetType(int token)
    {
        if (_types.TryGetValue(token, out TypeDisplay? cached))
        {
            return cached;
        }

        EntityHandle handle = GetHandle(token);
        TypeDisplay result = handle.Kind switch
        {
            HandleKind.TypeReference => _provider.GetTypeFromReference(
                _metadata,
                (TypeReferenceHandle)handle,
                0),
            HandleKind.TypeSpecification => _provider.GetTypeFromSpecification(
                _metadata,
                null,
                (TypeSpecificationHandle)handle,
                0),
            HandleKind.TypeDefinition => _provider.GetTypeFromDefinition(
                _metadata,
                (TypeDefinitionHandle)handle,
                0),
            _ => throw new InvalidDataException(
                $"Metadata token 0x{token:x8} is not a type token.")
        };

        _types.Add(token, result);
        return result;
    }

    public MemberDisplay GetMember(int token)
    {
        if (_members.TryGetValue(token, out MemberDisplay? cached))
        {
            return cached;
        }

        EntityHandle handle = GetHandle(token);
        MemberDisplay result = handle.Kind switch
        {
            HandleKind.MemberReference => ReadMemberReference((MemberReferenceHandle)handle),
            HandleKind.MethodSpecification => ReadMethodSpecification(
                (MethodSpecificationHandle)handle),
            _ => throw new InvalidDataException(
                $"Metadata token 0x{token:x8} is not a member or method specification.")
        };
        _members.Add(token, result);
        return result;
    }

    public string GetAssemblyName(int token)
    {
        EntityHandle handle = GetHandle(token);
        if (handle.Kind != HandleKind.AssemblyReference)
        {
            throw new InvalidDataException(
                $"Metadata token 0x{token:x8} is not an assembly reference.");
        }

        AssemblyReference reference = _metadata.GetAssemblyReference((AssemblyReferenceHandle)handle);
        return _metadata.GetString(reference.Name);
    }

    private MemberDisplay ReadMemberReference(MemberReferenceHandle handle)
    {
        MemberReference reference = _metadata.GetMemberReference(handle);
        TypeDisplay owner = GetParentType(reference.Parent);
        string name = _metadata.GetString(reference.Name);
        BlobReader signatureReader = _metadata.GetBlobReader(reference.Signature);
        SignatureHeader header = signatureReader.ReadSignatureHeader();
        if (header.Kind == SignatureKind.Field)
        {
            TypeDisplay fieldType = reference.DecodeFieldSignature(_provider, null);
            return new MemberDisplay(
                owner,
                name,
                $"{name}: {fieldType.Text}",
                true,
                false,
                null);
        }

        MethodSignature<TypeDisplay> signature = reference.DecodeMethodSignature(_provider, null);
        string genericSuffix = signature.GenericParameterCount > 0
            ? $"<{string.Join(", ", Enumerable.Range(0, signature.GenericParameterCount).Select(
                static index => $"!!{index}"))}>"
            : "";
        string parameters = string.Join(", ", signature.ParameterTypes.Select(static type => type.Text));
        string text = $"{name}{genericSuffix}({parameters}): {signature.ReturnType.Text}";
        return new MemberDisplay(owner, name, text, false, false, signature);
    }

    private MemberDisplay ReadMethodSpecification(MethodSpecificationHandle handle)
    {
        MethodSpecification specification = _metadata.GetMethodSpecification(handle);
        if (specification.Method.Kind != HandleKind.MemberReference)
        {
            throw new InvalidDataException(
                $"MSTAT method specifications must refer to member references, not " +
                $"{specification.Method.Kind} " +
                $"(0x{MetadataTokens.GetToken(specification.Method):x8}).");
        }

        MemberDisplay definition = ReadMemberReference(
            (MemberReferenceHandle)specification.Method);
        ImmutableArray<TypeDisplay> arguments = specification.DecodeSignature(_provider, null);
        string suffix = $"<{string.Join(", ", arguments.Select(static argument => argument.Text))}>";
        string parameters = definition.MethodSignature is MethodSignature<TypeDisplay> signature
            ? string.Join(", ", signature.ParameterTypes.Select(
                parameter => SubstituteMethodParameters(parameter.Text, arguments)))
            : "";
        string returnType = definition.MethodSignature is MethodSignature<TypeDisplay> methodSignature
            ? SubstituteMethodParameters(methodSignature.ReturnType.Text, arguments)
            : "void";
        return new MemberDisplay(
            definition.Owner,
            definition.Name,
            $"{definition.Name}{suffix}({parameters}): {returnType}",
            false,
            true,
            definition.MethodSignature,
            definition.Text);
    }

    private TypeDisplay GetParentType(EntityHandle parent) => parent.Kind switch
    {
        HandleKind.TypeReference => _provider.GetTypeFromReference(
            _metadata,
            (TypeReferenceHandle)parent,
            0),
        HandleKind.TypeSpecification => _provider.GetTypeFromSpecification(
            _metadata,
            null,
            (TypeSpecificationHandle)parent,
            0),
        HandleKind.TypeDefinition => _provider.GetTypeFromDefinition(
            _metadata,
            (TypeDefinitionHandle)parent,
            0),
        _ => throw new InvalidDataException(
            $"Unsupported MSTAT member parent kind '{parent.Kind}'.")
    };

    private static EntityHandle GetHandle(int token)
    {
        try
        {
            return MetadataTokens.EntityHandle(token);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Invalid metadata token 0x{token:x8}.", exception);
        }
    }

    private static string SubstituteMethodParameters(
        string text,
        ImmutableArray<TypeDisplay> arguments)
    {
        for (int i = arguments.Length - 1; i >= 0; i--)
        {
            text = text.Replace($"!!{i}", arguments[i].Text, StringComparison.Ordinal);
        }

        return text;
    }
}

internal sealed record MemberDisplay(
    TypeDisplay Owner,
    string Name,
    string Text,
    bool IsField,
    bool IsMethodInstantiation,
    MethodSignature<TypeDisplay>? MethodSignature,
    string? DefinitionText = null);

internal sealed record TypeDisplay(
    string Text,
    string Assembly,
    string Namespace,
    string DefinitionName,
    bool IsInstantiation = false)
{
    public string DefinitionIdentity => $"{Assembly}\0{Namespace}\0{DefinitionName}";
}

internal sealed class DisplayTypeProvider : ISignatureTypeProvider<TypeDisplay, object?>
{
    private readonly MetadataReader _metadata;
    private readonly Dictionary<TypeReferenceHandle, TypeDisplay> _references = [];

    public DisplayTypeProvider(MetadataReader metadata)
    {
        _metadata = metadata;
    }

    public TypeDisplay GetArrayType(TypeDisplay elementType, ArrayShape shape)
    {
        string commas = shape.Rank > 0 ? new string(',', shape.Rank - 1) : "";
        return Derived(elementType, $"{elementType.Text}[{commas}]");
    }

    public TypeDisplay GetByReferenceType(TypeDisplay elementType) =>
        Derived(elementType, $"{elementType.Text}&");

    public TypeDisplay GetFunctionPointerType(MethodSignature<TypeDisplay> signature)
    {
        string parameters = string.Join(", ", signature.ParameterTypes.Select(
            static parameter => parameter.Text));
        return new TypeDisplay(
            $"delegate*<{parameters}{(parameters.Length > 0 ? ", " : "")}{signature.ReturnType.Text}>",
            "<runtime>",
            "",
            "function pointer");
    }

    public TypeDisplay GetGenericInstantiation(
        TypeDisplay genericType,
        ImmutableArray<TypeDisplay> typeArguments)
    {
        string name = RemoveGenericArity(genericType.Text);
        return genericType with
        {
            Text = $"{name}<{string.Join(", ", typeArguments.Select(static type => type.Text))}>",
            IsInstantiation = true
        };
    }

    public TypeDisplay GetGenericMethodParameter(object? genericContext, int index) =>
        Placeholder($"!!{index}");

    public TypeDisplay GetGenericTypeParameter(object? genericContext, int index) =>
        Placeholder($"!{index}");

    public TypeDisplay GetModifiedType(
        TypeDisplay modifier,
        TypeDisplay unmodifiedType,
        bool isRequired) =>
        Derived(
            unmodifiedType,
            $"{unmodifiedType.Text} mod{(isRequired ? "req" : "opt")}({modifier.Text})");

    public TypeDisplay GetPinnedType(TypeDisplay elementType) =>
        Derived(elementType, $"{elementType.Text} pinned");

    public TypeDisplay GetPointerType(TypeDisplay elementType) =>
        Derived(elementType, $"{elementType.Text}*");

    public TypeDisplay GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        string name = typeCode switch
        {
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Byte => "byte",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.Double => "double",
            PrimitiveTypeCode.Int16 => "short",
            PrimitiveTypeCode.Int32 => "int",
            PrimitiveTypeCode.Int64 => "long",
            PrimitiveTypeCode.IntPtr => "nint",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.SByte => "sbyte",
            PrimitiveTypeCode.Single => "float",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.TypedReference => "TypedReference",
            PrimitiveTypeCode.UInt16 => "ushort",
            PrimitiveTypeCode.UInt32 => "uint",
            PrimitiveTypeCode.UInt64 => "ulong",
            PrimitiveTypeCode.UIntPtr => "nuint",
            PrimitiveTypeCode.Void => "void",
            _ => typeCode.ToString()
        };
        return new TypeDisplay(name, "System.Private.CoreLib", "System", name);
    }

    public TypeDisplay GetSZArrayType(TypeDisplay elementType) =>
        Derived(elementType, $"{elementType.Text}[]");

    public TypeDisplay GetTypeFromDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        byte rawTypeKind)
    {
        TypeDefinition definition = reader.GetTypeDefinition(handle);
        string name = reader.GetString(definition.Name);
        string ns = reader.GetString(definition.Namespace);
        string assembly = reader.IsAssembly
            ? reader.GetString(reader.GetAssemblyDefinition().Name)
            : "<module>";
        return new TypeDisplay(name, assembly, ns, name);
    }

    public TypeDisplay GetTypeFromReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        byte rawTypeKind)
    {
        if (_references.TryGetValue(handle, out TypeDisplay? cached))
        {
            return cached;
        }

        TypeReference reference = reader.GetTypeReference(handle);
        string localName = reader.GetString(reference.Name);
        string ns = reader.GetString(reference.Namespace);
        TypeDisplay result;
        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            TypeDisplay outer = GetTypeFromReference(
                reader,
                (TypeReferenceHandle)reference.ResolutionScope,
                rawTypeKind);
            result = new TypeDisplay(
                $"{outer.Text}+{localName}",
                outer.Assembly,
                outer.Namespace,
                $"{outer.DefinitionName}+{localName}");
        }
        else
        {
            string assembly = reference.ResolutionScope.Kind == HandleKind.AssemblyReference
                ? reader.GetString(
                    reader.GetAssemblyReference(
                        (AssemblyReferenceHandle)reference.ResolutionScope).Name)
                : "<module>";
            result = new TypeDisplay(localName, assembly, ns, localName);
        }

        _references.Add(handle, result);
        return result;
    }

    public TypeDisplay GetTypeFromSpecification(
        MetadataReader reader,
        object? genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    private static TypeDisplay Derived(TypeDisplay elementType, string text) =>
        new(text, elementType.Assembly, elementType.Namespace, text);

    private static TypeDisplay Placeholder(string text) =>
        new(text, "<generic parameter>", "", text);

    private static string RemoveGenericArity(string name)
    {
        var result = new System.Text.StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            if (name[i] == '`')
            {
                i++;
                while (i < name.Length && char.IsAsciiDigit(name[i]))
                {
                    i++;
                }

                i--;
            }
            else
            {
                result.Append(name[i]);
            }
        }

        return result.ToString();
    }
}
