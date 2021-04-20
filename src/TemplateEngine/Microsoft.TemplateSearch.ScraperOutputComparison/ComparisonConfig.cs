// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateSearch.ScraperOutputComparison
{
    internal class ComparisonConfig
    {
        public ComparisonConfig(string scraperOutputOneFile, string scraperOutputTwoFile, string comparisonResultFile)
        {
            ScraperOutputOneFile = scraperOutputOneFile;
            ScraperOutputTwoFile = scraperOutputTwoFile;
            ComparisonResultFile = comparisonResultFile;
        }

        public string ScraperOutputOneFile { get; }
        public string ScraperOutputTwoFile { get; }
        public string ComparisonResultFile { get; }
    }
}
