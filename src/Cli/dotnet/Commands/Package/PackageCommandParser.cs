// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;
using Microsoft.DotNet.Cli.Commands.Package.List;
using Microsoft.DotNet.Cli.Commands.Package.Remove;
using Microsoft.DotNet.Cli.Commands.Package.Search;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Package;

internal sealed class PackageCommandParser
{
    public static void ConfigureCommand(PackageCommandDefinition command)
    {
        command.SetAction((parseResult) => parseResult.HandleMissingCommand());

        command.RemoveCommand.SetAction(parseResult => new PackageRemoveCommand(parseResult).Execute());
        command.ListCommand.SetAction(parseResult => new PackageListCommand(parseResult).Execute());
        ConfigureAddCommand(command.AddCommand);

        command.SearchCommand.SetAction(parseResult =>
        {
            var command = new PackageSearchCommand(parseResult);
            int exitCode = command.Execute();

            if (exitCode == 1)
            {
                parseResult.ShowHelp();
            }
            // Only return 1 or 0
            return exitCode == 0 ? 0 : 1;
        });
    }

    internal static void ConfigureAddCommand(PackageAddCommandDefinitionBase def)
    {
        def.PackageIdArgument.CompletionSources.Add(context =>
        {
            // we should take --prerelease flags into account for version completion
            var allowPrerelease = context.ParseResult.GetValue(def.PrereleaseOption);
            return QueryNuGet(context.WordToComplete, allowPrerelease, CancellationToken.None).Result.Select(packageId => new CompletionItem(packageId));
        });

        def.VersionOption.CompletionSources.Add(context =>
        {
            // we can only do version completion if we have a package id
            if (context.ParseResult.GetValue(def.PackageIdArgument) is { HasVersion: false } packageId)
            {
                // we should take --prerelease flags into account for version completion
                var allowPrerelease = context.ParseResult.GetValue(def.PrereleaseOption);
                return QueryVersionsForPackage(packageId.Id, context.WordToComplete, allowPrerelease, CancellationToken.None)
                    .Result
                    .Select(version => new CompletionItem(version.ToNormalizedString()));
            }
            else
            {
                return [];
            }
        });

        def.SetAction(parseResult => new PackageAddCommand(parseResult).Execute());
    }

    private static async Task<IEnumerable<string>> QueryNuGet(string packageStem, bool allowPrerelease, CancellationToken cancellationToken)
    {
        try
        {
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(packageInstallDir: new DirectoryPath());
            var versions = await downloader.GetPackageIdsAsync(packageStem, allowPrerelease, cancellationToken: cancellationToken);
            return versions;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<IEnumerable<NuGetVersion>> QueryVersionsForPackage(string packageId, string versionFragment, bool allowPrerelease, CancellationToken cancellationToken)
    {
        try
        {
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(packageInstallDir: new DirectoryPath());
            var versions = await downloader.GetPackageVersionsAsync(new(packageId), versionFragment, allowPrerelease, cancellationToken: cancellationToken);
            return versions;
        }
        catch (Exception)
        {
            return [];
        }
    }

    internal static (string Path, AppKinds AllowedAppKinds) ProcessPathOptions(Option<string?> fileOption, Option<string?> projectOption, Argument<string>? projectOrFileArgument, ParseResult parseResult)
    {
        bool hasFileOption = parseResult.HasOption(fileOption);
        bool hasProjectOption = parseResult.HasOption(projectOption);

        return (hasFileOption, hasProjectOption) switch
        {
            (false, false) => projectOrFileArgument != null && parseResult.GetValue(projectOrFileArgument) is { } projectOrFile
                ? (projectOrFile, AppKinds.Any)
                : (Environment.CurrentDirectory, AppKinds.ProjectBased),
            (true, false) => (parseResult.GetValue(fileOption)!, AppKinds.FileBased),
            (false, true) => (parseResult.GetValue(projectOption)!, AppKinds.ProjectBased),
            (true, true) => throw new Utils.GracefulException(CliCommandStrings.CannotCombineOptions, fileOption.Name, projectOption.Name),
        };
    }
}
