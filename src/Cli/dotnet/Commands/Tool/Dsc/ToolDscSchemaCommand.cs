// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscSchemaCommand : CommandBase
{
    public ToolDscSchemaCommand(ParseResult parseResult)
        : base(parseResult)
    {
    }

    public override int Execute()
    {
        try
        {
            // Generate schema dynamically from DscToolState model
            var toolProperties = new Dictionary<string, object>();
            var requiredProperties = new List<string>();

            foreach (var prop in typeof(DscToolState).GetProperties())
            {
                var jsonPropertyAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (jsonPropertyAttr == null) continue;

                string propertyName = jsonPropertyAttr.Name;
                var propertySchema = GetPropertySchema(prop);
                
                toolProperties[propertyName] = propertySchema;

                // packageId is required
                if (propertyName == "packageId")
                {
                    requiredProperties.Add(propertyName);
                }
            }

            var schema = new
            {
                type = "object",
                properties = new
                {
                    tools = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = toolProperties,
                            required = requiredProperties.ToArray()
                        }
                    }
                }
            };

            DscWriter.WriteJson(schema, writeIndented: true);

            return 0;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }

    private static object GetPropertySchema(PropertyInfo prop)
    {
        var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        // Check for enum
        if (underlyingType.IsEnum)
        {
            var enumValues = Enum.GetNames(underlyingType);
            return new { type = "string", @enum = enumValues };
        }

        // Check for List<string>
        if (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = underlyingType.GetGenericArguments()[0];
            if (itemType == typeof(string))
            {
                return new { type = "array", items = new { type = "string" } };
            }
        }

        // Map CLR types to JSON schema types
        if (underlyingType == typeof(string))
            return new { type = "string" };
        if (underlyingType == typeof(bool))
            return new { type = "boolean" };
        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return new { type = "integer" };
        if (underlyingType == typeof(double) || underlyingType == typeof(float))
            return new { type = "number" };

        return new { type = "string" };
    }
}
