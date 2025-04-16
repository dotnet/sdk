// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest;
using NuGet.Packaging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.CommandLine;
using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

/// <summary>
/// Tool for comparing Microsoft builds with VMR (Virtual Mono Repo) builds.
/// Identifies missing assets, misclassified assets, and assembly version mismatches
/// Identifies differences in signing status between assets.
/// </summary>
public class Program
{
    private record ComparerCommand(string Name, string Description, Type ComparerType, List<Option> Options);

    private static Option clean = new Option<bool>("-clean")
    {
        Description = "Clean up each artifact after comparison.",
    };

    private static Option assetType = new Option<AssetType?>("-assetType")
    {
        Description = "Type of asset to compare. If not specified, all asset types will be compared.",
        Required = false
    };

    private static Option vmrManifestPath = new Option<string>("-vmrManifestPath")
    {
        Description = "Path to the manifest file",
        Required = true
    };

    private static Option vmrAssetBasePath = new Option<string>("-vmrAssetBasePath")
    {
        Description = "Path to the VMR asset base path",
        Required = true
    };

    private static Option msftAssetBasePath = new Option<string>("-msftAssetBasePath")
    {
        Description = "Path to the asset base path",
        Required = true
    };

    private static Option issuesReport = new Option<string>("-issuesReport")
    {
        Description = "Path to output xml file for non-baselined issues.",
        Required = true
    };

    private static Option noIssuesReport = new Option<string>("-noIssuesReport") 
    {
        Description = "Path to output xml file for baselined issues and assets without issues.",
        Required = true
    };

    private static Option parallel = new Option<int>("-parallel")
    {
        Description = "Amount of parallelism used while analyzing the builds.",
        DefaultValueFactory = _ => 8,
        Required = false
    };

    private static Option baseline = new Option<string>("-baseline")
    {
        Description = "Path to the baseline build manifest.",
        Required = true
    };

    private static Option exclusions = new Option<string>("-exclusions")
    {
        Description = "Path to the exclusions file.",
        Required = true
    };

    private static Option sdkTaskScript = new Option<string>("-sdkTaskScript")
    {
        Description = "Path to the SDK task script.",
        Required = true
    };

    /// <summary>
    /// Entry point for the build comparison tool.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Return code indicating success (0) or failure (non-zero).</returns>
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Tool for comparing Microsoft builds with VMR builds.");
        var subCommands = new List<ComparerCommand>
        {
            new ComparerCommand(
                "assets",
                "Compares asset manifests and outputs missing or misclassified assets",
                typeof(AssetComparer),
                new List<Option> { clean, assetType, vmrManifestPath, vmrAssetBasePath, msftAssetBasePath, issuesReport, noIssuesReport, parallel, baseline }),
            new ComparerCommand(
                "signing",
                "Compares signing status between builds and outputs assets with differences.",
                typeof(SigningComparer),
                new List<Option> { clean, assetType, vmrManifestPath, vmrAssetBasePath, msftAssetBasePath, issuesReport, noIssuesReport, parallel, baseline, exclusions, sdkTaskScript }),
        };

        foreach (var command in CreateComparerCommands(subCommands))
        {
            rootCommand.Add(command);
        }

        return rootCommand.Parse(args).InvokeAsync().GetAwaiter().GetResult();
    }

    private static IEnumerable<Command> CreateComparerCommands(List<ComparerCommand> commands)
    {
        foreach (var command in commands)
        {
            var subCommand = new Command(command.Name, command.Description);

            foreach (var option in command.Options)
            {
                subCommand.Options.Add(option);
            }

            subCommand.SetAction((result) =>
            {
                var options = new List<object>();
                foreach (var option in command.Options)
                {
                    var value = result.GetValue((dynamic)option);
                    options.Add(value);
                }
                var comparerInstance = (BuildComparer)Activator.CreateInstance(command.ComparerType, options.ToArray());
                return comparerInstance.Compare().GetAwaiter().GetResult();
            });

            yield return subCommand;
        }
    }
}
