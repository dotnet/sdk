// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

internal class ListCommand : CommandBase
{
    private readonly OutputFormat _format;
    private readonly bool _skipVerification;

    public ListCommand(ParseResult parseResult) : base(parseResult)
    {
        _format = parseResult.GetValue(CommonOptions.FormatOption);
        _skipVerification = parseResult.GetValue(ListCommandParser.NoVerifyOption);
    }

    public override int Execute()
    {
        var installations = InstallationLister.GetInstallations(verify: !_skipVerification);

        if (_format == OutputFormat.Json)
        {
            InstallationLister.WriteJson(Console.Out, installations);
        }
        else
        {
            InstallationLister.WriteHumanReadable(Console.Out, installations);
        }

        return 0;
    }
}

/// <summary>
/// Shared logic for listing installations, used by both ListCommand and InfoCommand.
/// </summary>
internal static class InstallationLister
{
    public static List<InstallationInfo> GetInstallations(bool verify = false)
    {
        var installations = new List<InstallationInfo>();

        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest();
            IInstallationValidator? validator = verify ? new ArchiveInstallationValidator() : null;
            var installs = manifest.GetInstalledVersions(validator);

            foreach (var install in installs)
            {
                installations.Add(new InstallationInfo
                {
                    Component = install.Component.ToString().ToLowerInvariant(),
                    Version = install.Version.ToString(),
                    InstallRoot = install.InstallRoot.Path,
                    Architecture = install.InstallRoot.Architecture.ToString().ToLowerInvariant()
                });
            }
        }
        catch (FileNotFoundException)
        {
            // Manifest doesn't exist - return empty list
        }

        return installations;
    }

    public static void WriteHumanReadable(TextWriter writer, List<InstallationInfo> installations)
    {
        writer.WriteLine();
        writer.WriteLine(Strings.ListHeader);
        writer.WriteLine();

        if (installations.Count == 0)
        {
            writer.WriteLine($"  {Strings.ListNoInstallations}");
        }
        else
        {
            // Group by install root for cleaner display
            var grouped = installations.GroupBy(i => i.InstallRoot);

            // Create an AnsiConsole that writes to our TextWriter
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer)
            });

            foreach (var group in grouped)
            {
                writer.WriteLine($"  {group.Key}");

                var grid = new Grid();
                grid.AddColumn(new GridColumn().PadLeft(4).NoWrap());
                grid.AddColumn(new GridColumn().NoWrap());
                grid.AddColumn(new GridColumn().NoWrap());

                foreach (var install in group.OrderBy(i => i.Component).ThenBy(i => i.Version))
                {
                    grid.AddRow(
                        install.ComponentDisplayName,
                        install.Version,
                        $"({install.Architecture})"
                    );
                }

                console.Write(grid);
                writer.WriteLine();
            }
        }

        writer.WriteLine($"{Strings.ListTotal}: {installations.Count}");
    }

    public static void WriteJson(TextWriter writer, List<InstallationInfo> installations)
    {
        var output = new InstallationListOutput
        {
            Installations = installations
        };
        writer.WriteLine(JsonSerializer.Serialize(output, InstallationListJsonContext.Default.InstallationListOutput));
    }
}

internal class InstallationInfo
{
    public string Component { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstallRoot { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the official framework name for JSON output (e.g., "Microsoft.NETCore.App").
    /// </summary>
    public string FrameworkName => ParseComponent().GetFrameworkName();

    /// <summary>
    /// Gets the human-readable display name for this component (e.g., "dotnet (runtime)").
    /// Not serialized to JSON.
    /// </summary>
    [JsonIgnore]
    public string ComponentDisplayName => ParseComponent().GetDisplayName();

    private InstallComponent ParseComponent() =>
        Enum.Parse<InstallComponent>(Component, ignoreCase: true);
}

internal class InstallationListOutput
{
    public List<InstallationInfo> Installations { get; set; } = [];
}

[JsonSerializable(typeof(InstallationListOutput))]
[JsonSerializable(typeof(InstallationInfo))]
[JsonSerializable(typeof(List<InstallationInfo>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class InstallationListJsonContext : JsonSerializerContext
{
}
