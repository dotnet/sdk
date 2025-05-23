// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Command = System.CommandLine.Command;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using RuntimeEnvironment = Microsoft.DotNet.Cli.Utils.RuntimeEnvironment;

namespace Microsoft.DotNet.Cli;

public class CommandLineInfo
{
    public static void PrintVersion()
    {
        Reporter.Output.WriteLine(Product.Version);
    }

    public static void PrintInfo()
    {
        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        var commitSha = versionFile.CommitSha ?? "N/A";
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetSdkInfoLabel}");
        Reporter.Output.WriteLine($" Version:           {Product.Version}");
        Reporter.Output.WriteLine($" Commit:            {commitSha}");
        Reporter.Output.WriteLine($" Workload version:  {WorkloadCommandParser.GetWorkloadsVersion()}");
        Reporter.Output.WriteLine($" MSBuild version:   {MSBuildForwardingAppWithoutLogging.MSBuildVersion.ToString()}");
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotNetRuntimeInfoLabel}");
        Reporter.Output.WriteLine($" OS Name:     {RuntimeEnvironment.OperatingSystem}");
        Reporter.Output.WriteLine($" OS Version:  {RuntimeEnvironment.OperatingSystemVersion}");
        Reporter.Output.WriteLine($" OS Platform: {RuntimeEnvironment.OperatingSystemPlatform}");
        Reporter.Output.WriteLine($" RID:         {GetDisplayRid(versionFile)}");
        Reporter.Output.WriteLine($" Base Path:   {AppContext.BaseDirectory}");
        PrintWorkloadsInfo();
    }

    private static void PrintWorkloadsInfo()
    {
        Reporter.Output.WriteLine();
        Reporter.Output.WriteLine($"{LocalizableStrings.DotnetWorkloadInfoLabel}");
        WorkloadCommandParser.ShowWorkloadsInfo(showVersion: false);
    }

    private static string GetDisplayRid(DotnetVersionFile versionFile)
    {
        FrameworkDependencyFile fxDepsFile = new();

        string currentRid = RuntimeInformation.RuntimeIdentifier;

        // if the current RID isn't supported by the shared framework, display the RID the CLI was
        // built with instead, so the user knows which RID they should put in their "runtimes" section.
        return fxDepsFile.IsRuntimeSupported(currentRid) ?
            currentRid :
            versionFile.BuildRid;
    }

    public static void PrintCliSchema(Command command)
    {
        // Using UnsafeRelaxedJsonEscaping because this JSON is not transmitted over the web. Therefore, HTML-sensitive characters are not encoded.
        // See: https://learn.microsoft.com/dotnet/api/system.text.encodings.web.javascriptencoder.unsaferelaxedjsonescaping
        var options = new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        //using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput(), options);

        writer.WriteStartObject();
        // Explicitly write "name" into the root JSON object as the name for any sub-commands are used as the key to the sub-command object.
        writer.WriteString("name", command.Name);
        WriteCommand(command, writer);
        writer.WriteEndObject();
        writer.Flush();

        //string json = Encoding.UTF8.GetString(stream.ToArray());
        //Console.WriteLine(json);
    }

    private static void WriteCommand(Command command, Utf8JsonWriter writer)
    {
        writer.WriteString(nameof(command.Description).ToCamelCase(), command.Description);
        writer.WriteBoolean(nameof(command.Hidden).ToCamelCase(), command.Hidden);
        //writer.WriteBoolean("hasValidators", command.GetHasValidators() ?? false);

        writer.WriteStartArray(nameof(command.Aliases).ToCamelCase());
        foreach (var alias in command.Aliases.Order())
        {
            writer.WriteStringValue(alias);
        }
        writer.WriteEndArray();

        //writer.WriteBoolean(nameof(command.TreatUnmatchedTokensAsErrors).ToCamelCase(), command.TreatUnmatchedTokensAsErrors);

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

        static void WriteArgument(Argument argument, Utf8JsonWriter writer)
        {
            writer.WriteStartObject(argument.Name);

            writer.WriteString(nameof(argument.Description).ToCamelCase(), argument.Description);
            writer.WriteBoolean(nameof(argument.Hidden).ToCamelCase(), argument.Hidden);
            //writer.WriteBoolean("hasValidators", argument.GetHasValidators() ?? false);
            writer.WriteString(nameof(argument.HelpName).ToCamelCase(), argument.HelpName);
            //writer.WriteString(nameof(argument.ValueType).ToCamelCase(), argument.ValueType.FullName);
            writer.WriteString(nameof(argument.ValueType).ToCamelCase(), argument.ValueType.ToCliTypeString());
            //writer.WriteBoolean(nameof(argument.HasDefaultValue).ToCamelCase(), argument.HasDefaultValue);
            //// TODO: Can only write the string representation of the default value currently.
            //writer.WriteString("defaultValue", argument.GetDefaultValue()?.ToString());
            WriteDefaultValue(argument, writer);

            WriteArity(argument.Arity, writer);

            //writer.WriteStartObject(nameof(argument.Arity).ToCamelCase());
            //writer.WriteNumber(nameof(argument.Arity.MinimumNumberOfValues).ToCamelCase(), argument.Arity.MinimumNumberOfValues);
            //writer.WriteNumber(nameof(argument.Arity.MaximumNumberOfValues).ToCamelCase(), argument.Arity.MaximumNumberOfValues);
            //writer.WriteEndObject();

            writer.WriteEndObject();
        }

        static void WriteOption(Option option, Utf8JsonWriter writer)
        {
            writer.WriteStartObject(option.Name);

            writer.WriteString(nameof(option.Description).ToCamelCase(), option.Description);
            writer.WriteBoolean(nameof(option.Hidden).ToCamelCase(), option.Hidden);
            //writer.WriteBoolean("hasValidators", option.GetHasValidators() ?? false);

            writer.WriteStartArray(nameof(option.Aliases).ToCamelCase());
            foreach (var alias in option.Aliases.Order())
            {
                writer.WriteStringValue(alias);
            }
            writer.WriteEndArray();

            writer.WriteString(nameof(option.HelpName).ToCamelCase(), option.HelpName);
            //writer.WriteString(nameof(option.ValueType).ToCamelCase(), option.ValueType.FullName);
            writer.WriteString(nameof(option.ValueType).ToCamelCase(), option.ValueType.ToCliTypeString());
            //writer.WriteBoolean(nameof(option.HasDefaultValue).ToCamelCase(), option.HasDefaultValue);
            //var internalArgument = option.GetArgument();
            //// TODO: Can only write the string representation of the default value currently.
            //writer.WriteString("defaultValue", internalArgument.GetDefaultValue()?.ToString());
            var internalArgument = option.GetArgument();
            WriteDefaultValue(internalArgument, writer);

            WriteArity(option.Arity, writer);

            //writer.WriteStartObject(nameof(option.Arity).ToCamelCase());
            //writer.WriteNumber(nameof(option.Arity.MinimumNumberOfValues).ToCamelCase(), option.Arity.MinimumNumberOfValues);
            //writer.WriteNumber(nameof(option.Arity.MaximumNumberOfValues).ToCamelCase(), option.Arity.MaximumNumberOfValues);
            //writer.WriteEndObject();

            writer.WriteBoolean(nameof(option.Required).ToCamelCase(), option.Required);
            writer.WriteBoolean(nameof(option.Recursive).ToCamelCase(), option.Recursive);
            //writer.WriteBoolean(nameof(option.AllowMultipleArgumentsPerToken).ToCamelCase(), option.AllowMultipleArgumentsPerToken);

            writer.WriteEndObject();
        }

        static void WriteDefaultValue(Argument argument, Utf8JsonWriter writer)
        {
            writer.WriteBoolean(nameof(argument.HasDefaultValue).ToCamelCase(), argument.HasDefaultValue);
            writer.WritePropertyName("defaultValue");
            if (!argument.HasDefaultValue)
            {
                writer.WriteNullValue();
                return;
            }

            // Encode the value automatically based on the System.Type of the argument.
            JsonSerializer.Serialize(writer, argument.GetDefaultValue(), argument.ValueType, JsonSerializerOptions);
            return;
        }

        // When the "OrMore" arity is present, write the maximum as null (thus unbounded).
        // The literal max integer value is set to an arbitrary amount (ATTOW 100000), which is not necessary to know for an external consumer.
        static void WriteArity(ArgumentArity arity, Utf8JsonWriter writer)
        {
            writer.WriteStartObject(nameof(arity));

            writer.WriteNumber("minimum", arity.MinimumNumberOfValues);
            writer.WritePropertyName("maximum");
            // ArgumentArity.ZeroOrMore.MaximumNumberOfValues is required as MaximumArity in ArgumentArity is a private field.
            if (arity.MaximumNumberOfValues == ArgumentArity.ZeroOrMore.MaximumNumberOfValues)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteNumberValue(arity.MaximumNumberOfValues);
            }

            writer.WriteEndObject();
        }
    }
}
