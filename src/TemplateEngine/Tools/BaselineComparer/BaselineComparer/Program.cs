using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BaselineComparer
{
    class Program
    {
        static int Main(string[] args)
        {
            //TestStuff();
            //return 0;

            if (args.Length != 4)
            {
                ShowUsageMessage();
                return -1;
            }

            if (string.Equals(args[0], "--create", StringComparison.Ordinal))
            {
                string baselineDataDir = args[1];
                string compareDataDir = args[2];
                string baselineReportFilePath = args[3];

                return TryCreateBaseline(baselineDataDir, compareDataDir, baselineReportFilePath);
            }

            if (string.Equals(args[0], "--compare", StringComparison.Ordinal))
            {
                string baselineReportFilePath = args[1];
                string compareDataDir = args[2];
                string comparisonReportFilePath = args[3];

                return CompareToBaseline(baselineReportFilePath, compareDataDir, comparisonReportFilePath);
            }

            ShowUsageMessage();
            return -1;
        }

        private static void ShowUsageMessage()
        {
            Console.WriteLine("Invalid args. Valid usage patterns:");
            Console.WriteLine("--create <baseline data dir> <compare data dir> <baseline report file path>");
            Console.WriteLine("--compare <baseline report file> <compare data dir> <comparison report file path>");
        }

        static void TestStuff()
        {
            string checkBasePath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Comparison\New3_VB_eval_tests";

            //string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\2018_01_05_baseline.json";
            //string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\2018_01_05_baseline_missing_a_file.json";
            //string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\2018_01_05_baseline_extra_file.json";
            //string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\2018_01_05_baseline_extra_and_missing_file.json";
            //string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\2018_01_05_baseline_removed_baseline_diff.json";
            //string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\2018_01_05_baseline_extra_baseline_diff.json";

            string baselineReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\Tests2\2018_01_09_baseline.json";

            //string baselineDataBasePath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Comparison\New3_2.1.0-preview2_baseline_w_item_and_web2.1";
            //TryCreateBaseline(baselineDataBasePath, checkBasePath, baselineReportPath);

            // compare the baseline report against the secondary data used to create the baseline (should be identical, nothing bad).
            string comparisonReportPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Baselining\Tests3\2018_01_09_comparison.json";

            CompareToBaseline(baselineReportPath, checkBasePath, comparisonReportPath);

            Console.ReadLine();
        }

        private static int CompareToBaseline(string baselineReportPath, string checkPath, string comparisonReportFilePath)
        {
            DataToBaselineChecker checker = new DataToBaselineChecker(baselineReportPath, checkPath);
            DirectoryComparisonDifference result = checker.ComparisonResult;

            ComparisonDifferenceReportCreator reporter = new ComparisonDifferenceReportCreator(result);
            Console.WriteLine(reporter.ReportText);

            string reportDirectory = Path.GetDirectoryName(comparisonReportFilePath);
            if (!Directory.Exists(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            File.WriteAllText(comparisonReportFilePath, reporter.ReportText);

            return reporter.AnyProblems ? -1 : 0;
        }

        private static int TryCreateBaseline(string baselinePath, string checkPath, string reportPath)
        {
            BaselineCreator baselineCreator = new BaselineCreator(baselinePath, checkPath);

            if (!baselineCreator.ComparisonResult.IsValidBaseline)
            {
                Console.WriteLine("Could not create baseline.");

                IReadOnlyList<string> missingBaselineFiles = baselineCreator.MissingBaselineFiles;
                if (missingBaselineFiles.Count > 0)
                {
                    Console.WriteLine("The check directory contains these files, missing from the baseline directory:");
                    foreach (string file in missingBaselineFiles)
                    {
                        Console.WriteLine($"\t{file}");
                    }
                }

                IReadOnlyList<string> missingCheckFiles = baselineCreator.MissingCheckFiles;
                if (missingCheckFiles.Count > 0)
                {
                    Console.WriteLine("The baseline directory contains these files, missing from the check directory:");
                    foreach (string file in missingCheckFiles)
                    {
                        Console.WriteLine($"\t{file}");
                    }
                }

                return -1;
            }

            try
            {
                baselineCreator.WriteBaseline(reportPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing the baseline file:");
                Console.WriteLine(ex.Message);
                return -1;
            }

            Console.WriteLine($"Successfully wrote baseline file {reportPath}");
            return 0;
        }

        private static void DirectoryTest(string baselinePath, string checkPath)    //, CompareMode mode)
        {
            Console.WriteLine($"Baseline base directory: {baselinePath}");
            Console.WriteLine($"Check base directory: {checkPath}");

            DirectoryComparer comparer = new DirectoryComparer(baselinePath, checkPath); //, mode);
            DirectoryDifference results = comparer.Compare();

            IReadOnlyList<string> baselineOnlyFiles = results.MissingCheckFiles;
            if (baselineOnlyFiles.Count > 0)
            {
                Console.WriteLine("These baseline files had no corresponding check files:");
                foreach (string differenceFile in baselineOnlyFiles)
                {
                    Console.WriteLine($"\t{differenceFile}");
                }
            }

            IReadOnlyList<string> checkOnlyFiles = results.MissingBaselineFiles;
            if (checkOnlyFiles.Count > 0)
            {
                Console.WriteLine("These check files had no corresponding baseline files:");
                foreach (string differenceFile in checkOnlyFiles)
                {
                    Console.WriteLine($"\t{differenceFile}");
                }
            }

            IList<FileDifference> contentDifferences = results.FileResults.Where(x => x.Differences.Count > 0).ToList();
            if (contentDifferences.Count > 0)
            {
                Console.WriteLine("*** Content Differences ***");
                Console.WriteLine();

                foreach (FileDifference fileDifference in contentDifferences)
                {
                    Console.WriteLine($"\t{fileDifference.File}");

                    foreach (PositionalDifference contentDifference in fileDifference.Differences)
                    {
                        contentDifference.ConsoleDebug();
                    }

                    Console.WriteLine();
                }
            }
        }

        private static void FileTest()
        {
            string baselinePath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Comparison\New3_2.1.0-preview2_baseline_w_item_and_web2.1\Test_C\MVC_2.0_Ind\Mvc_2.0_Ind.csproj";
            string checkTargetPath = @"C:\Users\sepeters\Desktop\TemplateProcessing\Comparison\New3_VB_eval_tests\Test_C\Mvc_2.0_Ind\Mvc_2.0_Ind.csproj";

            FileComparer comparer = new FileComparer(baselinePath, checkTargetPath);    //, CompareMode.Check);
            IReadOnlyList<PositionalDifference> differenceList = comparer.Compare();

            Console.WriteLine("Differences:");
            foreach (PositionalDifference difference in differenceList)
            {
                difference.ConsoleDebug();
            }

            Console.WriteLine("...done");
            Console.ReadLine();
        }

        private static void TestRead(string pathToRead, string outputPath)
        {
            using (FileStream source = File.Open(pathToRead, FileMode.Open))
            using (FileStream target = File.Create(outputPath))
            {
                BufferedReadStream reader = new BufferedReadStream(source);

                while (reader.TryReadNext(out byte next))
                {
                    target.WriteByte(next);
                }
            }
        }
    }
}
