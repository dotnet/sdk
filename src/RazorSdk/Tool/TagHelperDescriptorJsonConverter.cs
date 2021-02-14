// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal class TagHelperDescriptorJsonConverter : JsonConverter<TagHelperDescriptor>
    {
        public static readonly TagHelperDescriptorJsonConverter Instance = new TagHelperDescriptorJsonConverter();

        public override bool CanConvert(Type objectType) => typeof(TagHelperDescriptor).IsAssignableFrom(objectType);

        public override TagHelperDescriptor Read(ref Utf8JsonReader reader, Type objectType,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            // Required tokens (order matters)
            var descriptorKind = reader.ReadNextStringProperty(nameof(TagHelperDescriptor.Kind));
            var typeName = reader.ReadNextStringProperty(nameof(TagHelperDescriptor.Name));
            var assemblyName = reader.ReadNextStringProperty(nameof(TagHelperDescriptor.AssemblyName));
            var builder = TagHelperDescriptorBuilder.Create(descriptorKind, typeName, assemblyName);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.Documentation)):
                        if (reader.Read()) builder.Documentation = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.TagOutputHint)):
                        if (reader.Read()) builder.TagOutputHint = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.CaseSensitive)):
                        if (reader.Read()) builder.CaseSensitive = reader.GetBoolean();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.TagMatchingRules)):
                        ReadTagMatchingRules(ref reader, builder);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.BoundAttributes)):
                        ReadBoundAttributes(ref reader, builder);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.AllowedChildTags)):
                        ReadAllowedChildTags(ref reader, builder);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagHelperDescriptor.Diagnostics)):
                        ReadDiagnostics(ref reader, out RazorDiagnosticCollection diagnostics);
                        builder.Diagnostics.AddRange(diagnostics);
                        break;
                    case JsonTokenType.PropertyName when reader.ValueTextEquals(nameof(TagHelperDescriptor.Metadata)):
                        ReadMetadata(ref reader, out IDictionary<string, string> metadata);
                        foreach (var kvp in metadata)
                        {
                            builder.Metadata[kvp.Key] = kvp.Value;
                        }
                        break;
                }
            }

            return builder.Build();
        }

        public override void Write(Utf8JsonWriter writer, TagHelperDescriptor tagHelper, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(TagHelperDescriptor.Kind), tagHelper.Kind);
            writer.WriteString(nameof(TagHelperDescriptor.Name), tagHelper.Name);
            writer.WriteString(nameof(TagHelperDescriptor.AssemblyName), tagHelper.AssemblyName);

            if (tagHelper.Documentation != null)
            {
                writer.WriteString(nameof(TagHelperDescriptor.Documentation), tagHelper.Documentation);
            }

            if (tagHelper.TagOutputHint != null)
            {
                writer.WriteString(nameof(TagHelperDescriptor.TagOutputHint), tagHelper.TagOutputHint);
            }

            writer.WriteBoolean(nameof(TagHelperDescriptor.CaseSensitive), tagHelper.CaseSensitive);

            writer.WritePropertyName(nameof(TagHelperDescriptor.TagMatchingRules));
            writer.WriteStartArray();
            foreach (var ruleDescriptor in tagHelper.TagMatchingRules)
            {
                WriteTagMatchingRule(writer, ruleDescriptor, options);
            }
            writer.WriteEndArray();

            if (tagHelper.BoundAttributes != null && tagHelper.BoundAttributes.Count > 0)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.BoundAttributes));
                writer.WriteStartArray();
                foreach (var boundAttribute in tagHelper.BoundAttributes)
                {
                    WriteBoundAttribute(writer, boundAttribute, options);
                }
                writer.WriteEndArray();
            }

            if (tagHelper.AllowedChildTags != null && tagHelper.AllowedChildTags.Count > 0)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.AllowedChildTags));
                writer.WriteStartArray();
                foreach (var allowedChildTag in tagHelper.AllowedChildTags)
                {
                    WriteAllowedChildTags(writer, allowedChildTag, options);
                }

                writer.WriteEndArray();
            }

            if (tagHelper.Diagnostics != null && tagHelper.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(TagHelperDescriptor.Diagnostics));
                JsonSerializer.Serialize(writer, tagHelper.Diagnostics, options);
            }

            writer.WritePropertyName(nameof(TagHelperDescriptor.Metadata));
            WriteMetadata(writer, tagHelper.Metadata);

            writer.WriteEndObject();
        }

        private static void WriteAllowedChildTags(Utf8JsonWriter writer, AllowedChildTagDescriptor allowedChildTag, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(AllowedChildTagDescriptor.Name), allowedChildTag.Name);
            writer.WriteString(nameof(AllowedChildTagDescriptor.DisplayName), allowedChildTag.DisplayName);

            writer.WritePropertyName(nameof(AllowedChildTagDescriptor.Diagnostics));
            JsonSerializer.Serialize(writer, allowedChildTag.Diagnostics, options);

            writer.WriteEndObject();
        }

        private static void WriteBoundAttribute(Utf8JsonWriter writer, BoundAttributeDescriptor boundAttribute, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(BoundAttributeDescriptor.Kind), boundAttribute.Kind);
            writer.WriteString(nameof(BoundAttributeDescriptor.Name), boundAttribute.Name);
            writer.WriteString(nameof(BoundAttributeDescriptor.TypeName), boundAttribute.TypeName);

            if (boundAttribute.IsEnum)
            {
                writer.WriteBoolean(nameof(BoundAttributeDescriptor.IsEnum), boundAttribute.IsEnum);
            }

            if (boundAttribute.IndexerNamePrefix != null)
            {
                writer.WriteString(nameof(BoundAttributeDescriptor.IndexerNamePrefix),
                    boundAttribute.IndexerNamePrefix);
            }

            if (boundAttribute.IndexerTypeName != null)
            {
                writer.WriteString(nameof(BoundAttributeDescriptor.IndexerTypeName), boundAttribute.IndexerTypeName);
            }

            if (boundAttribute.Documentation != null)
            {
                writer.WriteString(nameof(BoundAttributeDescriptor.Documentation), boundAttribute.Documentation);
            }

            if (boundAttribute.Diagnostics != null && boundAttribute.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.Diagnostics));
                JsonSerializer.Serialize(writer, boundAttribute.Diagnostics, options);
            }

            writer.WritePropertyName(nameof(BoundAttributeDescriptor.Metadata));
            WriteMetadata(writer, boundAttribute.Metadata);

            if (boundAttribute.BoundAttributeParameters != null && boundAttribute.BoundAttributeParameters.Count > 0)
            {
                writer.WritePropertyName(nameof(BoundAttributeDescriptor.BoundAttributeParameters));
                writer.WriteStartArray();
                foreach (var boundAttributeParameter in boundAttribute.BoundAttributeParameters)
                {
                    WriteBoundAttributeParameter(writer, boundAttributeParameter, options);
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        private static void WriteBoundAttributeParameter(Utf8JsonWriter writer,
            BoundAttributeParameterDescriptor boundAttributeParameter, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(BoundAttributeParameterDescriptor.Name), boundAttributeParameter.Name);
            writer.WriteString(nameof(BoundAttributeParameterDescriptor.TypeName), boundAttributeParameter.TypeName);

            if (boundAttributeParameter.IsEnum != default)
            {
                writer.WriteBoolean(nameof(BoundAttributeParameterDescriptor.IsEnum), boundAttributeParameter.IsEnum);
            }

            if (boundAttributeParameter.Documentation != null)
            {
                writer.WriteString(nameof(BoundAttributeParameterDescriptor.Documentation),
                    boundAttributeParameter.Documentation);
            }

            if (boundAttributeParameter.Diagnostics != null && boundAttributeParameter.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Diagnostics));
                JsonSerializer.Serialize(writer, boundAttributeParameter.Diagnostics, options);
            }

            writer.WritePropertyName(nameof(BoundAttributeParameterDescriptor.Metadata));
            WriteMetadata(writer, boundAttributeParameter.Metadata);

            writer.WriteEndObject();
        }

        private static void WriteMetadata(Utf8JsonWriter writer, IReadOnlyDictionary<string, string> metadata)
        {
            writer.WriteStartObject();
            foreach (var kvp in metadata)
            {
                writer.WriteString(kvp.Key, kvp.Value);
            }

            writer.WriteEndObject();
        }

        private static void WriteTagMatchingRule(Utf8JsonWriter writer, TagMatchingRuleDescriptor ruleDescriptor, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(TagMatchingRuleDescriptor.TagName), ruleDescriptor.TagName);

            if (ruleDescriptor.ParentTag != null)
            {
                writer.WriteString(nameof(TagMatchingRuleDescriptor.ParentTag), ruleDescriptor.ParentTag);
            }

            if (ruleDescriptor.TagStructure != default)
            {
                writer.WriteNumber(nameof(TagMatchingRuleDescriptor.TagStructure), (int)ruleDescriptor.TagStructure);
            }

            if (ruleDescriptor.Attributes != null && ruleDescriptor.Attributes.Count > 0)
            {
                writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.Attributes));
                writer.WriteStartArray();
                foreach (var requiredAttribute in ruleDescriptor.Attributes)
                {
                    WriteRequiredAttribute(writer, requiredAttribute, options);
                }

                writer.WriteEndArray();
            }

            if (ruleDescriptor.Diagnostics != null && ruleDescriptor.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(TagMatchingRuleDescriptor.Diagnostics));
                JsonSerializer.Serialize(writer, ruleDescriptor.Diagnostics, options);
            }

            writer.WriteEndObject();
        }

        private static void WriteRequiredAttribute(Utf8JsonWriter writer, RequiredAttributeDescriptor requiredAttribute, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString(nameof(RequiredAttributeDescriptor.Name), requiredAttribute.Name);

            if (requiredAttribute.NameComparison != default)
            {
                writer.WriteNumber(nameof(RequiredAttributeDescriptor.NameComparison),
                    (int)requiredAttribute.NameComparison);
            }

            if (requiredAttribute.Value != null)
            {
                writer.WriteString(nameof(RequiredAttributeDescriptor.Value), requiredAttribute.Value);
            }

            if (requiredAttribute.ValueComparison != default)
            {
                writer.WriteNumber(nameof(RequiredAttributeDescriptor.ValueComparison),
                    (int)requiredAttribute.ValueComparison);
            }

            if (requiredAttribute.Diagnostics != null && requiredAttribute.Diagnostics.Count > 0)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Diagnostics));
                JsonSerializer.Serialize<IReadOnlyList<RazorDiagnostic>>(writer, requiredAttribute.Diagnostics, options);
            }

            if (requiredAttribute.Metadata != null && requiredAttribute.Metadata.Count > 0)
            {
                writer.WritePropertyName(nameof(RequiredAttributeDescriptor.Metadata));
                WriteMetadata(writer, requiredAttribute.Metadata);
            }

            writer.WriteEndObject();
        }

        private static void ReadBoundAttributes(ref Utf8JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.IsValidStartArray())
            {
                return;
            }

            do
            {
                ReadBoundAttribute(ref reader, builder);
            } while (reader.TokenType != JsonTokenType.EndArray);
        }

        private static void ReadBoundAttribute(ref Utf8JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.IsValidStartObject())
            {
                return;
            }

            string name, typeName, indexerAttributeNamePrefix, indexerValueTypeName, documentation;
            name = typeName = indexerAttributeNamePrefix = indexerValueTypeName = documentation = default;
            bool isDictionary, isEnum;
            isDictionary = isEnum = default;
            RazorDiagnosticCollection diagnostics = new RazorDiagnosticCollection();
            IDictionary<string, string> metadata = new Dictionary<string, string>();
            IList<IDictionary<string, object>> boundAttributeParameters = new List<IDictionary<string, object>>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.Name)):
                        if (reader.Read()) name = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.TypeName)):
                        if (reader.Read()) typeName = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.Documentation)):
                        if (reader.Read()) documentation = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.IndexerNamePrefix)):
                        if (reader.Read())
                        {
                            var indexerNamePrefix = reader.GetString();
                            if (indexerNamePrefix != null)
                            {
                                isDictionary = true;
                                indexerAttributeNamePrefix = indexerNamePrefix;
                            }
                        }   
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.IndexerTypeName)):
                        if (reader.Read())
                        {
                            var indexerTypeName = reader.GetString();
                            if (indexerTypeName != null)
                            {
                                isDictionary = true;
                                indexerValueTypeName = indexerTypeName;
                            }
                        }
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.IsEnum)):
                        if (reader.Read()) isEnum = reader.GetBoolean();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.BoundAttributeParameters)):
                        ReadBoundAttributeParameters(ref reader, out boundAttributeParameters);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.Diagnostics)):
                        ReadDiagnostics(ref reader, out diagnostics);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeDescriptor.Metadata)):
                        ReadMetadata(ref reader, out metadata);
                        break;
                }
            }

            builder.BindAttribute(attribute =>
            {
                attribute.Name = name;
                attribute.TypeName = typeName;
                attribute.IsDictionary = isDictionary;
                attribute.IndexerAttributeNamePrefix = indexerAttributeNamePrefix;
                attribute.IndexerValueTypeName = indexerValueTypeName;
                attribute.IsEnum = isEnum;
                attribute.Diagnostics.AddRange(diagnostics);

                foreach (var kvp in metadata)
                {
                    attribute.Metadata[kvp.Key] = kvp.Value;
                }

                foreach (var boundAttribute in boundAttributeParameters)
                {
                    attribute.BindAttributeParameter(p =>
                    {
                        if (boundAttribute.TryGetValue("Name", out var name))
                            p.Name = (string) name;
                        if (boundAttribute.TryGetValue("TypeName", out var typeName))
                            p.TypeName =  (string) typeName;
                        if (boundAttribute.TryGetValue("IsEnum", out var isEnum))
                            p.IsEnum = (bool) isEnum;
                        if (boundAttribute.TryGetValue("Documentation", out var documentation))
                            p.Documentation =  (string) documentation;
                        if (boundAttribute.TryGetValue("Metadata", out var boundAttributeMetadata))
                        {
                            foreach (var kvp in (Dictionary<string, string>) boundAttributeMetadata)
                            {
                                p.Metadata[kvp.Key] = kvp.Value;
                            }
                        }

                        if (boundAttribute.TryGetValue("Diagnostics", out var boundAttributeDiagnostics))
                        {
                            p.Diagnostics.AddRange((RazorDiagnosticCollection)boundAttributeDiagnostics);
                        }
                    });
                }
            });
        }

        private static void ReadBoundAttributeParameters(ref Utf8JsonReader reader,
            out IList<IDictionary<string, object>> boundAttributeParameters)
        {
            boundAttributeParameters = new List<IDictionary<string, object>>();

            if (!reader.IsValidStartArray())
            {
                return;
            }

            do
            {
                ReadBoundAttributeParameter(ref reader, boundAttributeParameters);
            } while (reader.TokenType != JsonTokenType.EndArray);
        }

        private static void ReadBoundAttributeParameter(ref Utf8JsonReader reader,
            IList<IDictionary<string, object>> boundAttributeParameters)
        {
            if (!reader.IsValidStartObject())
            {
                return;
            }

            var boundAttribute = new Dictionary<string, object>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeParameterDescriptor.Name)):
                        if (reader.Read()) boundAttribute.Add("Name", reader.GetString());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeParameterDescriptor.TypeName)):
                        if (reader.Read()) boundAttribute.Add("TypeName", reader.GetString());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeParameterDescriptor.IsEnum)):
                        if (reader.Read()) boundAttribute.Add("IsEnum", reader.GetBoolean());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeParameterDescriptor.Documentation)):
                        if (reader.Read()) boundAttribute.Add("Documentation", reader.GetString());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeParameterDescriptor.Metadata)):
                        IDictionary<string, string> metadata = default;
                        ReadMetadata(ref reader, out metadata);
                        boundAttribute.Add("Metadata", metadata);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(BoundAttributeParameterDescriptor.Diagnostics)):
                        RazorDiagnosticCollection diagnostics = default;
                        ReadDiagnostics(ref reader, out diagnostics);
                        boundAttribute.Add("Diagnostics", diagnostics);
                        break;
                }
            }

            boundAttributeParameters.Add(boundAttribute);
        }

        private static void ReadTagMatchingRules(ref Utf8JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.IsValidStartArray())
            {
                return;
            }

            do
            {
                ReadTagMatchingRule(ref reader, builder);
            } while (reader.TokenType != JsonTokenType.EndArray);
        }

        private static void ReadTagMatchingRule(ref Utf8JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.IsValidStartObject())
            {
                return;
            }

            string tagName = default;
            string parentTag = default;
            TagStructure tagStructure = default;
            RazorDiagnosticCollection diagnostics = new RazorDiagnosticCollection();
            IList<IDictionary<string, object>> attributes = new List<IDictionary<string, object>>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagMatchingRuleDescriptor.TagName)):
                        if (reader.Read()) tagName = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagMatchingRuleDescriptor.ParentTag)):
                        if (reader.Read()) parentTag = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagMatchingRuleDescriptor.TagStructure)):
                        if (reader.Read()) tagStructure = (TagStructure) reader.GetInt32();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagMatchingRuleDescriptor.Attributes)):
                        ReadRequiredAttributeValues(ref reader, out attributes);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(TagMatchingRuleDescriptor.Diagnostics)):
                        ReadDiagnostics(ref reader, out diagnostics);
                        break;
                }
            }

            builder.TagMatchingRule(rule =>
            {
                rule.TagName = tagName;
                rule.ParentTag = parentTag;
                rule.TagStructure = tagStructure;
                rule.Diagnostics.AddRange(diagnostics);

                foreach (var attribute in attributes)
                {
                    rule.Attribute(a =>
                    {
                        if (attribute.TryGetValue("Name", out var name))
                            a.Name = (string) name;
                        if (attribute.TryGetValue("NameComparisonMode", out var ncm) )
                            a.NameComparisonMode = (RequiredAttributeDescriptor.NameComparisonMode) ncm;
                        if (attribute.TryGetValue("Value", out var value))
                            a.Value = (string) value;
                        if (attribute.TryGetValue("ValueComparisonMode", out var vcm))
                            a.ValueComparisonMode = (RequiredAttributeDescriptor.ValueComparisonMode) vcm;
                        if (attribute.TryGetValue("Diagnostics", out var attributeDiagnostics))
                        {
                            a.Diagnostics.AddRange((RazorDiagnosticCollection)attributeDiagnostics);
                        }

                        if (attribute.TryGetValue("Metadata", out var attributeMetadata))
                        {
                            foreach (var kvp in (Dictionary<string, string>)attributeMetadata)
                            {
                                a.Metadata[kvp.Key] = kvp.Value;
                            }
                        }
                    });
                }
            });
        }

        private static void ReadRequiredAttributeValues(ref Utf8JsonReader reader,
            out IList<IDictionary<string, object>> attributes)
        {
            attributes = new List<IDictionary<string, object>>();

            if (!reader.IsValidStartArray())
            {
                return;
            }

            do
            {
                Dictionary<string, object> attribute = ReadRequiredAttribute(ref reader);
                if (attribute != null)
                {
                    attributes.Add(attribute);
                }
            } while (reader.TokenType != JsonTokenType.EndArray);
        }

        private static Dictionary<string, object> ReadRequiredAttribute(ref Utf8JsonReader reader)
        {
            if (!reader.IsValidStartObject())
            {
                return null;
            }

            var attribute = new Dictionary<string, object>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RequiredAttributeDescriptor.Name)):
                        if (reader.Read()) attribute.Add("Name", reader.GetString());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RequiredAttributeDescriptor.NameComparisonMode)):
                        if (reader.Read()) attribute.Add("NameComparisonMode", reader.GetInt32());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RequiredAttributeDescriptor.Value)):
                        if (reader.Read()) attribute.Add("Value", reader.GetString());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RequiredAttributeDescriptor.ValueComparison)):
                        if (reader.Read()) attribute.Add("ValueComparisonMode", reader.GetInt32());
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RequiredAttributeDescriptor.Diagnostics)):
                        RazorDiagnosticCollection diagnostics = default;
                        ReadDiagnostics(ref reader, out diagnostics);
                        attribute.Add("Diagnostics", diagnostics);
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RequiredAttributeDescriptor.Metadata)):
                        IDictionary<string, string> metadata = default;
                        ReadMetadata(ref reader, out metadata);
                        attribute.Add("Metadata", metadata);
                        break;
                }
            }

            return attribute;
        }

        private static void ReadAllowedChildTags(ref Utf8JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.IsValidStartArray())
            {
                return;
            }

            do
            {
                ReadAllowedChildTag(ref reader, builder);
            } while (reader.TokenType != JsonTokenType.EndArray);
        }

        private static void ReadAllowedChildTag(ref Utf8JsonReader reader, TagHelperDescriptorBuilder builder)
        {
            if (!reader.IsValidStartObject())
            {
                return;
            }

            string name = default;
            string displayName = default;
            RazorDiagnosticCollection diagnostics = new RazorDiagnosticCollection();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(AllowedChildTagDescriptor.Name)):
                        if (reader.Read()) name = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(AllowedChildTagDescriptor.DisplayName)):
                        if (reader.Read()) displayName = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(AllowedChildTagDescriptor.Diagnostics)):
                        ReadDiagnostics(ref reader, out diagnostics);
                        break;
                }
            }

            builder.AllowChildTag(childTag =>
            {
                childTag.Name = name;
                childTag.DisplayName = displayName;
                childTag.Diagnostics.AddRange(diagnostics);
            });
        }

        private static void ReadMetadata(ref Utf8JsonReader reader, out IDictionary<string, string> metadata)
        {
            metadata = new Dictionary<string, string>();

            if (!reader.IsValidStartObject())
            {
                return;
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    if (reader.Read())
                    {
                        var value = reader.GetString();
                        metadata[propertyName] = value;
                    }
                }
            }
        }

        private static void ReadDiagnostics(ref Utf8JsonReader reader, out RazorDiagnosticCollection diagnostics)
        {
            diagnostics = new RazorDiagnosticCollection();
            if (!reader.IsValidStartArray())
            {
                return;
            }

            do
            {
                RazorDiagnostic diagnostic = ReadDiagnostic(ref reader);
                if (diagnostic != null)
                {
                    diagnostics.Add(diagnostic);
                }
                
            } while (reader.TokenType != JsonTokenType.EndArray);
        }

        private static RazorDiagnostic ReadDiagnostic(ref Utf8JsonReader reader)
        {
            if (!reader.IsValidStartObject())
            {
                return null;
            }

            string id = default;
            int severity = default;
            string message = default;
            SourceSpan sourceSpan = default;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RazorDiagnostic.Id)):
                        if (reader.Read()) id = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RazorDiagnostic.Severity)):
                        if (reader.Read()) severity = reader.GetInt32();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals("Message"):
                        if (reader.Read()) message = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                        when reader.ValueTextEquals(nameof(RazorDiagnostic.Span)):
                        sourceSpan = ReadSourceSpan(ref reader);
                        break;
                }
            }

            var descriptor = new RazorDiagnosticDescriptor(id, () => message, (RazorDiagnosticSeverity)severity);
            return RazorDiagnostic.Create(descriptor, sourceSpan);
        }

        private static SourceSpan ReadSourceSpan(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
            {
                return SourceSpan.Undefined;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return SourceSpan.Undefined;
            }

            string filePath = default;
            int absoluteIndex = default;
            int lineIndex = default;
            int characterIndex = default;
            int length = default;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName
                    when reader.ValueTextEquals(nameof(SourceSpan.FilePath)):
                        if (reader.Read()) filePath = reader.GetString();
                        break;
                    case JsonTokenType.PropertyName
                    when reader.ValueTextEquals(nameof(SourceSpan.AbsoluteIndex)):
                        if (reader.Read()) absoluteIndex = reader.GetInt32();
                        break;
                    case JsonTokenType.PropertyName
                    when reader.ValueTextEquals(nameof(SourceSpan.LineIndex)):
                        if (reader.Read()) lineIndex = reader.GetInt32();
                        break;
                    case JsonTokenType.PropertyName
                    when reader.ValueTextEquals(nameof(SourceSpan.CharacterIndex)):
                        if (reader.Read()) characterIndex = reader.GetInt32();
                        break;
                    case JsonTokenType.PropertyName
                    when reader.ValueTextEquals(nameof(SourceSpan.Length)):
                        if (reader.Read()) length = reader.GetInt32();
                        break;
                }
            }

            var sourceSpan = new SourceSpan(filePath, absoluteIndex, lineIndex, characterIndex, length);
            return sourceSpan;
        }
    }
}
