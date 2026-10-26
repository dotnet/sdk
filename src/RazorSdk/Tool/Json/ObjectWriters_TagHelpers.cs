// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.NET.Sdk.Razor.Tool.Json;

internal static partial class ObjectWriters
{
    public static void Write(JsonDataWriter writer, TagHelperDescriptor? value)
        => writer.WriteObject(value, WriteProperties);

    public static void WriteProperties(JsonDataWriter writer, TagHelperDescriptor value)
    {
        writer.Write(nameof(value.Flags), (byte)value.Flags);
        writer.WriteIfNotDefault(nameof(value.Kind), (byte)value.Kind, defaultValue: (byte)TagHelperKind.Component);
        writer.WriteIfNotDefault(nameof(value.RuntimeKind), (byte)value.RuntimeKind, defaultValue: (byte)RuntimeKind.IComponent);
        writer.Write(nameof(value.Name), value.Name);
        writer.Write(nameof(value.AssemblyName), value.AssemblyName);
        writer.WriteIfNotNull(nameof(value.DisplayName), value.DisplayName);
        WriteTypeNameObject(writer, nameof(value.TypeName), value.TypeNameObject);
        WriteDocumentationObject(writer, nameof(value.Documentation), value.DocumentationObject);
        writer.WriteIfNotNull(nameof(value.TagOutputHint), value.TagOutputHint);
        writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.TagMatchingRules), value.TagMatchingRules, WriteTagMatchingRule);
        writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.BoundAttributes), value.BoundAttributes, WriteBoundAttribute);
        writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.AllowedChildTags), value.AllowedChildTags, WriteAllowedChildTag);
        WriteMetadata(writer, nameof(value.Metadata), value.Metadata);
        writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Diagnostics), value.Diagnostics, Write);

        static void WriteDocumentationObject(JsonDataWriter writer, string propertyName, DocumentationObject documentationObject)
        {
            switch (documentationObject.Object)
            {
                case DocumentationDescriptor descriptor:
                    writer.WriteObject(propertyName, descriptor, static (writer, value) =>
                    {
                        writer.Write(nameof(value.Id), (int)value.Id);
                        if (value.Args is { Length: > 0 })
                        {
                            writer.WriteArray(nameof(value.Args), value.Args, static (w, v) => w.WriteValue(v));
                        }
                    });

                    break;

                case string text:
                    writer.Write(propertyName, text);
                    break;

                case null:
                    // Don't write a property if there isn't any documentation.
                    break;

                default:
                    Debug.Fail($"Documentation objects should only be of type {nameof(DocumentationDescriptor)}, string, or null.");
                    break;
            }
        }

        static void WriteTypeNameObject(JsonDataWriter writer, string propertyName, TypeNameObject value)
        {
            if (value.IsNull)
            {
                // Don't write property if the value is null.
            }
            else if (value.Index is byte index)
            {
                writer.Write(propertyName, index);
            }
            else if (value.Namespace is null && value.Name is null)
            {
                // If we only have a full name, write that.
                writer.Write(propertyName, value.FullName.AssumeNotNull());
            }
            else
            {
                writer.WriteObject(propertyName, value, static (writer, value) =>
                {
                    Debug.Assert(value.Index is null);

                    writer.Write(nameof(value.FullName), value.FullName);
                    writer.WriteIfNotNull(nameof(value.Namespace), value.Namespace);
                    writer.WriteIfNotNull(nameof(value.Name), value.Name);
                });

            }
        }

        static void WriteTagMatchingRule(JsonDataWriter writer, TagMatchingRuleDescriptor value)
        {
            writer.WriteObject(value, static (writer, value) =>
            {
                writer.Write(nameof(value.TagName), value.TagName);
                writer.WriteIfNotNull(nameof(value.ParentTag), value.ParentTag);
                writer.WriteIfNotZero(nameof(value.TagStructure), (int)value.TagStructure);
                writer.WriteIfNotTrue(nameof(value.CaseSensitive), value.CaseSensitive);
                writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Attributes), value.Attributes, WriteRequiredAttribute);
                writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Diagnostics), value.Diagnostics, Write);
            });
        }

        static void WriteRequiredAttribute(JsonDataWriter writer, RequiredAttributeDescriptor value)
        {
            writer.WriteObject(value, static (writer, value) =>
            {
                writer.Write(nameof(value.Flags), (byte)value.Flags);
                writer.Write(nameof(value.Name), value.Name);
                writer.WriteIfNotZero(nameof(value.NameComparison), (byte)value.NameComparison);
                writer.WriteIfNotNull(nameof(value.Value), value.Value);
                writer.WriteIfNotZero(nameof(value.ValueComparison), (byte)value.ValueComparison);
                writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Diagnostics), value.Diagnostics, Write);
            });
        }

        static void WriteBoundAttribute(JsonDataWriter writer, BoundAttributeDescriptor value)
        {
            writer.WriteObject(value, static (writer, value) =>
            {
                writer.Write(nameof(value.Flags), (byte)value.Flags);
                writer.Write(nameof(value.Name), value.Name);
                writer.Write(nameof(value.PropertyName), value.PropertyName);
                WriteTypeNameObject(writer, nameof(value.TypeName), value.TypeNameObject);
                writer.WriteIfNotNull(nameof(value.IndexerNamePrefix), value.IndexerNamePrefix);
                WriteTypeNameObject(writer, nameof(value.IndexerTypeName), value.IndexerTypeNameObject);
                writer.WriteIfNotNull(nameof(value.DisplayName), value.DisplayName);
                writer.WriteIfNotNull(nameof(value.ContainingType), value.ContainingType);
                WriteDocumentationObject(writer, nameof(value.Documentation), value.DocumentationObject);
                writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Parameters), value.Parameters, WriteBoundAttributeParameter);

                WriteMetadata(writer, nameof(value.Metadata), value.Metadata);

                writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Diagnostics), value.Diagnostics, Write);
            });
        }

        static void WriteBoundAttributeParameter(JsonDataWriter writer, BoundAttributeParameterDescriptor value)
        {
            writer.WriteObject(value, static (writer, value) =>
            {
                writer.Write(nameof(value.Flags), (byte)value.Flags);
                writer.Write(nameof(value.Name), value.Name);
                writer.Write(nameof(value.PropertyName), value.PropertyName);
                WriteTypeNameObject(writer, nameof(value.TypeName), value.TypeNameObject);
                WriteDocumentationObject(writer, nameof(value.Documentation), value.DocumentationObject);

                writer.WriteArrayIfNotDefaultOrEmpty(nameof(value.Diagnostics), value.Diagnostics, Write);
            });
        }

        static void WriteAllowedChildTag(JsonDataWriter writer, AllowedChildTagDescriptor value)
        {
            writer.WriteObject(value, static (writer, value) =>
            {
                writer.Write(nameof(value.Name), value.Name);
                writer.Write(nameof(value.DisplayName), value.DisplayName);
                writer.WriteArray(nameof(value.Diagnostics), value.Diagnostics, Write);
            });
        }

        static void WriteMetadata(JsonDataWriter writer, string propertyName, MetadataObject metadata)
        {
            if (metadata.Kind is MetadataKind.None)
            {
                return;
            }

            writer.Write(WellKnownPropertyNames.MetadataKind, (byte)metadata.Kind);

            if (metadata.HasDefaultValue)
            {
                // No properties to write.
                return;
            }

            writer.WriteObject(propertyName, metadata, static (writer, value) =>
            {
                switch (value.Kind)
                {
                    case MetadataKind.TypeParameter:
                        WriteTypeParameterMetadata(writer, (TypeParameterMetadata)value);
                        break;

                    case MetadataKind.Property:
                        WritePropertyMetadata(writer, (PropertyMetadata)value);
                        break;

                    case MetadataKind.Bind:
                        WriteBindMetadata(writer, (BindMetadata)value);
                        break;

                    case MetadataKind.Component:
                        WriteComponentMetadata(writer, (ComponentMetadata)value);
                        break;

                    case MetadataKind.EventHandler:
                        WriteEventHandlerMetadata(writer, (EventHandlerMetadata)value);
                        break;

                    case MetadataKind.ViewComponent:
                        WriteViewComponentMetadata(writer, (ViewComponentMetadata)value);
                        break;

                    default:
                        Debug.Fail($"Unsupported metadata kind '{value.Kind}'.");
                        break;
                }
            });

            static void WriteTypeParameterMetadata(JsonDataWriter writer, TypeParameterMetadata metadata)
            {
                writer.WriteIfNotFalse(nameof(metadata.IsCascading), metadata.IsCascading);
                writer.WriteIfNotNull(nameof(metadata.Constraints), metadata.Constraints);
                writer.WriteIfNotNull(nameof(metadata.NameWithAttributes), metadata.NameWithAttributes);
            }

            static void WritePropertyMetadata(JsonDataWriter writer, PropertyMetadata metadata)
            {
                writer.WriteIfNotNull(nameof(metadata.GloballyQualifiedTypeName), metadata.GloballyQualifiedTypeName);
                writer.WriteIfNotFalse(nameof(metadata.IsChildContent), metadata.IsChildContent);
                writer.WriteIfNotFalse(nameof(metadata.IsEventCallback), metadata.IsEventCallback);
                writer.WriteIfNotFalse(nameof(metadata.IsDelegateSignature), metadata.IsDelegateSignature);
                writer.WriteIfNotFalse(nameof(metadata.IsDelegateWithAwaitableResult), metadata.IsDelegateWithAwaitableResult);
                writer.WriteIfNotFalse(nameof(metadata.IsGenericTyped), metadata.IsGenericTyped);
                writer.WriteIfNotFalse(nameof(metadata.IsInitOnlyProperty), metadata.IsInitOnlyProperty);
            }

            static void WriteBindMetadata(JsonDataWriter writer, BindMetadata metadata)
            {
                writer.WriteIfNotFalse(nameof(metadata.IsFallback), metadata.IsFallback);
                writer.WriteIfNotNull(nameof(metadata.ValueAttribute), metadata.ValueAttribute);
                writer.WriteIfNotNull(nameof(metadata.ChangeAttribute), metadata.ChangeAttribute);
                writer.WriteIfNotNull(nameof(metadata.ExpressionAttribute), metadata.ExpressionAttribute);
                writer.WriteIfNotNull(nameof(metadata.TypeAttribute), metadata.TypeAttribute);
                writer.WriteIfNotFalse(nameof(metadata.IsInvariantCulture), metadata.IsInvariantCulture);
                writer.WriteIfNotNull(nameof(metadata.Format), metadata.Format);
            }

            static void WriteComponentMetadata(JsonDataWriter writer, ComponentMetadata metadata)
            {
                writer.WriteIfNotFalse(nameof(metadata.IsGeneric), metadata.IsGeneric);
                writer.WriteIfNotFalse(nameof(metadata.HasRenderModeDirective), metadata.HasRenderModeDirective);
            }

            static void WriteEventHandlerMetadata(JsonDataWriter writer, EventHandlerMetadata metadata)
            {
                writer.Write(nameof(metadata.EventArgsType), metadata.EventArgsType);
            }

            static void WriteViewComponentMetadata(JsonDataWriter writer, ViewComponentMetadata metadata)
            {
                writer.Write(nameof(metadata.Name), metadata.Name);
            }
        }
    }
}
