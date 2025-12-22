// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static partial class ObjectReaders
{
    public static TagHelperDescriptor ReadTagHelper(JsonDataReader reader)
        => reader.ReadNonNullObject(ReadTagHelperFromProperties);

    public static TagHelperDescriptor ReadTagHelperFromProperties(JsonDataReader reader)
    {
        var flags = (TagHelperFlags)reader.ReadByte(nameof(TagHelperDescriptor.Flags));
        var kind = (TagHelperKind)reader.ReadByteOrDefault(nameof(TagHelperDescriptor.Kind), defaultValue: (byte)TagHelperKind.Component);
        var runtimeKind = (RuntimeKind)reader.ReadByteOrDefault(nameof(TagHelperDescriptor.RuntimeKind), defaultValue: (byte)RuntimeKind.IComponent);
        var name = reader.ReadNonNullString(nameof(TagHelperDescriptor.Name));
        var assemblyName = reader.ReadNonNullString(nameof(TagHelperDescriptor.AssemblyName));

        var displayName = reader.ReadStringOrNull(nameof(TagHelperDescriptor.DisplayName));
        var typeNameObject = ReadTypeNameObject(reader, nameof(TagHelperDescriptor.TypeName));
        var documentationObject = ReadDocumentationObject(reader, nameof(TagHelperDescriptor.Documentation));
        var tagOutputHint = reader.ReadStringOrNull(nameof(TagHelperDescriptor.TagOutputHint));

        var tagMatchingRules = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.TagMatchingRules), ReadTagMatchingRule);
        var boundAttributes = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.BoundAttributes), ReadBoundAttribute);
        var allowedChildTags = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.AllowedChildTags), ReadAllowedChildTag);

        var metadata = ReadMetadata(reader, nameof(TagHelperDescriptor.Metadata));
        var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(TagHelperDescriptor.Diagnostics), ReadDiagnostic);

        return new TagHelperDescriptor(
            flags, kind, runtimeKind, name, assemblyName,
            displayName!, typeNameObject, documentationObject, tagOutputHint,
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
                var metadata = ReadMetadata(reader, nameof(BoundAttributeDescriptor.Metadata));
                var diagnostics = reader.ReadImmutableArrayOrEmpty(nameof(BoundAttributeDescriptor.Diagnostics), ReadDiagnostic);

                return new BoundAttributeDescriptor(
                    flags, name!, propertyName, typeNameObject,
                    indexerNamePrefix, indexerTypeNameObject,
                    documentationObject, displayName, containingType,
                    parameters, metadata, diagnostics);
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

            if (reader.TryReadNull())
            {
                return default;
            }

            if (reader.IsInteger)
            {
                var index = reader.ReadByte();
                return new(index);
            }

            if (reader.IsObjectStart)
            {
                return reader.ReadNonNullObject(static reader =>
                {
                    var fullName = reader.ReadNonNullString(nameof(TypeNameObject.FullName));
                    var namespaceName = reader.ReadStringOrNull(nameof(TypeNameObject.Namespace));
                    var name = reader.ReadStringOrNull(nameof(TypeNameObject.Name));

                    return TypeNameObject.From(fullName, namespaceName, name);
                });
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

        static MetadataObject ReadMetadata(JsonDataReader reader, string propertyName)
        {
            var metadataKind = (MetadataKind)reader.ReadByteOrDefault(WellKnownPropertyNames.MetadataKind, defaultValue: (byte)MetadataKind.None);

            return metadataKind switch
            {
                MetadataKind.None => MetadataObject.None,
                MetadataKind.TypeParameter => reader.ReadNonNullObjectOrDefault(propertyName, ReadTypeParameterMetadata, defaultValue: TypeParameterMetadata.Default),
                MetadataKind.Property => reader.ReadNonNullObjectOrDefault(propertyName, ReadPropertyMetadata, defaultValue: PropertyMetadata.Default),
                MetadataKind.ChildContentParameter => ChildContentParameterMetadata.Default,
                MetadataKind.Bind => reader.ReadNonNullObjectOrDefault(propertyName, ReadBindMetadata, defaultValue: BindMetadata.Default),
                MetadataKind.Component => reader.ReadNonNullObjectOrDefault(propertyName, ReadComponentMetadata, defaultValue: ComponentMetadata.Default),
                MetadataKind.EventHandler => reader.ReadNonNullObject(propertyName, ReadEventHandlerMetadata),
                MetadataKind.ViewComponent => reader.ReadNonNullObject(propertyName, ReadViewComponentMetadata),
                _ => Assumed.Unreachable<MetadataObject>($"Unexpected MetadataKind '{metadataKind}'."),
            };
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

        static BindMetadata ReadBindMetadata(JsonDataReader reader)
        {
            var builder = new BindMetadata.Builder
            {
                IsFallback = reader.ReadBooleanOrFalse(nameof(BindMetadata.IsFallback)),
                ValueAttribute = reader.ReadStringOrNull(nameof(BindMetadata.ValueAttribute)),
                ChangeAttribute = reader.ReadStringOrNull(nameof(BindMetadata.ChangeAttribute)),
                ExpressionAttribute = reader.ReadStringOrNull(nameof(BindMetadata.ExpressionAttribute)),
                TypeAttribute = reader.ReadStringOrNull(nameof(BindMetadata.TypeAttribute)),
                IsInvariantCulture = reader.ReadBooleanOrFalse(nameof(BindMetadata.IsInvariantCulture)),
                Format = reader.ReadStringOrNull(nameof(BindMetadata.Format))
            };

            return builder.Build();
        }
    }

    static ComponentMetadata ReadComponentMetadata(JsonDataReader reader)
    {
        var builder = new ComponentMetadata.Builder
        {
            IsGeneric = reader.ReadBooleanOrFalse(nameof(ComponentMetadata.IsGeneric)),
            HasRenderModeDirective = reader.ReadBooleanOrFalse(nameof(ComponentMetadata.HasRenderModeDirective))
        };

        return builder.Build();
    }

    static EventHandlerMetadata ReadEventHandlerMetadata(JsonDataReader reader)
    {
        var builder = new EventHandlerMetadata.Builder
        {
            EventArgsType = reader.ReadNonNullString(nameof(EventHandlerMetadata.EventArgsType))
        };

        return builder.Build();
    }

    static ViewComponentMetadata ReadViewComponentMetadata(JsonDataReader reader)
    {
        var builder = new ViewComponentMetadata.Builder
        {
            Name = reader.ReadNonNullString(nameof(ViewComponentMetadata.Name))
        };

        return builder.Build();
    }
}
