using System;
using System.Collections.Generic;
using System.IO;
using BaselineComparer.Helpers;

namespace BaselineComparer
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsageMessage();
                return -1;
            }

            if (string.Equals(args[0], "--create-baseline"))
            {
                if (args.Length != 4)
                {
                    ShowUsageMessage();
                    return -1;
                }

                string dotnetCommand = args[1];
                string creationCommandFile = args[2];
                string outputBaseDir = args[3];

                return CreateFileSeparatedBaseline(dotnetCommand, creationCommandFile, outputBaseDir);
            }

            if (string.Equals(args[0], "--compare-to-baseline"))
            {
                if (args.Length != 3 && args.Length != 4)
                {
                    ShowUsageMessage();
                    return -1;
                }

                string baselineBaseDir = args[1];
                string comparisonBaseDir = args[2];

                bool debug = false;
                if (args.Length == 4)
                {
                    if (string.Equals(args[3], "--debug", StringComparison.Ordinal))
                    {
                        debug = true;
                        Console.WriteLine("Attach & hit return.");
                        Console.ReadLine();
                    }
                    else
                    {
                        ShowUsageMessage();
                        return -1;
                    }
                }

                return CreateFileSeparatedBaselineComparison(baselineBaseDir, comparisonBaseDir, debug);
            }

            ShowUsageMessage();
            return -1;
        }

        private static void ShowUsageMessage()
        {
            Console.WriteLine("Invalid args. Valid usage patterns:");
            Console.WriteLine("Generates 2 sets of templates using the creation commands, then compares them and writes a baseline report:");
            Console.WriteLine("  --create-baseline <Dotnet command {new|new3}> <creation command file> <output base dir>.");
            Console.WriteLine();

            Console.WriteLine("Reads the baseline report, generates a set of templates using the commands in the baseline, compares to the baseline master data, then compares the comparison to the baseline comparison:");
            Console.WriteLine("  --compare-to-baseline <baseline base dir> <comparison base dir>.");
            Console.WriteLine();

            Console.WriteLine("* Not directly part of baseline comparison. Generates a set of templates based on the command file:");
            Console.WriteLine("  --new-template-data <dotnet command {new|new3}> <creation command file> <creation base dir>");
        }

        public static int CreateFileSeparatedBaseline(string dotnetCommand, string creationCommandFile, string baselineBaseDir)
        {
            InvocationCommandData commandData = InvocationCommandData.FromFile(creationCommandFile);
            BaselineCreator creator = new BaselineCreator(baselineBaseDir, dotnetCommand, commandData.Invocations);
            creator.CreateBaseline();

            return 0;
        }

        public static int CreateFileSeparatedBaselineComparison(string baselineBaseDir, string comparisonBaseDir, bool debug)
        {
            // create the templates to compare to the baseline master data, and do the template-to-template comparisons.
            TemplateCompareToBaselineReportCreator comparisonCreator = new TemplateCompareToBaselineReportCreator(baselineBaseDir, comparisonBaseDir);
            comparisonCreator.CreateBaselineComparison(debug);

            // Compare the baseline comparisons against the newly created comparisons.
            ComparisonComparisonReportCreator comparisonComparisonCreator = new ComparisonComparisonReportCreator(comparisonCreator.BaselineReportDir, comparisonCreator.ComparisonReportDir);
            string comparisonComparisonReportDir = Path.Combine(comparisonBaseDir, "ComparisonComparisonReports");
            comparisonComparisonCreator.CreateComparisons(comparisonComparisonReportDir);

            return 0;
        }

        // Runs the creation commands
        // TODO: rework to use the same code wrapper as teh baseline and comparison creators... it's similar, but needs wrapping.
        private static int NewTemplateData(string dotnetCommand, string creationCommandFile, string creationBaseDir)
        {
            IReadOnlyList<string> templateCommands = TemplateCommandReader.ReadTemplateCommandFile(creationCommandFile);

            TemplateDataCreator dataCreator = new TemplateDataCreator(dotnetCommand, creationCommandFile, templateCommands);
            bool result = dataCreator.PerformTemplateCommands(false);

            return result ? 0 : -1;
        }
    }
}
