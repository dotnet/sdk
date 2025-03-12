// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateSearch.TemplateDiscovery.NuGet;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.Results;
using Microsoft.TemplateSearch.TemplateDiscovery.Test;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal class TemplateDiscoveryCommand : CliCommand
    {
        private const int DefaultPageSize = 100;

        private readonly CliOption<DirectoryInfo> _basePathOption = new("--basePath")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "The root dir for output for this run.",
            Required = true
        };

        private readonly CliOption<bool> _allowPreviewPacksOption = new("--allowPreviewPacks")
        {
            Description = "Include preview packs in the results (by default, preview packs are ignored and the latest stable pack is used.",
        };

        private readonly CliOption<int> _pageSizeOption = new("--pageSize")
        {
            Description = "(debugging) The chunk size for interactions with the source.",
            DefaultValueFactory = (r) => DefaultPageSize,
        };

        private readonly CliOption<bool> _onePageOption = new("--onePage")
        {
            Description = "(debugging) Only process one page of template packs.",
        };

        private readonly CliOption<bool> _savePacksOption = new("--savePacks")
        {
            Description = "Don't delete downloaded candidate packs (by default, they're deleted at the end of a run).",
        };

        private readonly CliOption<bool> _noTemplateJsonFilterOption = new("--noTemplateJsonFilter")
        {
            Description = "Don't prefilter packs that don't contain any template.json files (this filter is applied by default).",
        };

        private readonly CliOption<bool> _verboseOption = new("--verbose", "-v")
        {
            Description = "Verbose output for template processing.",
        };

        private readonly CliOption<bool> _testOption = new("--test", "-t")
        {
            Description = "Run tests on generated metadata files.",
        };

        private readonly CliOption<SupportedQueries[]> _queriesOption = new("--queries")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = $"The list of providers to run. Supported providers: {string.Join(",", Enum.GetValues<SupportedQueries>())}.",
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly CliOption<DirectoryInfo> _packagesPathOption = new CliOption<DirectoryInfo>("--packagesPath")
        {
            Description = "Path to pre-downloaded packages. If specified, the packages won't be downloaded from NuGet.org."
        }.AcceptExistingOnly();

        private readonly CliOption<bool> _diffOption = new("--diff")
        {
            Description = "The list of packages will be compared with previous run, and if package version is not changed, the package won't be rescanned.",
            DefaultValueFactory = (r) => true,
        };

        private readonly CliOption<FileInfo> _diffOverrideCacheOption = new CliOption<FileInfo>("--diff-override-cache")
        {
            Description = "Location of current search cache (local path only).",
        }.AcceptExistingOnly();

        private readonly CliOption<FileInfo> _diffOverrideNonPackagesOption = new CliOption<FileInfo>("--diff-override-non-packages")
        {
            Description = "Location of the list of packages known not to be a valid package (local path only).",
        }.AcceptExistingOnly();

        public TemplateDiscoveryCommand() : base("template-discovery", "Generates the template package search cache file based on the packages available on NuGet.org.")
        {
            _basePathOption.AcceptLegalFilePathsOnly();
            _queriesOption.AcceptOnlyFromAmong(Enum.GetValues<SupportedQueries>().Select(e => e.ToString()).ToArray());

            Options.Add(_basePathOption);
            Options.Add(_allowPreviewPacksOption);
            Options.Add(_pageSizeOption);
            Options.Add(_onePageOption);
            Options.Add(_savePacksOption);
            Options.Add(_noTemplateJsonFilterOption);
            Options.Add(_testOption);
            Options.Add(_queriesOption);
            Options.Add(_packagesPathOption);
            Options.Add(_verboseOption);
            Options.Add(_diffOption);
            Options.Add(_diffOverrideCacheOption);
            Options.Add(_diffOverrideNonPackagesOption);

            TreatUnmatchedTokensAsErrors = true;
            SetAction(async (parseResult, cancellationToken) =>
            {
                var config = new CommandArgs(parseResult.GetValue(_basePathOption) ?? throw new Exception("Output path is not set"))
                {
                    LocalPackagePath = parseResult.GetValue(_packagesPathOption),
                    PageSize = parseResult.GetValue(_pageSizeOption),
                    SaveCandidatePacks = parseResult.GetValue(_savePacksOption),
                    RunOnlyOnePage = parseResult.GetValue(_onePageOption),
                    IncludePreviewPacks = parseResult.GetValue(_allowPreviewPacksOption),
                    DontFilterOnTemplateJson = parseResult.GetValue(_noTemplateJsonFilterOption),
                    Verbose = parseResult.GetValue(_verboseOption),
                    TestEnabled = parseResult.GetValue(_testOption),
                    Queries = parseResult.GetValue(_queriesOption) ?? [],
                    DiffMode = parseResult.GetValue(_diffOption),
                    DiffOverrideSearchCacheLocation = parseResult.GetValue(_diffOverrideCacheOption),
                    DiffOverrideKnownPackagesLocation = parseResult.GetValue(_diffOverrideNonPackagesOption),
                };

                await ExecuteAsync(config, cancellationToken).ConfigureAwait(false);

                return 0;
            });
        }

        private static async Task ExecuteAsync(CommandArgs config, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Verbose.IsEnabled = config.Verbose;
            IPackCheckerFactory factory = config.LocalPackagePath == null ? new NuGetPackSourceCheckerFactory() : new TestPackCheckerFactory();
            PackSourceChecker packSourceChecker = await factory.CreatePackSourceCheckerAsync(config, cancellationToken).ConfigureAwait(false);
            PackSourceCheckResult checkResults = await packSourceChecker.CheckPackagesAsync(cancellationToken).ConfigureAwait(false);
            (string metadataPath, string legacyMetadataPath) = PackCheckResultReportWriter.WriteResults(config.OutputPath, checkResults);
            if (config.TestEnabled)
            {
                CacheFileTests.RunTests(metadataPath, legacyMetadataPath);
            }
        }
    }
}
