// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.TemplateSearch.TemplateDiscovery.NuGet;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.Results;
using Microsoft.TemplateSearch.TemplateDiscovery.Test;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal class TemplateDiscoveryCommand : Command
    {
        private const int DefaultPageSize = 100;

        private readonly Option<DirectoryInfo> _basePathOption = new Option<DirectoryInfo>("--basePath")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "The root dir for output for this run.",
            IsRequired = true
        };

        private readonly Option<bool> _allowPreviewPacksOption = new Option<bool>("--allowPreviewPacks")
        {
            Description = "Include preview packs in the results (by default, preview packs are ignored and the latest stable pack is used.",
        };

        private readonly Option<int> _pageSizeOption = new Option<int>("--pageSize", defaultValueFactory: () => DefaultPageSize)
        {
            Description = "(debugging) The chunk size for interactions with the source.",
        };

        private readonly Option<bool> _onePageOption = new Option<bool>("--onePage")
        {
            Description = "(debugging) Only process one page of template packs.",
        };

        private readonly Option<bool> _savePacksOption = new Option<bool>("--savePacks")
        {
            Description = "Don't delete downloaded candidate packs (by default, they're deleted at the end of a run).",
        };

        private readonly Option<bool> _noTemplateJsonFilterOption = new Option<bool>("--noTemplateJsonFilter")
        {
            Description = "Don't prefilter packs that don't contain any template.json files (this filter is applied by default).",
        };

        private readonly Option<bool> _verboseOption = new Option<bool>(new[] { "-v", "--verbose" })
        {
            Description = "Verbose output for template processing.",
        };

        private readonly Option<bool> _testOption = new Option<bool>(new[] { "-t", "--test" })
        {
            Description = "Run tests on generated metadata files.",
        };

        private readonly Option<SupportedQueries[]> _queriesOption = new Option<SupportedQueries[]>("--queries")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = $"The list of providers to run. Supported providers: {string.Join(",", Enum.GetValues<SupportedQueries>())}.",
            AllowMultipleArgumentsPerToken = true,
        };

        private readonly Option<DirectoryInfo> _packagesPathOption = new Option<DirectoryInfo>("--packagesPath")
        {
            Description = $"Path to pre-downloaded packages. If specified, the packages won't be downloaded from NuGet.org.",
        }.AcceptExistingOnly();

        private readonly Option<bool> _diffOption = new Option<bool>("--diff", defaultValueFactory: () => true)
        {
            Description = $"The list of packages will be compared with previous run, and if package version is not changed, the package won't be rescanned.",
        };

        private readonly Option<FileInfo> _diffOverrideCacheOption = new Option<FileInfo>("--diff-override-cache")
        {
            Description = $"Location of current search cache (local path only).",
        }.AcceptExistingOnly();

        private readonly Option<FileInfo> _diffOverrideNonPackagesOption = new Option<FileInfo>("--diff-override-non-packages")
        {
            Description = $"Location of the list of packages known not to be a valid package (local path only).",
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
            this.SetHandler(ExecuteAsync, new CommandArgsBinder(this));
        }

        private static async Task<int> ExecuteAsync(CommandArgs config)
        {
            Verbose.IsEnabled = config.Verbose;
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            try
            {
                IPackCheckerFactory factory = config.LocalPackagePath == null ? new NuGetPackSourceCheckerFactory() : new TestPackCheckerFactory();
                PackSourceChecker packSourceChecker = await factory.CreatePackSourceCheckerAsync(config, cts.Token).ConfigureAwait(false);
                PackSourceCheckResult checkResults = await packSourceChecker.CheckPackagesAsync(cts.Token).ConfigureAwait(false);
                (string metadataPath, string legacyMetadataPath) = PackCheckResultReportWriter.WriteResults(config.OutputPath, checkResults);
                if (config.TestEnabled)
                {
                    CacheFileTests.RunTests(metadataPath, legacyMetadataPath);
                }
                return 0;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Operation was cancelled.");
                return 2;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured: {e}");
                return 1;
            }

        }

        private class CommandArgsBinder : BinderBase<CommandArgs>
        {
            private readonly TemplateDiscoveryCommand _command;

            internal CommandArgsBinder(TemplateDiscoveryCommand command)
            {
                _command = command;
            }

            protected override CommandArgs GetBoundValue(BindingContext bindingContext)
            {
                return new CommandArgs(bindingContext.ParseResult.GetValue(_command._basePathOption) ?? throw new Exception("Output path is not set"))
                {
                    LocalPackagePath = bindingContext.ParseResult.GetValue(_command._packagesPathOption),
                    PageSize = bindingContext.ParseResult.GetValue(_command._pageSizeOption),
                    SaveCandidatePacks = bindingContext.ParseResult.GetValue(_command._savePacksOption),
                    RunOnlyOnePage = bindingContext.ParseResult.GetValue(_command._onePageOption),
                    IncludePreviewPacks = bindingContext.ParseResult.GetValue(_command._allowPreviewPacksOption),
                    DontFilterOnTemplateJson = bindingContext.ParseResult.GetValue(_command._noTemplateJsonFilterOption),
                    Verbose = bindingContext.ParseResult.GetValue(_command._verboseOption),
                    TestEnabled = bindingContext.ParseResult.GetValue(_command._testOption),
                    Queries = bindingContext.ParseResult.GetValue(_command._queriesOption) ?? Array.Empty<SupportedQueries>(),
                    DiffMode = bindingContext.ParseResult.GetValue(_command._diffOption),
                    DiffOverrideSearchCacheLocation = bindingContext.ParseResult.GetValue(_command._diffOverrideCacheOption),
                    DiffOverrideKnownPackagesLocation = bindingContext.ParseResult.GetValue(_command._diffOverrideNonPackagesOption),
                };
            }
        }

    }
}
