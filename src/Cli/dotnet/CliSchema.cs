// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Command = System.CommandLine.Command;
using CommandResult = System.CommandLine.Parsing.CommandResult;

namespace Microsoft.DotNet.Cli;

internal static class CliSchema
{
    // Using UnsafeRelaxedJsonEscaping because this JSON is not transmitted over the web. Therefore, HTML-sensitive characters are not encoded.
    // See: https://learn.microsoft.com/dotnet/api/system.text.encodings.web.javascriptencoder.unsaferelaxedjsonescaping
    private static readonly JsonWriterOptions s_jsonWriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void PrintCliSchema(CommandResult commandResult, ITelemetry telemetryClient)
    {
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput(), s_jsonWriterOptions);
        writer.WriteStartObject();

        var command = commandResult.Command;
        // Explicitly write "name" into the root JSON object as the name for any sub-commands are used as the key to the sub-command object.
        writer.WriteString("name", command.Name);
        writer.WriteString("version", Product.Version);
        WriteCommand(command, writer);

        writer.WriteEndObject();
        writer.Flush();

        var commandString = CommandHierarchyAsString(commandResult);
        var telemetryProperties = new Dictionary<string, string> { { "command", commandString } };
        telemetryClient.TrackEvent("schema", telemetryProperties, null);
    }

    private static void WriteCommand(Command command, Utf8JsonWriter writer)
    {
        writer.WriteString(nameof(command.Description).ToCamelCase(), command.Description);
        writer.WriteBoolean(nameof(command.Hidden).ToCamelCase(), command.Hidden);

        writer.WriteStartArray(nameof(command.Aliases).ToCamelCase());
        foreach (var alias in command.Aliases.Order())
        {
            writer.WriteStringValue(alias);
        }
        writer.WriteEndArray();

        writer.WriteStartObject(nameof(command.Arguments).ToCamelCase());
        // Leave default ordering for arguments. Do not order by name.
        foreach (var argument in command.Arguments)
        {
            WriteArgument(argument, writer);
        }
        writer.WriteEndObject();

        writer.WriteStartObject(nameof(command.Options).ToCamelCase());
        foreach (var option in command.Options.OrderBy(o => o.Name))
        {
            WriteOption(option, writer);
        }
        writer.WriteEndObject();

        writer.WriteStartObject(nameof(command.Subcommands).ToCamelCase());
        foreach (var subCommand in command.Subcommands.OrderBy(sc => sc.Name))
        {
            writer.WriteStartObject(subCommand.Name);
            WriteCommand(subCommand, writer);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }

    private static void WriteArgument(Argument argument, Utf8JsonWriter writer)
    {
        writer.WriteStartObject(argument.Name);

        writer.WriteString(nameof(argument.Description).ToCamelCase(), argument.Description);
        writer.WriteBoolean(nameof(argument.Hidden).ToCamelCase(), argument.Hidden);
        writer.WriteString(nameof(argument.HelpName).ToCamelCase(), argument.HelpName);
        writer.WriteString(nameof(argument.ValueType).ToCamelCase(), argument.ValueType.ToCliTypeString());

        WriteDefaultValue(argument, writer);
        WriteArity(argument.Arity, writer);

        writer.WriteEndObject();
    }

    private static void WriteOption(Option option, Utf8JsonWriter writer)
    {
        writer.WriteStartObject(option.Name);

        writer.WriteString(nameof(option.Description).ToCamelCase(), option.Description);
        writer.WriteBoolean(nameof(option.Hidden).ToCamelCase(), option.Hidden);

        writer.WriteStartArray(nameof(option.Aliases).ToCamelCase());
        foreach (var alias in option.Aliases.Order())
        {
            writer.WriteStringValue(alias);
        }
        writer.WriteEndArray();

        writer.WriteString(nameof(option.HelpName).ToCamelCase(), option.HelpName);
        writer.WriteString(nameof(option.ValueType).ToCamelCase(), option.ValueType.ToCliTypeString());

        // GetArgument will only return null if System.CommandLine is changed to no longer contain an Argument property within Option.
        var internalArgument = option.GetArgument() ?? new DynamicArgument<string>(string.Empty);
        WriteDefaultValue(internalArgument, writer);
        WriteArity(option.Arity, writer);

        writer.WriteBoolean(nameof(option.Required).ToCamelCase(), option.Required);
        writer.WriteBoolean(nameof(option.Recursive).ToCamelCase(), option.Recursive);

        writer.WriteEndObject();
    }

    private static void WriteDefaultValue(Argument argument, Utf8JsonWriter writer)
    {
        writer.WriteBoolean(nameof(argument.HasDefaultValue).ToCamelCase(), argument.HasDefaultValue);
        writer.WritePropertyName("defaultValue");
        if (!argument.HasDefaultValue)
        {
            writer.WriteNullValue();
            return;
        }

        // Encode the value automatically based on the System.Type of the argument.
        JsonSerializer.Serialize(writer, argument.GetDefaultValue(), argument.ValueType, s_jsonSerializerOptions);
        return;
    }

    private static void WriteArity(ArgumentArity arity, Utf8JsonWriter writer)
    {
        writer.WriteStartObject(nameof(arity));

        writer.WriteNumber("minimum", arity.MinimumNumberOfValues);
        writer.WritePropertyName("maximum");
        // ArgumentArity.ZeroOrMore.MaximumNumberOfValues is required as MaximumArity in ArgumentArity is a private field.
        if (arity.MaximumNumberOfValues == ArgumentArity.ZeroOrMore.MaximumNumberOfValues)
        {
            // When the "OrMore" arity is present, write the maximum as null (thus unbounded).
            // The literal max integer value is set to an arbitrary amount (ATTOW 100000), which is not necessary to know for an external consumer.
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(arity.MaximumNumberOfValues);
        }

        writer.WriteEndObject();
    }

    // Produces a string that represents the command call.
    // For example, calling the workload install command produces `dotnet workload install`.
    private static string CommandHierarchyAsString(CommandResult commandResult)
    {
        var commands = new List<string>();
        var currentResult = commandResult;
        while (currentResult is not null)
        {
            commands.Add(currentResult.Command.Name);
            currentResult = currentResult.Parent as CommandResult;
        }

        return string.Join(" ", commands.AsEnumerable().Reverse());
    }
}
