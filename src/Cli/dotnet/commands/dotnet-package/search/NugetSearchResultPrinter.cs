// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Search;

namespace Microsoft.DotNet.Tools.Package.Search
{

    internal class NugetSearchResultPrinter
    {
        private readonly IReporter _reporter;
        private const string _quietVerbosity = "quiet";
        private const string _normalVerbosity = "normal";
        private const string _detailedVerbosity = "detailed";

        public NugetSearchResultPrinter(IReporter reporter)
        {
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Print(string verbosity, bool exactMatch, string searchArgument, IReadOnlyCollection<SearchResultPackage> searchResultPackages)
        {
            foreach (SearchResultPackage package in searchResultPackages)
            {
                if ((!exactMatch) || (exactMatch && string.Equals(searchArgument, package.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    _reporter.WriteLine("");
                    if (string.Equals(verbosity, _normalVerbosity, StringComparison.OrdinalIgnoreCase))
                    {
                        _reporter.WriteLine(">" + package.Id.ToString() + " | " + package.LatestVersion.ToString() + " | Downloads: " + package.TotalDownloads.ToString());
                        _reporter.WriteLine(package.Description.ToString());
                        _reporter.WriteLine("====================");
                    }
                    else if (string.Equals(verbosity, _detailedVerbosity, StringComparison.OrdinalIgnoreCase))
                    {
                        _reporter.WriteLine(">" + package.Id.ToString() + " | " + package.LatestVersion.ToString() + " | Downloads: " + package.TotalDownloads.ToString());
                        _reporter.WriteLine("--------------------");
                        _reporter.WriteLine("Deprecated: " + !string.IsNullOrEmpty(package.Deprecation) + " | Vulnerable: " + !(package.Vulnerabilities == null || package.Vulnerabilities.Count == 0));
                        _reporter.WriteLine(package.Description.ToString());
                        _reporter.WriteLine("License URL: " + (package.LicenseUrl ?? "N/A"));
                        _reporter.WriteLine("====================");
                    }
                    else
                    {
                        _reporter.WriteLine(">" + package.Id.ToString() + " | " + package.LatestVersion.ToString());
                    }
                }
                else
                {
                    // we are doing exact match and the first item is not the exact match we are looijubg for
                    break;
                }
            }
        }
    }
}
