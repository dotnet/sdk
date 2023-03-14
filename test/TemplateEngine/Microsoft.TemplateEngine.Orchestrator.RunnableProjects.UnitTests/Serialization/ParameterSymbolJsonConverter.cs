// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Newtonsoft.Json;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Serialization
{
    internal class ParameterSymbolJsonConverter : JsonConverter<ParameterSymbol>
    {
        //falls back to default de-serializer if not implemented
        public override ParameterSymbol? ReadJson(JsonReader reader, Type objectType, ParameterSymbol? existingValue, bool hasExistingValue, JsonSerializer serializer) => throw new NotImplementedException();

        public override void WriteJson(JsonWriter writer, ParameterSymbol? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                return;
            }

            writer.WritePropertyName(value.Name);
            writer.WriteStartObject();

            writer.WritePropertyName("type");
            writer.WriteValue(value.Type);

            if (!string.IsNullOrEmpty(value.FileRename))
            {
                writer.WritePropertyName("fileRename");
                writer.WriteValue(value.FileRename);
            }

            if (!string.IsNullOrEmpty(value.Replaces))
            {
                writer.WritePropertyName("replaces");
                writer.WriteValue(value.Replaces);
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
                writer.WriteValue(value.DefaultValue);
            }

            if (!string.IsNullOrEmpty(value.DataType))
            {
                writer.WritePropertyName("datatype");
                writer.WriteValue(value.DataType);
            }

            if (!string.IsNullOrEmpty(value.DisplayName))
            {
                writer.WritePropertyName("displayName");
                writer.WriteValue(value.DisplayName);
            }

            if (!string.IsNullOrEmpty(value.Description))
            {
                writer.WritePropertyName("description");
                writer.WriteValue(value.Description);
            }

            if (!string.IsNullOrEmpty(value.DefaultIfOptionWithoutValue) && value.DataType != "bool")
            {
                writer.WritePropertyName("defaultIfOptionWithoutValue");
                writer.WriteValue(value.DefaultIfOptionWithoutValue);
            }

            if (value.AllowMultipleValues)
            {
                writer.WritePropertyName("allowMultipleValues");
                writer.WriteValue(value.AllowMultipleValues);
            }

            if (value.EnableQuotelessLiterals)
            {
                writer.WritePropertyName("enableQuotelessLiterals");
                writer.WriteValue(value.EnableQuotelessLiterals);
            }

            if (value.IsRequired)
            {
                writer.WritePropertyName("isRequired");
                writer.WriteValue(value.IsRequired);
            }
            else if (!string.IsNullOrEmpty(value.IsRequiredCondition))
            {
                writer.WritePropertyName("isRequired");
                writer.WriteValue(value.IsRequiredCondition);
            }

            if (value.Precedence.PrecedenceDefinition == PrecedenceDefinition.Disabled)
            {
                writer.WritePropertyName("isEnabled");
                writer.WriteValue(false);
            }
            else if (!string.IsNullOrEmpty(value.IsEnabledCondition))
            {
                writer.WritePropertyName("isEnabled");
                writer.WriteValue(value.IsEnabledCondition);
            }

            if (value.Choices != null)
            {
                writer.WritePropertyName("choices");
                writer.WriteStartArray();
                foreach (KeyValuePair<string, ParameterChoice> choice in value.Choices)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("choice");
                    writer.WriteValue(choice.Key);
                    if (!string.IsNullOrEmpty(choice.Value.DisplayName))
                    {
                        writer.WritePropertyName("displayName");
                        writer.WriteValue(choice.Value.DisplayName);
                    }

                    if (!string.IsNullOrEmpty(choice.Value.Description))
                    {
                        writer.WritePropertyName("description");
                        writer.WriteValue(choice.Value.Description);
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
