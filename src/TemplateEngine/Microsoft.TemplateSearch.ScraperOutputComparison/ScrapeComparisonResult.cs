// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateSearch.ScraperOutputComparison
{
    internal class ScrapeComparisonResult
    {
        public ScrapeComparisonResult(string firstScrapeFile, string secondScrapeFile, List<string> packsInFirstScrapeOnly, List<string> packsInSecondScrapeOnly)
        {
            FirstScrapeFile = firstScrapeFile;
            SecondScrapeFile = secondScrapeFile;
            PacksInFirstScrapeOnly = packsInFirstScrapeOnly;
            PacksInSecondScrapeOnly = packsInSecondScrapeOnly;
        }

        public string FirstScrapeFile { get; }

        public string SecondScrapeFile { get; }

        public List<string> PacksInFirstScrapeOnly { get; }

        public List<string> PacksInSecondScrapeOnly { get; }
    }
}
