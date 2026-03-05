// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    protected override string GetCommandName() => "list";

    protected override int ExecuteCore()
    {
        var listData = InstallationLister.GetListData(verify: !_skipVerification);

        if (_format == OutputFormat.Json)
        {
            InstallationLister.WriteJson(Console.Out, listData);
        }
        else
        {
            InstallationLister.WriteHumanReadable(Console.Out, listData);
        }

        return 0;
    }
}

/// <summary>
/// Shared logic for listing installations, used by both ListCommand and InfoCommand.
/// </summary>
internal static class InstallationLister
{
    /// <summary>
    /// Gets both install specs and installations from the manifest.
    /// </summary>
    public static ListData GetListData(bool verify = false)
    {
        var installSpecs = new List<InstallSpecInfo>();
        var installations = new List<InstallationInfo>();

        try
        {
            using var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates);
            var manifest = new DotnetupSharedManifest();
            var manifestData = manifest.ReadManifest();

            foreach (var root in manifestData.DotnetRoots)
            {
                var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

                // Collect install specs
                foreach (var spec in root.InstallSpecs)
                {
                    installSpecs.Add(new InstallSpecInfo
                    {
                        Component = spec.Component.ToString().ToLowerInvariant(),
                        VersionOrChannel = spec.VersionOrChannel,
                        Source = spec.InstallSource.ToString().ToLowerInvariant(),
                        InstallRoot = root.Path,
                        Architecture = root.Architecture.ToString().ToLowerInvariant()
                    });
                }

                // Collect installations
                foreach (var installation in root.Installations.ToList())
                {
                    if (verify)
                    {
                        var dotnetInstall = new DotnetInstall(
                            installRoot,
                            new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(installation.Version),
                            installation.Component);
                        if (!new ArchiveInstallationValidator().Validate(dotnetInstall))
                        {
                            manifest.RemoveInstallation(installRoot, installation);
                            continue;
                        }
                    }

                    installations.Add(new InstallationInfo
                    {
                        Component = installation.Component.ToString().ToLowerInvariant(),
                        Version = installation.Version,
                        InstallRoot = root.Path,
                        Architecture = root.Architecture.ToString().ToLowerInvariant()
                    });
                }
            }
        }
        catch (FileNotFoundException)
        {
            // Manifest doesn't exist - return empty data
        }

        return new ListData { InstallSpecs = installSpecs, Installations = installations };
    }

    /// <summary>
    /// Gets only installations (backwards-compatible for InfoCommand).
    /// </summary>
    public static List<InstallationInfo> GetInstallations(bool verify = false)
    {
        return GetListData(verify).Installations;
    }

    public static void WriteHumanReadable(TextWriter writer, ListData listData)
    {
        writer.WriteLine();
        writer.WriteLine(Strings.ListHeader);
        writer.WriteLine();

        if (listData.InstallSpecs.Count == 0 && listData.Installations.Count == 0)
        {
            writer.WriteLine($"  {Strings.ListNoInstallations}");
        }
        else
        {
            // Create an AnsiConsole that writes to our TextWriter
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer)
            });

            // Group by install root for cleaner display
            var allRoots = listData.InstallSpecs.Select(s => s.InstallRoot)
                .Union(listData.Installations.Select(i => i.InstallRoot))
                .Distinct();

            foreach (var rootPath in allRoots)
            {
                writer.WriteLine($"  {rootPath}");

                // Show tracked channels/specs
                var specs = listData.InstallSpecs.Where(s => s.InstallRoot == rootPath).ToList();
                if (specs.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("    Tracked channels:");

                    var specGrid = new Grid();
                    specGrid.AddColumn(new GridColumn().PadLeft(6).NoWrap());
                    specGrid.AddColumn(new GridColumn().NoWrap());
                    specGrid.AddColumn(new GridColumn().NoWrap());

                    foreach (var spec in specs.OrderBy(s => s.Component).ThenBy(s => s.VersionOrChannel))
                    {
                        specGrid.AddRow(
                            spec.ComponentDisplayName,
                            spec.VersionOrChannel,
                            $"[dim](source: {spec.Source})[/]"
                        );
                    }

                    console.Write(specGrid);
                }

                // Show installed versions
                var installs = listData.Installations.Where(i => i.InstallRoot == rootPath).ToList();
                if (installs.Count > 0)
                {
                    writer.WriteLine();
                    writer.WriteLine("    Installed versions:");

                    var installGrid = new Grid();
                    installGrid.AddColumn(new GridColumn().PadLeft(6).NoWrap());
                    installGrid.AddColumn(new GridColumn().NoWrap());
                    installGrid.AddColumn(new GridColumn().NoWrap());

                    foreach (var install in installs.OrderBy(i => i.Component).ThenBy(i => i.Version))
                    {
                        installGrid.AddRow(
                            install.ComponentDisplayName,
                            install.Version,
                            $"({install.Architecture})"
                        );
                    }

                    console.Write(installGrid);
                }

                writer.WriteLine();
            }
        }

        writer.WriteLine($"{Strings.ListTotal}: {listData.Installations.Count}");
    }

    public static void WriteJson(TextWriter writer, ListData listData)
    {
        var output = new InstallationListOutput
        {
            InstallSpecs = listData.InstallSpecs,
            Installations = listData.Installations
        };
        writer.WriteLine(JsonSerializer.Serialize(output, InstallationListJsonContext.Default.InstallationListOutput));
    }
}

internal class InstallSpecInfo
{
    public string Component { get; set; } = string.Empty;
    public string VersionOrChannel { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string InstallRoot { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Gets the human-readable display name for this component.
    /// Not serialized to JSON.
    /// </summary>
    [JsonIgnore]
    public string ComponentDisplayName => ParseComponent().GetDisplayName();

    private InstallComponent ParseComponent() =>
        Enum.Parse<InstallComponent>(Component, ignoreCase: true);
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

internal class ListData
{
    public List<InstallSpecInfo> InstallSpecs { get; set; } = [];
    public List<InstallationInfo> Installations { get; set; } = [];
}

internal class InstallationListOutput
{
    public List<InstallSpecInfo> InstallSpecs { get; set; } = [];
    public List<InstallationInfo> Installations { get; set; } = [];
}

[JsonSerializable(typeof(InstallationListOutput))]
[JsonSerializable(typeof(InstallationInfo))]
[JsonSerializable(typeof(InstallSpecInfo))]
[JsonSerializable(typeof(List<InstallationInfo>))]
[JsonSerializable(typeof(List<InstallSpecInfo>))]
[JsonSerializable(typeof(ListData))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class InstallationListJsonContext : JsonSerializerContext
{
}
