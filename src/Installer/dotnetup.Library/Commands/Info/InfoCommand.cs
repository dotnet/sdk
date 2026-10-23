// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal class InfoCommand : CommandBase
{
    private readonly OutputFormat _format;
    private readonly bool _noList;
    private readonly TextWriter _output;

    /// <summary>
    /// Constructor for use with the command-line parser.
    /// </summary>
    public InfoCommand(ParseResult parseResult) : base(parseResult, "info")
    {
        _format = parseResult.GetValue(InfoCommandParser.FormatOption);
        _noList = parseResult.GetValue(InfoCommandParser.NoListOption);
        _output = Console.Out;
    }

    /// <summary>
    /// Constructor for testing with explicit parameters.
    /// </summary>
    public InfoCommand(ParseResult parseResult, OutputFormat format, bool noList, TextWriter output) : base(parseResult, "info")
    {
        _format = format;
        _noList = noList;
        _output = output;
    }

    /// <summary>
    /// Static helper for tests to execute the command without needing a ParseResult.
    /// </summary>
    public static int Execute(OutputFormat format, bool noList, TextWriter output)
    {
        var parseResult = Parser.Parse(new[] { "--info" });
        var command = new InfoCommand(parseResult, format, noList, output);
        return command.Execute();
    }

    internal override AdminModeSupport GetAdminModeSupport(Command command) => AdminModeSupport.AnySymbol;

    protected override void ExecuteCore()
    {
        var info = GetDotnetupInfo();
        ListData? listData = null;
        string? installationsMessage = null;

        if (!_noList)
        {
            if (AccessModeResolver.Resolve().IsAdmin)
            {
                installationsMessage = Strings.AdminModeInfoInstallationsNotShown;
            }
            else
            {
                // --info verifies by default
                listData = InstallationLister.GetListData(verify: true);
            }
        }

        if (_format == OutputFormat.Json)
        {
            PrintJsonInfo(_output, info, listData, installationsMessage);
        }
        else
        {
            PrintHumanReadableInfo(_output, info, listData, installationsMessage);
        }
    }

    private static DotnetupInfo GetDotnetupInfo()
    {
        DotnetupConfigData? config = DotnetupConfig.Read();
        return new DotnetupInfo
        {
            Version = BuildInfo.Version,
            Commit = BuildInfo.CommitSha,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            Rid = RuntimeInformation.RuntimeIdentifier,
            AccessMode = config?.AccessMode.ToString().ToLowerInvariant(),
            DotnetupOnPath = config?.DotnetupOnPath,
        };
    }

    private static void PrintHumanReadableInfo(TextWriter output, DotnetupInfo info, ListData? listData, string? installationsMessage)
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
        grid.AddRow("Access mode:", info.AccessMode ?? "not configured");
        grid.AddRow("dotnetup on PATH:", info.DotnetupOnPath?.ToString().ToLowerInvariant() ?? "not configured");

        console.Write(grid);

        if (listData is not null)
        {
            InstallationLister.WriteHumanReadable(output, listData);
        }
        else if (installationsMessage is not null)
        {
            output.WriteLine();
            output.WriteLine(installationsMessage);
        }
    }

    private static void PrintJsonInfo(TextWriter output, DotnetupInfo info, ListData? listData, string? installationsMessage)
    {
        var fullInfo = new DotnetupFullInfo
        {
            Version = info.Version,
            Commit = info.Commit,
            Architecture = info.Architecture,
            Rid = info.Rid,
            AccessMode = info.AccessMode,
            DotnetupOnPath = info.DotnetupOnPath,
            InstallationsMessage = installationsMessage,
            InstallSpecs = listData?.InstallSpecs,
            Installations = listData?.Installations
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
    public string? AccessMode { get; set; }
    public bool? DotnetupOnPath { get; set; }
}

internal class DotnetupFullInfo
{
    public string Version { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Rid { get; set; } = string.Empty;
    public string? AccessMode { get; set; }
    public bool? DotnetupOnPath { get; set; }
    public string? InstallationsMessage { get; set; }
    public List<InstallSpecInfo>? InstallSpecs { get; set; }
    public List<InstallationInfo>? Installations { get; set; }
}

// Note: DotnetupInfo is not serialized directly (only DotnetupFullInfo is),
// so we don't need it in the JSON context
[JsonSerializable(typeof(DotnetupFullInfo))]
[JsonSerializable(typeof(InstallSpecInfo))]
[JsonSerializable(typeof(InstallationInfo))]
[JsonSerializable(typeof(List<InstallSpecInfo>))]
[JsonSerializable(typeof(List<InstallationInfo>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
internal partial class DotnetupInfoJsonContext : JsonSerializerContext
{
}
