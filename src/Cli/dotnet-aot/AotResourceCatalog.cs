// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Cli;

internal enum AotResourceMode
{
    Embedded,
    ExternalLocalized,
    ExternalAll,
}

internal static class AotResourceModeConfiguration
{
#if DOTNET_AOT_EXTERNAL_ALL
    internal static AotResourceMode Current => AotResourceMode.ExternalAll;
#elif DOTNET_AOT_EXTERNAL_LOCALIZED
    internal static AotResourceMode Current => AotResourceMode.ExternalLocalized;
#else
    internal static AotResourceMode Current => AotResourceMode.Embedded;
#endif
}

internal readonly record struct AotResourceDescriptor(
    string BaseName,
    string ManagedAssemblyName,
    Assembly GeneratedOwnerAssembly);

internal static class AotResourceCatalog
{
    internal static IReadOnlyList<AotResourceDescriptor> All { get; } =
    [
        new("Microsoft.DotNet.Cli.CliStrings", "dotnet", typeof(AotResourceCatalog).Assembly),
        new("Microsoft.DotNet.Cli.Commands.CliCommandStrings", "dotnet", typeof(AotResourceCatalog).Assembly),
        new("Microsoft.NET.Build.Tasks.Strings", "dotnet", typeof(AotResourceCatalog).Assembly),
        new(
            "Microsoft.DotNet.Cli.CommandDefinitionStrings",
            "Microsoft.DotNet.Cli.Definitions",
            typeof(global::Microsoft.DotNet.Cli.Commands.DotNetCommandDefinition).Assembly),
        new(
            "Microsoft.DotNet.Cli.Utils.LocalizableStrings",
            "Microsoft.DotNet.Cli.Utils",
            typeof(global::Microsoft.DotNet.Cli.Utils.Command).Assembly),
        new(
            "Microsoft.DotNet.Configurer.LocalizableStrings",
            "Microsoft.DotNet.Configurer",
            typeof(global::Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer).Assembly),
        new(
            "Microsoft.DotNet.ProjectTools.Resources",
            "Microsoft.DotNet.ProjectTools",
            typeof(global::Microsoft.DotNet.ProjectTools.LaunchSettings).Assembly),
        new(
            "Microsoft.DotNet.FileBasedPrograms.FileBasedProgramsResources",
            "Microsoft.DotNet.ProjectTools",
            typeof(global::Microsoft.DotNet.ProjectTools.LaunchSettings).Assembly),
        new(
            "Microsoft.NET.Sdk.Localization.Strings",
            "Microsoft.NET.Sdk.WorkloadManifestReader",
            typeof(global::Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadManifestReader).Assembly),
        new(
            "System.CommandLine.StaticCompletions.Resources.Strings",
            "System.CommandLine.StaticCompletions",
            typeof(global::System.CommandLine.StaticCompletions.CompletionsCommandDefinition).Assembly),
        new(
            "System.CommandLine.Properties.Resources",
            "System.CommandLine",
            typeof(global::System.CommandLine.Symbol).Assembly),
    ];

    internal static bool TryGet(string baseName, out AotResourceDescriptor descriptor)
    {
        foreach (AotResourceDescriptor candidate in All)
        {
            if (string.Equals(candidate.BaseName, baseName, StringComparison.Ordinal))
            {
                descriptor = candidate;
                return true;
            }
        }

        descriptor = default;
        return false;
    }
}
