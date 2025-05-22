// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

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
        var options = new JsonWriterOptions { Indented = true };
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, options);

        writer.WriteStartObject();
        TraverseCli(command, writer);
        writer.WriteEndObject();
        writer.Flush();

        string json = Encoding.UTF8.GetString(stream.ToArray());
        Console.WriteLine(json);
    }

    private static void TraverseCli(Command command, Utf8JsonWriter writer)
    {
        writer.WriteString(nameof(command.Description).ToCamelCase(), command.Description);
        writer.WriteBoolean(nameof(command.Hidden).ToCamelCase(), command.Hidden);
        writer.WriteBoolean("hasValidators", command.GetHasValidators() ?? false);

        writer.WriteStartArray(nameof(command.Aliases).ToCamelCase());
        foreach (var alias in command.Aliases.Order())
        {
            writer.WriteStringValue(alias);
        }
        writer.WriteEndArray();

        writer.WriteBoolean(nameof(command.TreatUnmatchedTokensAsErrors).ToCamelCase(), command.TreatUnmatchedTokensAsErrors);

        writer.WriteStartObject(nameof(command.Arguments).ToCamelCase());
        foreach (var argument in command.Arguments.OrderBy(a => a.Name))
        {
            // TODO: Check these
            writer.WriteStartObject(argument.Name);

            writer.WriteString(nameof(argument.Description).ToCamelCase(), argument.Description);
            writer.WriteBoolean(nameof(argument.Hidden).ToCamelCase(), argument.Hidden);
            writer.WriteBoolean("hasValidators", argument.GetHasValidators() ?? false);
            writer.WriteString(nameof(argument.HelpName).ToCamelCase(), argument.HelpName);
            writer.WriteString(nameof(argument.ValueType).ToCamelCase(), argument.ValueType.FullName);
            writer.WriteBoolean(nameof(argument.HasDefaultValue).ToCamelCase(), argument.HasDefaultValue);

            writer.WriteStartObject(nameof(argument.Arity).ToCamelCase());
            writer.WriteNumber(nameof(argument.Arity.MinimumNumberOfValues).ToCamelCase(), argument.Arity.MinimumNumberOfValues);
            writer.WriteNumber(nameof(argument.Arity.MaximumNumberOfValues).ToCamelCase(), argument.Arity.MaximumNumberOfValues);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
        writer.WriteEndObject();

        writer.WriteStartObject(nameof(command.Options).ToCamelCase());
        foreach (var option in command.Options.OrderBy(o => o.Name))
        {
            // TODO: Check these
            writer.WriteStartObject(option.Name);

            writer.WriteString(nameof(option.Description).ToCamelCase(), option.Description);
            writer.WriteBoolean(nameof(option.Hidden).ToCamelCase(), option.Hidden);
            writer.WriteBoolean("hasValidators", option.GetHasValidators() ?? false);

            writer.WriteStartArray(nameof(option.Aliases).ToCamelCase());
            foreach (var alias in option.Aliases.Order())
            {
                writer.WriteStringValue(alias);
            }
            writer.WriteEndArray();

            writer.WriteString(nameof(option.HelpName).ToCamelCase(), option.HelpName);
            var internalArgument = option.GetArgument();
            writer.WriteString(nameof(internalArgument.ValueType).ToCamelCase(), internalArgument.ValueType.FullName);
            writer.WriteBoolean(nameof(option.HasDefaultValue).ToCamelCase(), option.HasDefaultValue);

            writer.WriteStartObject(nameof(option.Arity).ToCamelCase());
            writer.WriteNumber(nameof(option.Arity.MinimumNumberOfValues).ToCamelCase(), option.Arity.MinimumNumberOfValues);
            writer.WriteNumber(nameof(option.Arity.MaximumNumberOfValues).ToCamelCase(), option.Arity.MaximumNumberOfValues);
            writer.WriteEndObject();

            writer.WriteBoolean(nameof(option.Required).ToCamelCase(), option.Required);
            writer.WriteBoolean(nameof(option.Recursive).ToCamelCase(), option.Recursive);
            writer.WriteBoolean(nameof(option.AllowMultipleArgumentsPerToken).ToCamelCase(), option.AllowMultipleArgumentsPerToken);

            writer.WriteEndObject();
        }
        writer.WriteEndObject();

        writer.WriteStartObject(nameof(command.Subcommands).ToCamelCase());
        foreach (var subCommand in command.Subcommands.OrderBy(sc => sc.Name))
        {
            writer.WriteStartObject(subCommand.Name);
            TraverseCli(subCommand, writer);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
