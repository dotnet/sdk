// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateSearch.TemplateDiscovery.Nuget;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking;
using Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting;
using Microsoft.TemplateSearch.TemplateDiscovery.Results;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal class Program
    {
        private const int _defaultPageSize = 100;

        private static async Task Main(string[] args)
        {
            RootCommand rootCommand = CreateCommand();
            rootCommand.Handler = CommandHandler.Create(ExecuteAsync);
            await rootCommand.InvokeAsync(args).ConfigureAwait(false);
        }

        private static async Task<int> ExecuteAsync(CommandArgs args)
        {
            PackSourceChecker packSourceChecker = NuGetPackSourceCheckerFactory.CreatePackSourceChecker(args);

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            try
            {
                PackSourceCheckResult checkResults = await packSourceChecker.CheckPackagesAsync(cts.Token).ConfigureAwait(false);
                PackCheckResultReportWriter.WriteResults(args.OutputPath, checkResults);
                return 0;
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Operation was cancelled.");
                return 1;
            }

        }

        private static RootCommand CreateCommand()
        {
            RootCommand rootCommand = new RootCommand("Generates the template package search cache file based on the packages available on NuGet.org.");
            rootCommand.AddOption(new Option<DirectoryInfo>("--basePath")
            {
                Arity = ArgumentArity.ExactlyOne,
                Description = "The root dir for output for this run.",
                IsRequired = true
            }.LegalFilePathsOnly());

            rootCommand.AddOption(new Option("--allowPreviewPacks")
            {
                Description = "Include preview packs in the results (by default, preview packs are ignored and the latest stable pack is used.",
            });
            rootCommand.AddOption(new Option<int>("--pageSize", getDefaultValue: () => _defaultPageSize)
            {
                Description = "(debugging) The chunk size for interactions with the source.",
            });
            rootCommand.AddOption(new Option("--onePage")
            {
                Description = "(debugging) Only process one page of template packs.",
            });
            rootCommand.AddOption(new Option("--savePacks")
            {
                Description = "Don't delete downloaded candidate packs (by default, they're deleted at the end of a run).",
            });
            rootCommand.AddOption(new Option("--noTemplateJsonFilter")
            {
                Description = "Don't prefilter packs that don't contain any template.json files (this filter is applied by default).",
            });
            rootCommand.AddOption(new Option(new[] { "-v", "--verbose" })
            {
                Description = "Verbose output for template processing.",
            });

            Option<SupportedQueries[]> queriesOption = new Option<SupportedQueries[]>("--queries")
            {
                Arity = ArgumentArity.OneOrMore,
                Description = $"The list of providers to run. Supported providers: {string.Join(",", Enum.GetValues<SupportedQueries>())}.",
                AllowMultipleArgumentsPerToken = true,
            };
            queriesOption.FromAmong(Enum.GetValues<SupportedQueries>().Select(e => e.ToString()).ToArray());
            rootCommand.AddOption(queriesOption);

            rootCommand.AddOption(new Option<DirectoryInfo>("--packagesPath")
            {
                Description = $"Path to pre-downloaded packages. If specified, the packages won't be downloaded from NuGet.org.",
            }.ExistingOnly());

            rootCommand.TreatUnmatchedTokensAsErrors = true;
            return rootCommand;
        }
    }
}
