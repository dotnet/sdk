// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal static class InfoCommand
{
    public static int Execute(bool jsonOutput, bool noList = false)
    {
        var info = GetDotnetupInfo();
        List<InstallationInfo>? installations = null;

        if (!noList)
        {
            // --info verifies by default
            installations = InstallationLister.GetInstallations(verify: true);
        }

        if (jsonOutput)
        {
            PrintJsonInfo(info, installations);
        }
        else
        {
            PrintHumanReadableInfo(info, installations);
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
        // Format: "1.0.0+abc123def" or just "1.0.0"
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex > 0)
        {
            var version = informationalVersion.Substring(0, plusIndex);
            var commit = informationalVersion.Substring(plusIndex + 1);
            // Truncate commit to 10 characters for display (standard short SHA)
            if (commit.Length > 10)
            {
                commit = commit.Substring(0, 10);
            }
            return (version, commit);
        }

        return (informationalVersion, "N/A");
    }

    private static void PrintHumanReadableInfo(DotnetupInfo info, List<InstallationInfo>? installations)
    {
        Console.WriteLine(Strings.InfoHeader);
        Console.WriteLine($" Version:      {info.Version}");
        Console.WriteLine($" Commit:       {info.Commit}");
        Console.WriteLine($" Architecture: {info.Architecture}");
        Console.WriteLine($" RID:          {info.Rid}");

        if (installations is not null)
        {
            InstallationLister.WriteHumanReadable(Console.Out, installations);
        }
    }

    private static void PrintJsonInfo(DotnetupInfo info, List<InstallationInfo>? installations)
    {
        var fullInfo = new DotnetupFullInfo
        {
            Version = info.Version,
            Commit = info.Commit,
            Architecture = info.Architecture,
            Rid = info.Rid,
            Installations = installations
        };
        Console.WriteLine(JsonSerializer.Serialize(fullInfo, DotnetupInfoJsonContext.Default.DotnetupFullInfo));
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

[JsonSerializable(typeof(DotnetupInfo))]
[JsonSerializable(typeof(DotnetupFullInfo))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class DotnetupInfoJsonContext : JsonSerializerContext
{
}
