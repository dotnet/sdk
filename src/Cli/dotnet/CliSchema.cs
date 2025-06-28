// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
    // Force the newline to be "\n" instead of the default "\r\n" for consistency across platforms (and for testing)
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        RespectNullableAnnotations = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // needed to workaround https://github.com/dotnet/aspnetcore/issues/55692, but will need to be removed when
        // we tackle AOT in favor of the source-generated JsonTypeInfo stuff
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public record ArgumentDetails(
        string? description,
        int order,
        bool hidden,
        string? helpName,
        string valueType,
        bool hasDefaultValue,
        object? defaultValue,
        ArityDetails arity);

    public record ArityDetails(
        int minimum,
        int? maximum);

    public record OptionDetails(
        string? description,
        bool hidden,
        string[]? aliases,
        string? helpName,
        string valueType,
        bool hasDefaultValue,
        object? defaultValue,
        ArityDetails arity,
        bool required,
        bool recursive);

    public record CommandDetails(
        string? description,
        bool hidden,
        string[]? aliases,
        Dictionary<string, ArgumentDetails>? arguments,
        Dictionary<string, OptionDetails>? options,
        Dictionary<string, CommandDetails>? subcommands);

    public record RootCommandDetails(
        string name,
        string version,
        string? description,
        bool hidden,
        string[]? aliases,
        Dictionary<string, ArgumentDetails>? arguments,
        Dictionary<string, OptionDetails>? options,
        Dictionary<string, CommandDetails>? subcommands
    ) : CommandDetails(description, hidden, aliases, arguments, options, subcommands);

    public static void PrintCliSchema(CommandResult commandResult, TextWriter outputWriter, ITelemetry? telemetryClient)
    {
        var command = commandResult.Command;
        RootCommandDetails transportStructure = CreateRootCommandDetails(command);
        var result = JsonSerializer.Serialize(transportStructure, s_jsonSerializerOptions);
        outputWriter.Write(result.AsSpan());
        outputWriter.Flush();
        var commandString = CommandHierarchyAsString(commandResult);
        var telemetryProperties = new Dictionary<string, string?> { { "command", commandString } };
        telemetryClient?.TrackEvent("schema", telemetryProperties, null);
    }

    public static object GetJsonSchema()
    {
        var node = s_jsonSerializerOptions.GetJsonSchemaAsNode(typeof(RootCommandDetails), new JsonSchemaExporterOptions());
        return node.ToJsonString(s_jsonSerializerOptions);
    }

    private static ArityDetails CreateArityDetails(ArgumentArity arity)
    {
        return new ArityDetails(
            minimum: arity.MinimumNumberOfValues,
            maximum: arity.MaximumNumberOfValues == ArgumentArity.ZeroOrMore.MaximumNumberOfValues ? null : arity.MaximumNumberOfValues
        );
    }

    private static RootCommandDetails CreateRootCommandDetails(Command command)
    {
        var arguments = CreateArgumentsDictionary(command.Arguments);
        var options = CreateOptionsDictionary(command.Options);
        var subcommands = CreateSubcommandsDictionary(command.Subcommands);

        return new RootCommandDetails(
            name: command.Name,
            version: Product.Version,
            description: command.Description?.ReplaceLineEndings("\n"),
            hidden: command.Hidden,
            aliases: DetermineAliases(command.Aliases),
            arguments: arguments,
            options: options,
            subcommands: subcommands
        );
    }

    private static Dictionary<string, ArgumentDetails>? CreateArgumentsDictionary(IList<Argument> arguments)
    {
        if (arguments.Count == 0)
        {
            return null;
        }
        var dict = new Dictionary<string, ArgumentDetails>();
        foreach ((var index, var argument) in arguments.Index())
        {
            dict[argument.Name] = CreateArgumentDetails(index, argument);
        }
        return dict;
    }

    private static Dictionary<string, OptionDetails>? CreateOptionsDictionary(IList<Option> options)
    {
        if (options.Count == 0)
        {
            return null;
        }
        var dict = new Dictionary<string, OptionDetails>();
        foreach (var option in options.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            dict[option.Name] = CreateOptionDetails(option);
        }
        return dict;
    }

    private static Dictionary<string, CommandDetails>? CreateSubcommandsDictionary(IList<Command> subcommands)
    {
        if (subcommands.Count == 0)
        {
            return null;
        }
        var dict = new Dictionary<string, CommandDetails>();
        foreach (var subcommand in subcommands.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            dict[subcommand.Name] = CreateCommandDetails(subcommand);
        }
        return dict;
    }

    private static string[]? DetermineAliases(ICollection<string> aliases)
    {
        if (aliases.Count == 0)
        {
            return null;
        }

        // Order the aliases to ensure consistent output.
        return aliases.Order().ToArray();
    }

    private static CommandDetails CreateCommandDetails(Command subCommand) => new CommandDetails(
                subCommand.Description?.ReplaceLineEndings("\n"),
                subCommand.Hidden,
                DetermineAliases(subCommand.Aliases),
                CreateArgumentsDictionary(subCommand.Arguments),
                CreateOptionsDictionary(subCommand.Options),
                CreateSubcommandsDictionary(subCommand.Subcommands)
            );

    private static OptionDetails CreateOptionDetails(Option option) => new OptionDetails(
                option.Description?.ReplaceLineEndings("\n"),
                option.Hidden,
                DetermineAliases(option.Aliases),
                option.HelpName,
                option.ValueType.ToCliTypeString(),
                option.HasDefaultValue,
                option.HasDefaultValue ? option.GetDefaultValue() : null,
                CreateArityDetails(option.Arity),
                option.Required,
                option.Recursive
            );

    private static ArgumentDetails CreateArgumentDetails(int index, Argument argument) => new ArgumentDetails(
                argument.Description?.ReplaceLineEndings("\n"),
                index,
                argument.Hidden,
                argument.HelpName,
                argument.ValueType.ToCliTypeString(),
                argument.HasDefaultValue,
                argument.HasDefaultValue ? argument.GetDefaultValue() : null,
                CreateArityDetails(argument.Arity)
            );

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
