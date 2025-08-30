// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static partial class ObjectReaders
{
    public static TagHelperDescriptor ReadTagHelper(JsonDataReader reader)
        => reader.ReadNonNullObject(ReadTagHelperFromProperties);

    public static TagHelperDescriptor ReadTagHelperFromProperties(JsonDataReader reader)
    {
        var kind = reader.ReadNonNullString(nameof(TagHelperDescriptor.Kind));
        var name = reader.ReadNonNullString(nameof(TagHelperDescriptor.Name));
        var assemblyName = reader.ReadNonNullString(nameof(TagHelperDescriptor.AssemblyName));

        var displayName = reader.ReadStringOrNull(nameof(TagHelperDescriptor.DisplayName));
        var documentationObject = ReadDocumentationObject(reader, nameof(TagHelperDescriptor.Documentation));
        var tagOutputHint = reader.ReadStringOrNull(nameof(TagHelperDescriptor.TagOutputHint));
        var caseSensitive = reader.ReadBooleanOrTrue(nameof(TagHelperDescriptor.CaseSensitive));

        var tagMatchingRules = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.TagMatchingRules), ReadTagMatchingRule);
        var boundAttributes = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.BoundAttributes), ReadBoundAttribute);
        var allowedChildTags = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.AllowedChildTags), ReadAllowedChildTag);

        var metadata = ReadMetadata(reader, nameof(TagHelperDescriptor.Metadata));
        var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.Diagnostics), ReadDiagnostic);

        return new TagHelperDescriptor(
            kind, name, assemblyName,
            displayName!, documentationObject,
            tagOutputHint, caseSensitive,
            tagMatchingRules, boundAttributes, allowedChildTags,
            metadata, diagnostics);

        static TagMatchingRuleDescriptor ReadTagMatchingRule(JsonDataReader reader)
        {
            return reader.ReadNonNullObject(ReadFromProperties);

            static TagMatchingRuleDescriptor ReadFromProperties(JsonDataReader reader)
            {
                var tagName = reader.ReadNonNullString(nameof(TagMatchingRuleDescriptor.TagName));
                var parentTag = reader.ReadStringOrNull(nameof(TagMatchingRuleDescriptor.ParentTag));
                var tagStructure = (TagStructure)reader.ReadInt32OrZero(nameof(TagMatchingRuleDescriptor.TagStructure));
                var caseSensitive = reader.ReadBooleanOrTrue(nameof(TagMatchingRuleDescriptor.CaseSensitive));
                var attributes = reader.ReadImmutableArrayOrEmpty(nameof(TagMatchingRuleDescriptor.Attributes), ReadRequiredAttribute);

                var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(TagMatchingRuleDescriptor.Diagnostics), ReadDiagnostic);

                return new TagMatchingRuleDescriptor(
                    tagName, parentTag,
                    tagStructure, caseSensitive,
                    attributes, diagnostics);
            }
        }

        static RequiredAttributeDescriptor ReadRequiredAttribute(JsonDataReader reader)
        {
            return reader.ReadNonNullObject(ReadFromProperties);

            static RequiredAttributeDescriptor ReadFromProperties(JsonDataReader reader)
            {
                var flags = (RequiredAttributeDescriptorFlags)reader.ReadByte(nameof(RequiredAttributeDescriptor.Flags));
                var name = reader.ReadString(nameof(RequiredAttributeDescriptor.Name));
                var nameComparison = (RequiredAttributeNameComparison)reader.ReadByteOrZero(nameof(RequiredAttributeDescriptor.NameComparison));
                var value = reader.ReadStringOrNull(nameof(RequiredAttributeDescriptor.Value));
                var valueComparison = (RequiredAttributeValueComparison)reader.ReadByteOrZero(nameof(RequiredAttributeDescriptor.ValueComparison));

                var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(RequiredAttributeDescriptor.Diagnostics), ReadDiagnostic);

                return new RequiredAttributeDescriptor(
                    flags, name!, nameComparison, value, valueComparison, diagnostics);
            }
        }

        static BoundAttributeDescriptor ReadBoundAttribute(JsonDataReader reader)
        {
            return reader.ReadNonNullObject(ReadFromProperties);

            static BoundAttributeDescriptor ReadFromProperties(JsonDataReader reader)
            {
                var flags = (BoundAttributeFlags)reader.ReadByte(nameof(BoundAttributeDescriptor.Flags));
                var name = reader.ReadString(nameof(BoundAttributeDescriptor.Name));
                var propertyName = reader.ReadNonNullString(nameof(BoundAttributeDescriptor.PropertyName));
                var typeNameObject = ReadTypeNameObject(reader, nameof(BoundAttributeDescriptor.TypeName));
                var indexerNamePrefix = reader.ReadStringOrNull(nameof(BoundAttributeDescriptor.IndexerNamePrefix));
                var indexerTypeNameObject = ReadTypeNameObject(reader, nameof(BoundAttributeDescriptor.IndexerTypeName));
                var displayName = reader.ReadNonNullString(nameof(BoundAttributeDescriptor.DisplayName));
                var containingType = reader.ReadStringOrNull(nameof(BoundAttributeDescriptor.ContainingType));
                var documentationObject = ReadDocumentationObject(reader, nameof(BoundAttributeDescriptor.Documentation));
                var parameters = reader.ReadImmutableArrayOrEmpty(nameof(BoundAttributeDescriptor.Parameters), ReadBoundAttributeParameter);

                var metadataKind = (MetadataKind)reader.ReadByteOrDefault("MetadataKind", defaultValue: (byte)MetadataKind.None);

                var metadataObject = metadataKind switch
                {
                    MetadataKind.None => MetadataObject.None,
                    MetadataKind.TypeParameter => reader.ReadNonNullObject(nameof(BoundAttributeDescriptor.Metadata), ReadTypeParameterMetadata),
                    MetadataKind.Property => reader.ReadNonNullObject(nameof(BoundAttributeDescriptor.Metadata), ReadPropertyMetadata),
                    MetadataKind.ChildContentParameter => ChildContentParameterMetadata.Default,
                    _ => Assumed.Unreachable<MetadataObject>($"Unexpected MetadataKind '{metadataKind}'."),
                };

                var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(BoundAttributeDescriptor.Diagnostics), ReadDiagnostic);

                return new BoundAttributeDescriptor(
                    flags, name!, propertyName, typeNameObject,
                    indexerNamePrefix, indexerTypeNameObject,
                    documentationObject, displayName, containingType,
                    parameters, metadataObject, diagnostics);
            }
        }

        static BoundAttributeParameterDescriptor ReadBoundAttributeParameter(JsonDataReader reader)
        {
            return reader.ReadNonNullObject(ReadFromProperties);

            static BoundAttributeParameterDescriptor ReadFromProperties(JsonDataReader reader)
            {
                var flags = (BoundAttributeParameterFlags)reader.ReadInt32(nameof(BoundAttributeParameterDescriptor.Flags));
                var name = reader.ReadString(nameof(BoundAttributeParameterDescriptor.Name));
                var propertyName = reader.ReadNonNullString(nameof(BoundAttributeParameterDescriptor.PropertyName));
                var typeNameObject = ReadTypeNameObject(reader, nameof(BoundAttributeParameterDescriptor.TypeName));
                var documentationObject = ReadDocumentationObject(reader, nameof(BoundAttributeParameterDescriptor.Documentation));
                var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(BoundAttributeParameterDescriptor.Diagnostics), ReadDiagnostic);

                return new BoundAttributeParameterDescriptor(
                    flags, name!, propertyName, typeNameObject, documentationObject, diagnostics);
            }
        }

        static AllowedChildTagDescriptor ReadAllowedChildTag(JsonDataReader reader)
        {
            return reader.ReadNonNullObject(ReadFromProperties);

            static AllowedChildTagDescriptor ReadFromProperties(JsonDataReader reader)
            {
                var name = reader.ReadNonNullString(nameof(AllowedChildTagDescriptor.Name));
                var displayName = reader.ReadNonNullString(nameof(AllowedChildTagDescriptor.DisplayName));
                var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(AllowedChildTagDescriptor.Diagnostics), ReadDiagnostic);

                return new AllowedChildTagDescriptor(name, displayName, diagnostics);
            }
        }

        static TypeNameObject ReadTypeNameObject(JsonDataReader reader, string propertyName)
        {
            if (!reader.TryReadPropertyName(propertyName))
            {
                return default;
            }

            if (reader.IsInteger)
            {
                var index = reader.ReadByte();
                return new(index);
            }

            Debug.Assert(reader.IsString);

            var fullName = reader.ReadNonNullString();
            return new(fullName);
        }

        static DocumentationObject ReadDocumentationObject(JsonDataReader reader, string propertyName)
        {
            return reader.TryReadPropertyName(propertyName)
                ? ReadCore(reader)
                : default;

            static DocumentationObject ReadCore(JsonDataReader reader)
            {
                if (reader.IsObjectStart)
                {
                    return reader.ReadNonNullObject(static reader =>
                    {
                        var id = (DocumentationId)reader.ReadInt32(nameof(DocumentationDescriptor.Id));
                        // Check to see if the Args property was actually written before trying to read it;
                        // otherwise, assume the args are null.
                        var args = reader.TryReadPropertyName(nameof(DocumentationDescriptor.Args))
                            ? reader.ReadArray(static r => r.ReadValue())
                            : null;

                        if (args is { Length: > 0 and var length })
                        {
                            for (var i = 0; i < length; i++)
                            {
                                if (args[i] is string s)
                                {
                                    args[i] = s;
                                }
                            }
                        }

                        return DocumentationDescriptor.From(id, args);
                    });
                }
                else
                {
                    return reader.ReadString() switch
                    {
                        string s => s,
                        null => default(DocumentationObject)
                    };
                }
            }
        }

        static MetadataCollection ReadMetadata(JsonDataReader reader, string propertyName)
        {
            return reader.TryReadPropertyName(propertyName)
                ? reader.ReadNonNullObject(ReadFromProperties)
                : MetadataCollection.Empty;

            static MetadataCollection ReadFromProperties(JsonDataReader reader)
            {
                using var builder = new MetadataBuilder();

                while (reader.TryReadNextPropertyName(out var key))
                {
                    var value = reader.ReadString();
                    builder.Add(key, value);
                }

                return builder.Build();
            }
        }

        static TypeParameterMetadata ReadTypeParameterMetadata(JsonDataReader reader)
        {
            var builder = new TypeParameterMetadata.Builder
            {
                IsCascading = reader.ReadBooleanOrFalse(nameof(TypeParameterMetadata.IsCascading)),
                Constraints = reader.ReadStringOrNull(nameof(TypeParameterMetadata.Constraints)),
                NameWithAttributes = reader.ReadStringOrNull(nameof(TypeParameterMetadata.NameWithAttributes))
            };

            return builder.Build();
        }

        static PropertyMetadata ReadPropertyMetadata(JsonDataReader reader)
        {
            var builder = new PropertyMetadata.Builder
            {
                GloballyQualifiedTypeName = reader.ReadStringOrNull(nameof(PropertyMetadata.GloballyQualifiedTypeName)),
                IsChildContent = reader.ReadBooleanOrFalse(nameof(PropertyMetadata.IsChildContent)),
                IsEventCallback = reader.ReadBooleanOrFalse(nameof(PropertyMetadata.IsEventCallback)),
                IsDelegateSignature = reader.ReadBooleanOrFalse(nameof(PropertyMetadata.IsDelegateSignature)),
                IsDelegateWithAwaitableResult = reader.ReadBooleanOrFalse(nameof(PropertyMetadata.IsDelegateWithAwaitableResult)),
                IsGenericTyped = reader.ReadBooleanOrFalse(nameof(PropertyMetadata.IsGenericTyped)),
                IsInitOnlyProperty = reader.ReadBooleanOrFalse(nameof(PropertyMetadata.IsInitOnlyProperty))
            };

            return builder.Build();
        }
    }
}
