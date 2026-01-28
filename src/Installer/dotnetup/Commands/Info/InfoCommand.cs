// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

internal static class InfoCommand
{
    public static int Execute(bool jsonOutput)
    {
        var info = GetDotnetupInfo();

        if (jsonOutput)
        {
            PrintJsonInfo(info);
        }
        else
        {
            PrintHumanReadableInfo(info);
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

    private static void PrintHumanReadableInfo(DotnetupInfo info)
    {
        Console.WriteLine(Strings.InfoHeader);
        Console.WriteLine($" Version:      {info.Version}");
        Console.WriteLine($" Commit:       {info.Commit}");
        Console.WriteLine($" Architecture: {info.Architecture}");
        Console.WriteLine($" RID:          {info.Rid}");
    }

    private static void PrintJsonInfo(DotnetupInfo info)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        Console.WriteLine(JsonSerializer.Serialize(info, DotnetupInfoJsonContext.Default.DotnetupInfo));
    }
}

internal class DotnetupInfo
{
    public string Version { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Rid { get; set; } = string.Empty;
}

[JsonSerializable(typeof(DotnetupInfo))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class DotnetupInfoJsonContext : JsonSerializerContext
{
}
