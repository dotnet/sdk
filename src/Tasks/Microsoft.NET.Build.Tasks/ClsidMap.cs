// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.NET.Build.Tasks
{
    internal static class ClsidMap
    {
        private struct ClsidEntry
        {
            public string type;
            public string assembly;
        }

        public static void Create(MetadataReader metadataReader, string clsidMapPath)
        {
            Dictionary<string, ClsidEntry> clsidMap = new Dictionary<string, ClsidEntry>();

            string assemblyName = GetAssemblyName(metadataReader).FullName;

            bool isAssemblyComVisible = IsComVisible(metadataReader, metadataReader.GetAssemblyDefinition());

            foreach (TypeDefinitionHandle type in metadataReader.TypeDefinitions)
            {
                TypeDefinition definition = metadataReader.GetTypeDefinition(type);

                if (TypeIsPublic(metadataReader, definition) && TypeIsClass(metadataReader, definition) && IsComVisible(metadataReader, definition, isAssemblyComVisible))
                {
                    string guid = GetTypeGuid(metadataReader, definition);

                    if (clsidMap.ContainsKey(guid))
                    {
                        throw new BuildErrorException(Strings.ClsidMapConflictingGuids, clsidMap[guid].type, GetTypeName(metadataReader, definition), guid);
                    }

                    clsidMap.Add(guid,
                        new ClsidEntry
                        {
                            type = GetTypeName(metadataReader, definition),
                            assembly = assemblyName
                        });
                }
            }

            using (StreamWriter writer = File.CreateText(clsidMapPath))
            {
                writer.Write(JsonConvert.SerializeObject(clsidMap));
            }
        }

        private static bool TypeIsClass(MetadataReader metadataReader, TypeDefinition definition)
        {
            if ((definition.Attributes & TypeAttributes.Interface) != 0)
            {
                return false;
            }

            EntityHandle baseTypeEntity = definition.BaseType;
            if (baseTypeEntity.Kind == HandleKind.TypeReference)
            {
                TypeReference baseClass = metadataReader.GetTypeReference((TypeReferenceHandle)baseTypeEntity);
                if (baseClass.ResolutionScope.Kind == HandleKind.AssemblyReference && ReferenceToCoreLibrary(metadataReader, (AssemblyReferenceHandle)baseClass.ResolutionScope))
                {
                    if (HasTypeName(metadataReader, baseClass, "System", "ValueType") || HasTypeName(metadataReader, baseClass, "System", "Enum"))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static readonly string[] s_CoreLibraryNames = { "mscorlib", "System.Runtime", "netstandard" };

        private static bool ReferenceToCoreLibrary(MetadataReader reader, AssemblyReferenceHandle assembly)
        {
            return s_CoreLibraryNames.Contains(reader.GetString(reader.GetAssemblyReference(assembly).Name));
        }

        private static bool TypeIsPublic(MetadataReader reader, TypeDefinition type)
        {
            if ((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public)
            {
                return true;
            }
            else if ((type.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic)
            {
                return TypeIsPublic(reader, reader.GetTypeDefinition(type.GetDeclaringType()));
            }
            return false;
        }

        private static string GetTypeName(MetadataReader metadataReader, TypeDefinition type)
        {
            if (!type.GetDeclaringType().IsNil)
            {
                return $"{GetTypeName(metadataReader, metadataReader.GetTypeDefinition(type.GetDeclaringType()))}.{metadataReader.GetString(type.Name)}";
            }
            return $"{metadataReader.GetString(type.Namespace)}.{metadataReader.GetString(type.Name)}";
        }

        private static bool HasTypeName(MetadataReader metadataReader, TypeReference type, string ns, string name)
        {
            return metadataReader.StringComparer.Equals(type.Namespace, ns) && metadataReader.StringComparer.Equals(type.Name, name);
        }

        private static string GetTypeName(MetadataReader metadataReader, TypeReference type)
        {
            return $"{metadataReader.GetString(type.Namespace)}.{metadataReader.GetString(type.Name)}";
        }

        private static AssemblyName GetAssemblyName(MetadataReader metadataReader)
        {
            AssemblyName name = new AssemblyName();

            AssemblyDefinition definition = metadataReader.GetAssemblyDefinition();
            name.Name = metadataReader.GetString(definition.Name);
            name.Version = definition.Version;
            name.CultureInfo = CultureInfo.GetCultureInfo(metadataReader.GetString(definition.Culture));
            name.SetPublicKey(metadataReader.GetBlobBytes(definition.PublicKey));

            return name;
        }

        private static bool IsComVisible(MetadataReader reader, AssemblyDefinition assembly)
        {
            CustomAttributeHandle handle = GetComVisibleAttribute(reader, assembly.GetCustomAttributes());

            if (handle.IsNil)
            {
                return false;
            }

            CustomAttribute comVisibleAttribute = reader.GetCustomAttribute(handle);
            CustomAttributeValue<KnownType> data = comVisibleAttribute.DecodeValue(new TypeResolver());
            return (bool)data.FixedArguments[0].Value;
        }

        private static bool IsComVisible(MetadataReader metadataReader, TypeDefinition definition, bool assemblyComVisible)
        {
            bool? IsComVisibleCore(TypeDefinition typeDefinition)
            {
                CustomAttributeHandle handle = GetComVisibleAttribute(metadataReader, typeDefinition.GetCustomAttributes());
                if (handle.IsNil)
                {
                    return null;
                }

                CustomAttribute comVisibleAttribute = metadataReader.GetCustomAttribute(handle);
                CustomAttributeValue<KnownType> data = comVisibleAttribute.DecodeValue(new TypeResolver());
                return (bool)data.FixedArguments[0].Value;
            }

            if (!definition.GetDeclaringType().IsNil)
            {
                return IsComVisible(metadataReader, metadataReader.GetTypeDefinition(definition.GetDeclaringType()), assemblyComVisible) && (IsComVisibleCore(definition) ?? assemblyComVisible);
            }

            return IsComVisibleCore(definition) ?? assemblyComVisible;
        }

        private static CustomAttributeHandle GetComVisibleAttribute(MetadataReader reader, CustomAttributeHandleCollection customAttributes)
        {
            foreach (CustomAttributeHandle attr in customAttributes)
            {
                CustomAttribute attribute = reader.GetCustomAttribute(attr);
                MemberReference attributeConstructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                TypeReference attributeType = reader.GetTypeReference((TypeReferenceHandle)attributeConstructor.Parent);
                if (reader.GetString(attributeType.Namespace) == "System.Runtime.InteropServices" && reader.GetString(attributeType.Name) == "ComVisibleAttribute")
                {
                    return attr;
                }
            }
            return new CustomAttributeHandle();
        }

        private static string GetTypeGuid(MetadataReader reader, TypeDefinition type)
        {
            foreach (CustomAttributeHandle attr in type.GetCustomAttributes())
            {
                CustomAttribute attribute = reader.GetCustomAttribute(attr);
                MemberReference attributeConstructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                TypeReference attributeType = reader.GetTypeReference((TypeReferenceHandle)attributeConstructor.Parent);
                if (reader.GetString(attributeType.Namespace) == "System.Runtime.InteropServices" && reader.GetString(attributeType.Name) == "GuidAttribute")
                {
                    CustomAttributeValue<KnownType> data = attribute.DecodeValue(new TypeResolver());
                    return (string)data.FixedArguments[0].Value;
                }
            }
            throw new BuildErrorException(Strings.ClsidMapExportedTypesRequireExplicitGuid, GetTypeName(reader, type));
        }

        private enum KnownType
        {
            Bool,
            String,
            SystemType,
            Unknown
        }

        private class TypeResolver : ICustomAttributeTypeProvider<KnownType>
        {
            public KnownType GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                switch (typeCode)
                {
                    case PrimitiveTypeCode.Boolean:
                        return KnownType.Bool;
                    case PrimitiveTypeCode.String:
                        return KnownType.String;
                    default:
                        return KnownType.Unknown;
                }
            }

            public KnownType GetSystemType()
            {
                return KnownType.SystemType;
            }

            public KnownType GetSZArrayType(KnownType elementType)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromSerializedName(string name)
            {
                return KnownType.Unknown;
            }

            public PrimitiveTypeCode GetUnderlyingEnumType(KnownType type)
            {
                throw new BadImageFormatException("Unexpectedly got an enum parameter for an attribute.");
            }

            public bool IsSystemType(KnownType type)
            {
                return type == KnownType.SystemType;
            }
        }

    }
}
