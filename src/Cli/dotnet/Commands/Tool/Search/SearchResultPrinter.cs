// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal class SearchResultPrinter(IReporter reporter, IReporter? errorReporter = null)
{
    private readonly IReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    private readonly IReporter _errorReporter = errorReporter ?? Reporter.Error;

    public void PrintSourceHeading(PackageSource source)
    {
        _reporter.WriteLine($"{CliCommandStrings.SourceArgumentName}: {source.Source}".Bold());
    }

    public void PrintSourceFailure(PackageSource source, string message)
    {
        _errorReporter.WriteLine($"{CliCommandStrings.SourceArgumentName}: {source.Source}".Bold());
        _errorReporter.WriteLine(message);
    }

    public void PrintInvalidSource(string invalidSource)
    {
        _errorReporter.WriteLine(string.Format(CliStrings.FailedToLoadNuGetSource, invalidSource));
    }

    public void PrintNoSourcesConfigured()
    {
        _errorReporter.WriteLine(CliCommandStrings.ToolSearchNoSourcesConfigured);
    }

    public void Print(bool isDetailed, IReadOnlyCollection<SearchResultPackage> searchResultPackages)
    {
        if (searchResultPackages.Count == 0)
        {
            _reporter.WriteLine(CliCommandStrings.NoResult);
            return;
        }

        if (!isDetailed)
        {
            var table = new PrintableTable<SearchResultPackage>();
            table.AddColumn(
                CliCommandStrings.PackageId,
                p => p.Id.ToString());
            table.AddColumn(
                CliCommandStrings.LatestVersion,
                p => p.LatestVersion);
            table.AddColumn(
                CliCommandStrings.Authors,
                p => string.Join(", ", p.Authors));
            table.AddColumn(
                CliCommandStrings.Downloads,
                p => p.TotalDownloads.ToString());
            table.AddColumn(
                CliCommandStrings.Verified,
                p => p.Verified ? "x" : "");

            table.PrintRows(searchResultPackages, l => _reporter.WriteLine(l));
        }
        else
        {
            foreach (var p in searchResultPackages)
            {
                _reporter.WriteLine("----------------".Bold());
                _reporter.WriteLine(p.Id.ToString());
                _reporter.WriteLine($"{CliCommandStrings.LatestVersion}: ".Bold() + p.LatestVersion);
                if (p.Authors.Count != 0)
                {
                    _reporter.WriteLine($"{CliCommandStrings.Authors}: ".Bold() + string.Join(", ", p.Authors));
                }

                if (p.Tags.Count != 0)
                {
                    _reporter.WriteLine($"{CliCommandStrings.Tags}: ".Bold() + string.Join(", ", p.Tags));
                }

                _reporter.WriteLine($"{CliCommandStrings.Downloads}: ".Bold() + p.TotalDownloads);


                _reporter.WriteLine($"{CliCommandStrings.Verified}: ".Bold() + p.Verified.ToString());

                if (!string.IsNullOrWhiteSpace(p.Summary))
                {
                    _reporter.WriteLine($"{CliCommandStrings.Summary}: ".Bold() + p.Summary);
                }

                if (!string.IsNullOrWhiteSpace(p.Description))
                {
                    _reporter.WriteLine($"{CliCommandStrings.Description}: ".Bold() + p.Description);
                }

                if (p.Versions.Count != 0)
                {
                    _reporter.WriteLine($"{CliCommandStrings.Versions}: ".Bold());
                    foreach (SearchResultPackageVersion version in p.Versions)
                    {
                        _reporter.WriteLine(
                            $"\t{version.Version}" + $" {CliCommandStrings.Downloads}: ".Bold() + version.Downloads);
                    }
                }

                _reporter.WriteLine();
            }
        }
    }
}
