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
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Common;
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
            return QueryNuGet(context, def, allowPrerelease, CancellationToken.None).Result.Select(packageId => new CompletionItem(packageId));
        });

        def.VersionOption.CompletionSources.Add(context =>
        {
            // we can only do version completion if we have a package id
            if (context.ParseResult.GetValue(def.PackageIdArgument) is { HasVersion: false } packageId)
            {
                // we should take --prerelease flags into account for version completion
                var allowPrerelease = context.ParseResult.GetValue(def.PrereleaseOption);
                return QueryVersionsForPackage(context, def, packageId.Id, allowPrerelease, CancellationToken.None)
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

    private static async Task<IEnumerable<string>> QueryNuGet(CompletionContext context, PackageAddCommandDefinitionBase definition, bool allowPrerelease, CancellationToken cancellationToken)
    {
        try
        {
            PackageSourceLocation location = GetCompletionSourceLocation(context.ParseResult, definition);
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(
                packageInstallDir: new DirectoryPath(), verboseLogger: NullLogger.Instance);
            return await downloader.GetPackageIdsAsync(context.WordToComplete, allowPrerelease, location, cancellationToken);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<IEnumerable<NuGetVersion>> QueryVersionsForPackage(CompletionContext context, PackageAddCommandDefinitionBase definition, string packageId, bool allowPrerelease, CancellationToken cancellationToken)
    {
        try
        {
            PackageSourceLocation location = GetCompletionSourceLocation(context.ParseResult, definition);
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(
                packageInstallDir: new DirectoryPath(), verboseLogger: NullLogger.Instance);
            return await downloader.GetPackageVersionsAsync(new(packageId), context.WordToComplete, allowPrerelease, location, cancellationToken);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static PackageSourceLocation GetCompletionSourceLocation(ParseResult parseResult, PackageAddCommandDefinitionBase definition)
    {
        string invocationDirectory = Directory.GetCurrentDirectory();
        var (target, _) = ProcessPathOptions(definition.FileOption, definition.ProjectOption, definition.GetProjectOrFileArgument(), parseResult);
        string targetPath = Path.GetFullPath(target, invocationDirectory);
        string rootDirectory;
        if (Directory.Exists(targetPath))
        {
            rootDirectory = targetPath;
        }
        else if (File.Exists(targetPath))
        {
            rootDirectory = Path.GetDirectoryName(targetPath)!;
        }
        else
        {
            throw new FileNotFoundException(null, targetPath);
        }

        string? source = parseResult.GetValue(definition.SourceOption);
        return new PackageSourceLocation(
            rootConfigDirectory: new DirectoryPath(rootDirectory),
            sourceFeedOverrides: source is null ? [] : [source],
            basePath: invocationDirectory);
    }

    internal static (string Path, AppKinds AllowedAppKinds) ProcessPathOptions(Option<string?>? fileOption, Option<string?>? projectOption, Argument<string>? projectOrFileArgument, ParseResult parseResult)
    {
        bool hasFileOption = fileOption is not null && parseResult.HasOption(fileOption);
        bool hasProjectOption = projectOption is not null && parseResult.HasOption(projectOption);

        return (hasFileOption, hasProjectOption) switch
        {
            (false, false) => projectOrFileArgument != null && parseResult.GetValue(projectOrFileArgument) is { } projectOrFile
                ? (projectOrFile, AppKinds.Any)
                : (Environment.CurrentDirectory, AppKinds.ProjectBased),
            (true, false) => (parseResult.GetValue(fileOption!)!, AppKinds.FileBased),
            (false, true) => (parseResult.GetValue(projectOption!)!, AppKinds.ProjectBased),
            (true, true) => throw new Utils.GracefulException(CliCommandStrings.CannotCombineOptions, fileOption!.Name, projectOption!.Name),
        };
    }
}
