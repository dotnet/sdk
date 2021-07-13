// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateSearch.ScraperOutputComparison
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (!TryParseArgs(args, out ComparisonConfig comparisonConfig))
            {
                DisplayUsage();
                return;
            }

            ScrapeComparer comparer = new ScrapeComparer(comparisonConfig);
            if (!comparer.Compare(out ScrapeComparisonResult comparisonResult))
            {
                Console.WriteLine("Unable to read one or both of the scraper outputs to compare");
                return;
            }

            if (TryWriteComparisonResults(comparisonConfig, comparisonResult))
            {
                Console.WriteLine("Successfully wrote the comparison result file");
            }
            else
            {
                Console.WriteLine("Error writing the comparison file");
            }
        }

        private static bool TryWriteComparisonResults(ComparisonConfig config, ScrapeComparisonResult comparisonResult)
        {
            try
            {
                JObject toSerialize = JObject.FromObject(comparisonResult);

                string outputDirectory = Path.GetDirectoryName(config.ComparisonResultFile);
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                File.WriteAllText(config.ComparisonResultFile, toSerialize.ToString());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseArgs(string[] args, out ComparisonConfig comparisonConfig)
        {
            if (args.Length != 3)
            {
                comparisonConfig = null;
                return false;
            }

            string scraperOutputOnePath = args[0];
            string scraperOutputTwoPath = args[1];
            string comparisonResultFile = args[2];

            if (!File.Exists(scraperOutputOnePath))
            {
                Console.WriteLine($"Scraper output file '{scraperOutputOnePath}' does not exist");
                comparisonConfig = null;
                return false;
            }

            if (!File.Exists(scraperOutputTwoPath))
            {
                Console.WriteLine($"Scraper output file '{scraperOutputTwoPath}' does not exist");
                comparisonConfig = null;
                return false;
            }

            if (File.Exists(comparisonResultFile))
            {
                Console.WriteLine($"Comparison result file '{comparisonResultFile}' already exists. Exiting to avoid overwriting it.");
                comparisonConfig = null;
                return false;
            }

            comparisonConfig = new ComparisonConfig(scraperOutputOnePath, scraperOutputTwoPath, comparisonResultFile);
            return true;
        }

        private static void DisplayUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("Running the comparer requires 3 args. The first two are the paths to the scraper files to compare. The third is the output file for the comparison results.");
        }
    }
}
