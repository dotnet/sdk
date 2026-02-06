// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal static class InfoCommand
{
    public static int Execute(OutputFormat format, bool noList = false, TextWriter? output = null)
    {
        output ??= Console.Out;

        var info = GetDotnetupInfo();
        List<InstallationInfo>? installations = null;

        if (!noList)
        {
            // --info verifies by default
            installations = InstallationLister.GetInstallations(verify: true);
        }

        if (format == OutputFormat.Json)
        {
            PrintJsonInfo(output, info, installations);
        }
        else
        {
            PrintHumanReadableInfo(output, info, installations);
        }

        return 0;
    }

    private static DotnetupInfo GetDotnetupInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        // InformationalVersion format is typically "version+commitsha" when SourceLink is enabled
        var (version, commit) = ParseInformationalVersion(informationalVersion);

        return new DotnetupInfo
        {
            Version = version,
            Commit = commit,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            Rid = RuntimeInformation.RuntimeIdentifier
        };
    }

    private static (string Version, string Commit) ParseInformationalVersion(string informationalVersion)
    {
        // Format: "1.0.0+abc123d" or just "1.0.0"
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex > 0)
        {
            var version = informationalVersion.Substring(0, plusIndex);
            var commit = informationalVersion.Substring(plusIndex + 1);
            // Truncate commit to 7 characters for display (git's standard short SHA)
            if (commit.Length > 7)
            {
                commit = commit.Substring(0, 7);
            }
            return (version, commit);
        }

        return (informationalVersion, "N/A");
    }

    private static void PrintHumanReadableInfo(TextWriter output, DotnetupInfo info, List<InstallationInfo>? installations)
    {
        output.WriteLine(Strings.InfoHeader);

        // Create an AnsiConsole that writes to our TextWriter
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output)
        });

        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadLeft(1).PadRight(1).NoWrap());
        grid.AddColumn(new GridColumn().NoWrap());

        grid.AddRow("Version:", info.Version);
        grid.AddRow("Commit:", info.Commit);
        grid.AddRow("Architecture:", info.Architecture);
        grid.AddRow("RID:", info.Rid);

        console.Write(grid);

        if (installations is not null)
        {
            InstallationLister.WriteHumanReadable(output, installations);
        }
    }

    private static void PrintJsonInfo(TextWriter output, DotnetupInfo info, List<InstallationInfo>? installations)
    {
        var fullInfo = new DotnetupFullInfo
        {
            Version = info.Version,
            Commit = info.Commit,
            Architecture = info.Architecture,
            Rid = info.Rid,
            Installations = installations
        };
        output.WriteLine(JsonSerializer.Serialize(fullInfo, DotnetupInfoJsonContext.Default.DotnetupFullInfo));
    }
}

internal class DotnetupInfo
{
    public string Version { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Rid { get; set; } = string.Empty;
}

internal class DotnetupFullInfo
{
    public string Version { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Rid { get; set; } = string.Empty;
    public List<InstallationInfo>? Installations { get; set; }
}

// Note: DotnetupInfo is not serialized directly (only DotnetupFullInfo is),
// so we don't need it in the JSON context
[JsonSerializable(typeof(DotnetupFullInfo))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class DotnetupInfoJsonContext : JsonSerializerContext
{
}
