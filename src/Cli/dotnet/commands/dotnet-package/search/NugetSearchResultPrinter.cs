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

        public NugetSearchResultPrinter(IReporter reporter)
        {
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public void Print(bool exactMatch, string searchArgument, IReadOnlyCollection<SearchResultPackage> searchResultPackages)
        {
            foreach (SearchResultPackage package in searchResultPackages)
            {
                if ((!exactMatch) || (exactMatch && string.Equals(searchArgument, package.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
                {
                    _reporter.WriteLine(">" + package.Id.ToString() + " | " + package.LatestVersion.ToString() + " | Downloads: " + package.TotalDownloads.ToString());
                }
                else
                {
                    // we are doing exact match and the first item is not the exact match we are looking for
                    break;
                }
            }
        }
    }
}
