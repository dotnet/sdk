// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Configuration;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.Extensions.Configuration.DotnetCli.Services;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Package.Add;

public static class PackageAddCommandParser
{
    public static readonly Option<bool> PrereleaseOption = new ForwardedOption<bool>("--prerelease")
    {
        Description = CliStrings.CommandPrereleaseOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--prerelease");

    public static readonly Argument<PackageIdentityWithRange> CmdPackageArgument = CommonArguments.RequiredPackageIdentityArgument()
    .AddCompletions((context) =>
    {
        // we should take --prerelease flags into account for version completion
        var allowPrerelease = context.ParseResult.GetValue(PrereleaseOption);
        return QueryNuGet(context.WordToComplete, allowPrerelease, CancellationToken.None).Result.Select(packageId => new CompletionItem(packageId));
    });

    public static readonly Option<string> VersionOption = new DynamicForwardedOption<string>("--version", "-v")
    {
        Description = CliCommandStrings.CmdVersionDescription,
        HelpName = CliCommandStrings.CmdVersion
    }.ForwardAsSingle(o => $"--version {o}")
        .AddCompletions((context) =>
        {
            // we can only do version completion if we have a package id
            if (context.ParseResult.GetValue(CmdPackageArgument) is { HasVersion: false } packageId)
            {
                // we should take --prerelease flags into account for version completion
                var allowPrerelease = context.ParseResult.GetValue(PrereleaseOption);
                return QueryVersionsForPackage(packageId.Id, context.WordToComplete, allowPrerelease, CancellationToken.None)
                    .Result
                    .Select(version => new CompletionItem(version.ToNormalizedString()));
            }
            else
            {
                return [];
            }
        });

    public static readonly Option<string> FrameworkOption = new ForwardedOption<string>("--framework", "-f")
    {
        Description = CliCommandStrings.PackageAddCmdFrameworkDescription,
        HelpName = CliCommandStrings.PackageAddCmdFramework
    }.ForwardAsSingle(o => $"--framework {o}");

    public static readonly Option<bool> NoRestoreOption = new("--no-restore", "-n")
    {
        Description = CliCommandStrings.PackageAddCmdNoRestoreDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> SourceOption = new ForwardedOption<string>("--source", "-s")
    {
        Description = CliCommandStrings.PackageAddCmdSourceDescription,
        HelpName = CliCommandStrings.PackageAddCmdSource
    }.ForwardAsSingle(o => $"--source {o}");

    public static readonly Option<string> PackageDirOption = new ForwardedOption<string>("--package-directory")
    {
        Description = CliCommandStrings.CmdPackageDirectoryDescription,
        HelpName = CliCommandStrings.CmdPackageDirectory
    }.ForwardAsSingle(o => $"--package-directory {o}");

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("add", CliCommandStrings.PackageAddAppFullName);

        VersionOption.Validators.Add(DisallowVersionIfPackageIdentityHasVersionValidator);
        command.Arguments.Add(CmdPackageArgument);
        command.Options.Add(VersionOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(SourceOption);
        command.Options.Add(PackageDirOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(PrereleaseOption);
        command.Options.Add(PackageCommandParser.ProjectOption);
        command.Options.Add(PackageCommandParser.FileOption);

        command.SetAction((parseResult) => new PackageAddCommand(parseResult).Execute());

        return command;
    }

    private static void DisallowVersionIfPackageIdentityHasVersionValidator(OptionResult result)
    {
        if (result.Parent?.GetValue(CmdPackageArgument).HasVersion == true)
        {
            result.AddError(CliCommandStrings.ValidationFailedDuplicateVersion);
        }
    }

    public static async Task<IEnumerable<string>> QueryNuGet(string packageStem, bool allowPrerelease, CancellationToken cancellationToken)
    {
        try
        {
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(packageInstallDir: new DirectoryPath(), configurationService: DotNetConfigurationFactory.Create());
            var versions = await downloader.GetPackageIdsAsync(packageStem, allowPrerelease, cancellationToken: cancellationToken);
            return versions;
        }
        catch (Exception)
        {
            return [];
        }
    }

    internal static async Task<IEnumerable<NuGetVersion>> QueryVersionsForPackage(string packageId, string versionFragment, bool allowPrerelease, CancellationToken cancellationToken)
    {
        try
        {
            var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(packageInstallDir: new DirectoryPath(), configurationService: DotNetConfigurationFactory.Create());
            var versions = await downloader.GetPackageVersionsAsync(new(packageId), versionFragment, allowPrerelease, cancellationToken: cancellationToken);
            return versions;
        }
        catch (Exception)
        {
            return [];
        }
    }
}
