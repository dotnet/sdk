// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.Commands.Tool.Search;

internal class SearchResultPrinter(IReporter reporter, IReporter errorReporter = null)
{
    private readonly IReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    private readonly IReporter _errorReporter = errorReporter ?? Reporter.Error;

    /// <summary>
    /// Prints a heading identifying which source the following results came from.
    /// </summary>
    public void PrintSourceHeading(PackageSource source)
    {
        _reporter.WriteLine($"{CliCommandStrings.SourceArgumentName}: {source.Source}".Bold());
    }

    /// <summary>
    /// Prints a per-source failure diagnostic to stderr and continues processing the remaining sources.
    /// </summary>
    public void PrintSourceFailure(PackageSource source, string message)
    {
        _errorReporter.WriteLine($"{CliCommandStrings.SourceArgumentName}: {source.Source}".Bold());
        _errorReporter.WriteLine(message);
    }

    /// <summary>
    /// Prints a diagnostic for a source string that could not be parsed/loaded as a NuGet package source.
    /// </summary>
    public void PrintInvalidSource(string invalidSource)
    {
        _errorReporter.WriteLine(string.Format(CliStrings.FailedToLoadNuGetSource, invalidSource));
    }

    /// <summary>
    /// Prints a diagnostic indicating that there are no NuGet package sources to search.
    /// </summary>
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
                p => p.Authors == null ? "" : string.Join(", ", p.Authors));
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
                if (p.Authors != null)
                {
                    _reporter.WriteLine($"{CliCommandStrings.Authors}: ".Bold() + string.Join(", ", p.Authors));
                }

                if (p.Tags != null)
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
