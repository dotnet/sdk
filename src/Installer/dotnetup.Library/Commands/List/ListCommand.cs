// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

internal class ListCommand : CommandBase
{
    private readonly OutputFormat _format;
    private readonly bool _skipVerification;
    private readonly string? _manifestPath;
    private readonly string? _installPath;

    public ListCommand(ParseResult parseResult) : base(parseResult, "list")
    {
        _format = parseResult.GetValue(CommonOptions.FormatOption);
        _skipVerification = parseResult.GetValue(ListCommandParser.NoVerifyOption);
        _manifestPath = parseResult.GetValue(CommonOptions.ManifestPathOption);
        _installPath = parseResult.GetValue(CommonOptions.InstallPathOption);
    }

    protected override void ExecuteCore()
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

        var allData = roots.Select(r => CollectRootData(r, verify, validator)).ToList();

        return new ListData
        {
            InstallSpecs = [.. allData.SelectMany(d => d.Specs)],
            Installations = [.. allData.SelectMany(d => d.Installations)]
        };
    }

    private static (List<InstallSpecInfo> Specs, List<InstallationInfo> Installations) CollectRootData(
        DotnetRootEntry root,
        bool verify,
        ArchiveInstallationValidator validator)
    {
        var installRoot = new DotnetInstallRoot(root.Path, root.Architecture);

        var specs = root.InstallSpecs.Select(spec => new InstallSpecInfo
        {
            Component = spec.Component,
            VersionOrChannel = spec.VersionOrChannel,
            Source = spec.InstallSource,
            GlobalJsonPath = spec.GlobalJsonPath,
            InstallRoot = root.Path,
            Architecture = root.Architecture
        }).ToList();

        var installations = new List<InstallationInfo>();
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

        return (specs, installations);
    }

    public static void WriteHumanReadable(TextWriter writer, ListData listData)
    {
        InstallationListRenderer.Write(writer, listData);
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
