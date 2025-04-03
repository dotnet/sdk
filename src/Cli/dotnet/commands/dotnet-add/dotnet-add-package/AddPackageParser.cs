// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.Text.Json;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Add.PackageReference;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Add.PackageReference.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class AddPackageParser
    {
        public static readonly CliArgument<string> CmdPackageArgument = new CliArgument<string>(LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.CmdPackageDescription
        }.AddCompletions((context) =>
        {
            // we should take --prerelease flags into account for version completion
            var allowPrerelease = context.ParseResult.GetValue(PrereleaseOption);
            return QueryNuGet(context.WordToComplete, allowPrerelease, CancellationToken.None).Result.Select(packageId => new CompletionItem(packageId));
        });

        public static readonly CliOption<string> VersionOption = new ForwardedOption<string>("--version", "-v")
        {
            Description = LocalizableStrings.CmdVersionDescription,
            HelpName = LocalizableStrings.CmdVersion
        }.ForwardAsSingle(o => $"--version {o}")
            .AddCompletions((context) =>
            {
                // we can only do version completion if we have a package id
                if (context.ParseResult.GetValue(CmdPackageArgument) is string packageId)
                {
                    // we should take --prerelease flags into account for version completion
                    var allowPrerelease = context.ParseResult.GetValue(PrereleaseOption);
                    return QueryVersionsForPackage(packageId, context.WordToComplete, allowPrerelease, CancellationToken.None)
                        .Result
                        .Select(version => new CompletionItem(version.ToNormalizedString()));
                }
                else
                {
                    return Enumerable.Empty<CompletionItem>();
                }
            });

        public static readonly CliOption<string> FrameworkOption = new ForwardedOption<string>("--framework", "-f")
        {
            Description = LocalizableStrings.CmdFrameworkDescription,
            HelpName = LocalizableStrings.CmdFramework
        }.ForwardAsSingle(o => $"--framework {o}");

        public static readonly CliOption<bool> NoRestoreOption = new("--no-restore", "-n")
        {
            Description = LocalizableStrings.CmdNoRestoreDescription
        };

        public static readonly CliOption<string> SourceOption = new ForwardedOption<string>("--source", "-s")
        {
            Description = LocalizableStrings.CmdSourceDescription,
            HelpName = LocalizableStrings.CmdSource
        }.ForwardAsSingle(o => $"--source {o}");

        public static readonly CliOption<string> PackageDirOption = new ForwardedOption<string>("--package-directory")
        {
            Description = LocalizableStrings.CmdPackageDirectoryDescription,
            HelpName = LocalizableStrings.CmdPackageDirectory
        }.ForwardAsSingle(o => $"--package-directory {o}");

        public static readonly CliOption<bool> InteractiveOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription,
        }.ForwardAs("--interactive");

        public static readonly CliOption<bool> PrereleaseOption = new ForwardedOption<bool>("--prerelease")
        {
            Description = CommonLocalizableStrings.CommandPrereleaseOptionDescription
        }.ForwardAs("--prerelease");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("package", LocalizableStrings.AppFullName);

            command.Arguments.Add(CmdPackageArgument);
            command.Options.Add(VersionOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(NoRestoreOption);
            command.Options.Add(SourceOption);
            command.Options.Add(PackageDirOption);
            command.Options.Add(InteractiveOption);
            command.Options.Add(PrereleaseOption);

            command.SetAction((parseResult) => new AddPackageReferenceCommand(parseResult).Execute());

            return command;
        }

        public static async Task<IEnumerable<string>> QueryNuGet(string packageStem, bool allowPrerelease, CancellationToken cancellationToken)
        {
            try
            {
                var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(packageInstallDir: new DirectoryPath());
                var versions = await downloader.GetPackageIdsAsync(packageStem, allowPrerelease, cancellationToken: cancellationToken);
                return versions;
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
        }

        internal static async Task<IEnumerable<NuGetVersion>> QueryVersionsForPackage(string packageId, string versionFragment, bool allowPrerelease, CancellationToken cancellationToken)
        {
            try
            {
                var downloader = new NuGetPackageDownloader.NuGetPackageDownloader(packageInstallDir: new DirectoryPath());
                var versions = await downloader.GetPackageVersionsAsync(new(packageId), versionFragment, allowPrerelease, cancellationToken: cancellationToken);
                return versions;
            }
            catch (Exception)
            {
                return Enumerable.Empty<NuGetVersion>();
            }
        }
    }
}
