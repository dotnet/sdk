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
                if (args.Length != 3)
                {
                    ShowUsageMessage();
                    return -1;
                }

                string baselineBaseDir = args[1];
                string comparisonBaseDir = args[2];

                return CreateFileSeparatedBaselineComparison(baselineBaseDir, comparisonBaseDir);
            }

            ShowUsageMessage();
            return -1;
        }

        private static void ShowUsageMessage()
        {
            Console.WriteLine("Invalid args. Valid usage patterns:");
            Console.WriteLine("--create-baseline <Dotnet command {new|new3}> <creation command file> <output base dir>.");
            Console.WriteLine("\t\tGenerates 2 sets of templates using the creation commands, then compares them and writes a baseline report.");

            Console.WriteLine("--compare-to-baseline <baseline base dir> <comparison base dir>.");
            Console.WriteLine("\t\tReads the baseline report, generates a set of templates using the commands in the baseline, compares to the baseline master data, then compares the comparison to the baseline comparison.");

            Console.WriteLine("--new-template-data <dotnet command {new|new3}> <creation command file> <creation base dir>");
            Console.WriteLine("\t\t* Not directly part of baseline comparison. Generates a set of templates based on the command file.");
        }

        public static int CreateFileSeparatedBaseline(string dotnetCommand, string creationCommandFile, string baselineBaseDir)
        {
            InvocationCommandData commandData = InvocationCommandData.FromFile(creationCommandFile);
            BaselineCreator creator = new BaselineCreator(baselineBaseDir, dotnetCommand, commandData.Invocations);
            creator.CreateBaseline();

            return 0;
        }

        public static int CreateFileSeparatedBaselineComparison(string baselineBaseDir, string comparisonBaseDir)
        {
            // create the templates to compare to the baseline master data, and do the template-to-template comparisons.
            TemplateCompareToBaselineReportCreator comparisonCreator = new TemplateCompareToBaselineReportCreator(baselineBaseDir, comparisonBaseDir);
            comparisonCreator.CreateBaselineComparison();

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
