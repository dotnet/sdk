// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization
{
    internal class ParameterSymbolJsonConverter : JsonConverter<ParameterSymbol>
    {
        //falls back to default de-serializer if not implemented
        public override ParameterSymbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

        public override void Write(Utf8JsonWriter writer, ParameterSymbol value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                return;
            }

            writer.WritePropertyName(value.Name);
            writer.WriteStartObject();

            writer.WritePropertyName("type");
            writer.WriteStringValue(value.Type);

            if (!string.IsNullOrEmpty(value.FileRename))
            {
                writer.WritePropertyName("fileRename");
                writer.WriteStringValue(value.FileRename);
            }

            if (!string.IsNullOrEmpty(value.Replaces))
            {
                writer.WritePropertyName("replaces");
                writer.WriteStringValue(value.Replaces);
            }

            if (value.ReplacementContexts.Any())
            {
                throw new NotSupportedException("Serializing replacement context is not supported.");
            }

            if (value.Forms != SymbolValueFormsModel.Default)
            {
                throw new NotSupportedException("Serializing forms is not supported.");
            }

            if (!string.IsNullOrEmpty(value.DefaultValue))
            {
                writer.WritePropertyName("defaultValue");
                writer.WriteStringValue(value.DefaultValue);
            }

            if (!string.IsNullOrEmpty(value.DataType))
            {
                writer.WritePropertyName("datatype");
                writer.WriteStringValue(value.DataType);
            }

            if (!string.IsNullOrEmpty(value.DisplayName))
            {
                writer.WritePropertyName("displayName");
                writer.WriteStringValue(value.DisplayName);
            }

            if (!string.IsNullOrEmpty(value.Description))
            {
                writer.WritePropertyName("description");
                writer.WriteStringValue(value.Description);
            }

            if (!string.IsNullOrEmpty(value.DefaultIfOptionWithoutValue) && value.DataType != "bool")
            {
                writer.WritePropertyName("defaultIfOptionWithoutValue");
                writer.WriteStringValue(value.DefaultIfOptionWithoutValue);
            }

            if (value.AllowMultipleValues)
            {
                writer.WritePropertyName("allowMultipleValues");
                writer.WriteBooleanValue(value.AllowMultipleValues);
            }

            if (value.EnableQuotelessLiterals)
            {
                writer.WritePropertyName("enableQuotelessLiterals");
                writer.WriteBooleanValue(value.EnableQuotelessLiterals);
            }

            if (value.IsRequired)
            {
                writer.WritePropertyName("isRequired");
                writer.WriteBooleanValue(value.IsRequired);
            }
            else if (!string.IsNullOrEmpty(value.IsRequiredCondition))
            {
                writer.WritePropertyName("isRequired");
                writer.WriteStringValue(value.IsRequiredCondition);
            }

            if (value.Precedence.PrecedenceDefinition == PrecedenceDefinition.Disabled)
            {
                writer.WritePropertyName("isEnabled");
                writer.WriteBooleanValue(false);
            }
            else if (!string.IsNullOrEmpty(value.IsEnabledCondition))
            {
                writer.WritePropertyName("isEnabled");
                writer.WriteStringValue(value.IsEnabledCondition);
            }

            if (value.Choices != null)
            {
                writer.WritePropertyName("choices");
                writer.WriteStartArray();
                foreach (KeyValuePair<string, ParameterChoice> choice in value.Choices)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("choice");
                    writer.WriteStringValue(choice.Key);
                    if (!string.IsNullOrEmpty(choice.Value.DisplayName))
                    {
                        writer.WritePropertyName("displayName");
                        writer.WriteStringValue(choice.Value.DisplayName);
                    }

                    if (!string.IsNullOrEmpty(choice.Value.Description))
                    {
                        writer.WritePropertyName("description");
                        writer.WriteStringValue(choice.Value.Description);
                    }
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        internal static ParameterSymbolJsonConverter Instance { get; } = new ParameterSymbolJsonConverter();
    }

}
