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
    private readonly string? _manifestPath;
    private readonly string? _installPath;

    public ListCommand(ParseResult parseResult) : base(parseResult)
    {
        _format = parseResult.GetValue(CommonOptions.FormatOption);
        _skipVerification = parseResult.GetValue(ListCommandParser.NoVerifyOption);
        _manifestPath = parseResult.GetValue(CommonOptions.ManifestPathOption);
        _installPath = parseResult.GetValue(CommonOptions.InstallPathOption);
    }

    protected override string GetCommandName() => "list";

    protected override int ExecuteCore()
    {
        var listData = InstallationLister.GetListData(verify: !_skipVerification, manifestPath: _manifestPath, installPath: _installPath);

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
    public static ListData GetListData(bool verify = false, string? manifestPath = null, string? installPath = null)
    {
        var installSpecs = new List<InstallSpecInfo>();
        var installations = new List<InstallationInfo>();

        DotnetupManifestData manifestData;
        using (var mutex = new ScopedMutex(Constants.MutexNames.ModifyInstallationStates))
        {
            var manifest = new DotnetupSharedManifest(manifestPath);
            manifestData = manifest.ReadManifest();
        }

        var validator = new ArchiveInstallationValidator();

        var roots = installPath is not null
            ? manifestData.DotnetRoots.Where(r => string.Equals(
                Path.GetFullPath(r.Path), Path.GetFullPath(installPath), StringComparison.OrdinalIgnoreCase))
            : manifestData.DotnetRoots;

        foreach (var root in roots)
        {
            var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

            // Collect install specs
            foreach (var spec in root.InstallSpecs)
            {
                installSpecs.Add(new InstallSpecInfo
                {
                    Component = spec.Component,
                    VersionOrChannel = spec.VersionOrChannel,
                    Source = spec.InstallSource,
                    GlobalJsonPath = spec.GlobalJsonPath,
                    InstallRoot = root.Path,
                    Architecture = root.Architecture
                });
            }

            // Collect installations
            foreach (var installation in root.Installations)
            {
                bool? isValid = null;
                string? validationFailure = null;

                if (verify)
                {
                    var dotnetInstall = new DotnetInstall(
                        installRoot,
                        new Microsoft.Deployment.DotNet.Releases.ReleaseVersion(installation.Version),
                        installation.Component);
                    isValid = validator.Validate(dotnetInstall, out validationFailure);
                }

                installations.Add(new InstallationInfo
                {
                    Component = installation.Component,
                    Version = installation.Version,
                    InstallRoot = root.Path,
                    Architecture = root.Architecture,
                    IsValid = isValid,
                    ValidationFailure = validationFailure
                });
            }
        }

        return new ListData { InstallSpecs = installSpecs, Installations = installations };
    }

    private const int IndentSize = 2;

    private static string Indent(int level, string text) => $"{new string(' ', level * IndentSize)}{text}";

    public static void WriteHumanReadable(TextWriter writer, ListData listData)
    {
        // Create an AnsiConsole that writes to our TextWriter
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer)
        });

        writer.WriteLine();
        console.MarkupLine($"Installations [{DotnetupTheme.Current.Dim}](managed by dotnetup)[/]:");
        writer.WriteLine();

        if (listData.InstallSpecs.Count == 0 && listData.Installations.Count == 0)
        {
            writer.WriteLine(Indent(1, Strings.ListNoInstallations));
        }
        else
        {

            // Group by install root for cleaner display
            var allRoots = listData.InstallSpecs.Select(s => s.InstallRoot)
                .Union(listData.Installations.Select(i => i.InstallRoot))
                .Distinct();

            foreach (var rootPath in allRoots)
            {
                writer.WriteLine(Indent(1, rootPath));

                var specs = listData.InstallSpecs.Where(s => s.InstallRoot == rootPath).ToList();
                DisplayInstallSpecs(writer, console, specs);

                var installs = listData.Installations.Where(i => i.InstallRoot == rootPath).ToList();
                DisplayInstallations(writer, console, installs);

                writer.WriteLine();
            }
        }

        writer.WriteLine($"{Strings.ListTotal}: {listData.Installations.Count}");
    }

    private static void DisplayInstallSpecs(TextWriter writer, IAnsiConsole console, List<InstallSpecInfo> specs)
    {
        if (specs.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine(Indent(2, "Tracked channels:"));

            var specGrid = CreateIndentedGrid();

            foreach (var spec in specs.OrderBy(s => s.Component).ThenBy(s => s.VersionOrChannel))
            {
                string sourceDisplay = spec.Source == InstallSource.GlobalJson && spec.GlobalJsonPath is not null
                    ? spec.GlobalJsonPath
                    : spec.Source.ToString().ToLowerInvariant();

                specGrid.AddRow(
                    $"{spec.Component.GetDisplayName()} {spec.VersionOrChannel}",
                    $"[{DotnetupTheme.Current.Dim}](source: {sourceDisplay})[/]"
                );
            }

            console.Write(specGrid);
        }
    }

    private static void DisplayInstallations(TextWriter writer, IAnsiConsole console, List<InstallationInfo> installs)
    {
        if (installs.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine(Indent(2, "Installed versions:"));

            var installGrid = CreateIndentedGrid();

            foreach (var install in installs.OrderBy(i => i.Component).ThenBy(i => i.Version))
            {
                string status = install.IsValid == false
                    ? $"[{DotnetupTheme.Current.Error}]({install.Architecture} — invalid: {install.ValidationFailure})[/]"
                    : $"({install.Architecture})";

                installGrid.AddRow(
                    $"{install.Component.GetDisplayName()} {install.Version}",
                    status
                );
            }

            console.Write(installGrid);
        }
    }

    private static Grid CreateIndentedGrid()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadLeft(3 * IndentSize).PadRight(IndentSize).NoWrap());
        grid.AddColumn(new GridColumn().NoWrap());
        return grid;
    }

    public static void WriteJson(TextWriter writer, ListData listData)
    {
        writer.WriteLine(JsonSerializer.Serialize(listData, InstallationListJsonContext.Default.ListData));
    }
}

/// <summary>
/// Represents an install spec (tracked channel) in the list output.
/// This type defines part of the JSON output contract — changes to property names,
/// types, or structure are breaking changes for consumers.
/// </summary>
internal class InstallSpecInfo
{
    public InstallComponent Component { get; set; }
    public string VersionOrChannel { get; set; } = string.Empty;
    public InstallSource Source { get; set; }
    public string? GlobalJsonPath { get; set; }
    public string InstallRoot { get; set; } = string.Empty;
    public InstallArchitecture Architecture { get; set; }
}

/// <summary>
/// Represents an installed .NET component in the list output.
/// This type defines part of the JSON output contract — changes to property names,
/// types, or structure are breaking changes for consumers.
/// </summary>
internal class InstallationInfo
{
    public InstallComponent Component { get; set; }
    public string Version { get; set; } = string.Empty;
    public string InstallRoot { get; set; } = string.Empty;
    public InstallArchitecture Architecture { get; set; }

    /// <summary>
    /// Validation status — null if not verified, true if valid, false if invalid.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsValid { get; set; }

    /// <summary>
    /// Validation failure reason, if IsValid is false.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValidationFailure { get; set; }

    /// <summary>
    /// Gets the official framework name for JSON output (e.g., "Microsoft.NETCore.App").
    /// </summary>
    public string FrameworkName => Component.GetFrameworkName();
}

/// <summary>
/// Root data type for the list command output.
/// This type defines the JSON output contract — changes to property names,
/// types, or structure are breaking changes for consumers.
/// </summary>
internal class ListData
{
    public List<InstallSpecInfo> InstallSpecs { get; set; } = [];
    public List<InstallationInfo> Installations { get; set; } = [];
}

[JsonSerializable(typeof(ListData))]
[JsonSerializable(typeof(InstallationInfo))]
[JsonSerializable(typeof(InstallSpecInfo))]
[JsonSerializable(typeof(List<InstallationInfo>))]
[JsonSerializable(typeof(List<InstallSpecInfo>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
internal partial class InstallationListJsonContext : JsonSerializerContext
{
}
