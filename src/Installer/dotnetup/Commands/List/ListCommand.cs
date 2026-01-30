// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

internal class ListCommand : CommandBase
{
    private readonly bool _jsonOutput;
    private readonly bool _skipVerification;

    public ListCommand(ParseResult parseResult) : base(parseResult)
    {
        _jsonOutput = parseResult.GetValue(CommonOptions.JsonOption);
        _skipVerification = parseResult.GetValue(ListCommandParser.NoVerifyOption);
    }

    protected override string GetCommandName() => "list";

    protected override int ExecuteCore()
    {
        var installations = InstallationLister.GetInstallations(verify: !_skipVerification);

        if (_jsonOutput)
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
        catch (Exception ex) when (ex is InvalidOperationException || ex is IOException)
        {
            // Manifest doesn't exist or is inaccessible - return empty list
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

            foreach (var group in grouped)
            {
                writer.WriteLine($"  {group.Key}");
                foreach (var install in group.OrderBy(i => i.Component).ThenBy(i => i.Version))
                {
                    writer.WriteLine($"    {install.ComponentDisplayName,-28} {install.Version,-20} ({install.Architecture})");
                }
                writer.WriteLine();
            }
        }

        writer.WriteLine($"{Strings.ListTotal}: {installations.Count}");
    }

    public static void WriteJson(TextWriter writer, List<InstallationInfo> installations)
    {
        var output = new InstallationListOutput
        {
            Installations = installations,
            Total = installations.Count
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
    /// Gets the display name for this component as shown by dotnet --list-runtimes (e.g., "Microsoft.NETCore.App").
    /// Not serialized to JSON.
    /// </summary>
    [JsonIgnore]
    public string ComponentDisplayName => ParseComponent().GetDisplayName();

    private InstallComponent ParseComponent() =>
        Enum.TryParse<InstallComponent>(Component, ignoreCase: true, out var result) ? result : InstallComponent.SDK;
}

internal class InstallationListOutput
{
    public List<InstallationInfo> Installations { get; set; } = [];
    public int Total { get; set; }
}

[JsonSerializable(typeof(InstallationListOutput))]
[JsonSerializable(typeof(InstallationInfo))]
[JsonSerializable(typeof(List<InstallationInfo>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class InstallationListJsonContext : JsonSerializerContext
{
}
