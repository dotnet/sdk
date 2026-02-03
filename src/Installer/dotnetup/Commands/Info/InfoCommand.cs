// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Spectre.Console;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal class InfoCommand : CommandBase
{
    public static int Execute(OutputFormat format, bool noList = false, TextWriter? output = null)
    {
        _jsonOutput = jsonOutput;
        _noList = noList;
        _output = output ?? Console.Out;
    }

    protected override string GetCommandName() => "info";

    protected override int ExecuteCore()
    {
        var info = GetDotnetupInfo();
        List<InstallationInfo>? installations = null;

        if (!_noList)
        {
            // --info verifies by default
            installations = InstallationLister.GetInstallations(verify: true);
        }

        if (format == OutputFormat.Json)
        {
            PrintJsonInfo(_output, info, installations);
        }
        else
        {
            PrintHumanReadableInfo(_output, info, installations);
        }

        return 0;
    }

    private static DotnetupInfo GetDotnetupInfo()
    {
        return new DotnetupInfo
        {
            Version = BuildInfo.Version,
            Commit = BuildInfo.CommitSha,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            Rid = RuntimeInformation.RuntimeIdentifier
        };
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
