using System;
using System.Collections.Generic;
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
                if (args.Length != 5)
                {
                    ShowUsageMessage();
                    return -1;
                }

                string dotnetCommand = args[1];
                string creationCommandFile = args[2];
                string creationBaseDir = args[3];
                string baselineReportFile = args[4];

                return CreateBaseline(dotnetCommand, creationCommandFile, creationBaseDir, baselineReportFile);
            }

            if (string.Equals(args[0], "--compare-to-baseline", StringComparison.Ordinal))
            {
                if (args.Length != 4 && args.Length != 5)
                {
                    ShowUsageMessage();
                    return -1;
                }

                string baselineReportFilePath = args[1];
                string newDataBaseDir = args[2];
                string comparisonReportFilePath = args[3];

                string templateComparisonReportFilePath = null;
                if (args.Length == 5)
                {
                    templateComparisonReportFilePath = args[4];
                }

                return CompareToBaseline(baselineReportFilePath, newDataBaseDir, comparisonReportFilePath, templateComparisonReportFilePath);
            }

            if (string.Equals(args[0], "--new-template-data"))
            {
                if (args.Length != 4)
                {
                    ShowUsageMessage();
                    return -1;
                }

                string dotnetCommand = args[1];
                string creationCommandFile = args[2];
                string creationBaseDir = args[3];

                return NewTemplateData(dotnetCommand, creationCommandFile, creationBaseDir);
            }

            ShowUsageMessage();
            return -1;
        }

        private static void ShowUsageMessage()
        {
            Console.WriteLine("Invalid args. Valid usage patterns:");
            Console.WriteLine("--create-baseline <Dotnet command {new|new3}> <creation command file> <creation base dir> <baseline report file>");
            Console.WriteLine("\t\tGenerates 2 sets of templates using the creation commands, then compares them and writes a baseline report.");

            Console.WriteLine("--compare-to-baseline <baseline report file> <new data base dir> <comparison report file path> [new template comparison report file path]");
            Console.WriteLine("\t\tReads the baseline report, generates a set of templates using the commands in the baseline, compares to the baseline master data, then compares the comparison to the baseline comparison.");

            Console.WriteLine("--new-template-data <dotnet command {new|new3}> <creation command file> <creation base dir>");
            Console.WriteLine("\t\t* Not directly part of baseline comparison. Generates a set of templates based on the command file.");
        }

        private static int CreateBaseline(string dotnetCommand, string creationCommandFile, string creationBaseDir, string baselineReportFile)
        {
            IReadOnlyList<string> templateCommands = TemplateCommandReader.ReadTemplateCommandFile(creationCommandFile);
            BaselineCreator creator = new BaselineCreator(creationBaseDir, dotnetCommand, templateCommands);
            bool result = creator.WriteBaseline(baselineReportFile);

            return result ? 0 : -1;
        }

        private static int CompareToBaseline(string baselineReportFilePath, string newDataBaseDir, string comparisonReportFilePath, string templateComparisonReportFilePath = null)
        {
            try
            {
                BaselineComparer comparer = new BaselineComparer(baselineReportFilePath, newDataBaseDir);

                if (templateComparisonReportFilePath != null)
                {
                    comparer.WriteTemplateComparisonReport(templateComparisonReportFilePath);
                }

                // possibly bring this back with an optional parameter. It's a short, human-readable summary.
                //comparer.WriteFreeFormComparisonDifferenceReport(comparisonReportFilePath);

                comparer.WriteStructuredComparisonDifferenceReport(comparisonReportFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating the baseline comparison. Error: {ex.Message.ToString()}");
                return -1;
            }

            return 0;
        }

        // Runs the creation commands
        private static int NewTemplateData(string dotnetCommand, string creationCommandFile, string creationBaseDir)
        {
            IReadOnlyList<string> templateCommands = TemplateCommandReader.ReadTemplateCommandFile(creationCommandFile);

            TemplateDataCreator dataCreator = new TemplateDataCreator(dotnetCommand, creationCommandFile, templateCommands);
            bool result = dataCreator.PerformTemplateCommands();

            return result ? 0 : -1;
        }
    }
}
