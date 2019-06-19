using System;
using System.Collections.Generic;
using System.Text;

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
