using System;
using System.Collections.Generic;
using System.IO;
using BaselineComparer.Helpers;
using BaselineComparer.TemplateComparison;
using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class BaselineCreator
    {
        private static readonly string BaselineMasterDirName = "BaselineMaster";
        private static readonly string BaselineComparisonDirName = "BaselineCompare";

        // When establishing a baseline, neither template creation directory is particularly "preferred". But one is designated as the baseline.
        // The same baseline data must be used for future comparisons, because the differences in the comparison results is the real test.
        public BaselineCreator(string baseDataDir, string newCommand, IReadOnlyList<string> templateCommands)
        {
            BaselineDir = Path.Combine(baseDataDir, BaselineMasterDirName);
            ComparisonDir = Path.Combine(baseDataDir, BaselineComparisonDirName);
            NewCommand = newCommand;
            TemplateCommands = templateCommands;
        }

        public string BaselineDir { get; }
        public string ComparisonDir { get; }
        public string NewCommand { get; }
        public IReadOnlyList<string> TemplateCommands { get; }

        private DirectoryDifference _comparisonResult;

        public bool WriteBaseline(string baselineDataFilename)
        {
            EnsureComparisons();

            if (!ValidateBaseline())
            {
                return false;
            }

            Baseline baseline = new Baseline(BaselineDir, ComparisonDir, NewCommand, TemplateCommands, _comparisonResult);
            JObject serializedBaseline = JObject.FromObject(baseline);
            File.WriteAllText(baselineDataFilename, serializedBaseline.ToString());

            return true;
        }

        private bool ValidateBaseline()
        {
            if (!ComparisonResult.IsValidBaseline)
            {
                Console.WriteLine("Could not create baseline.");

                if (MissingBaselineFiles.Count > 0)
                {
                    Console.WriteLine("The secondary directory contains these files, missing from the baseline directory:");
                    foreach (string file in MissingBaselineFiles)
                    {
                        Console.WriteLine($"\t{file}");
                    }
                }

                if (MissingSecondaryFiles.Count > 0)
                {
                    Console.WriteLine("The baseline directory contains these files, missing from the secondary directory:");
                    foreach (string file in MissingSecondaryFiles)
                    {
                        Console.WriteLine($"\t{file}");
                    }
                }

                return false;
            }

            return true;
        }

        public DirectoryDifference ComparisonResult
        {
            get
            {
                EnsureComparisons();

                return _comparisonResult;
            }
        }

        public IReadOnlyList<string> MissingBaselineFiles
        {
            get
            {
                EnsureComparisons();

                return _comparisonResult.MissingBaselineFiles;
            }
        }

        public IReadOnlyList<string> MissingSecondaryFiles
        {
            get
            {
                EnsureComparisons();

                return _comparisonResult.MissingSecondaryFiles;
            }
        }

        private void EnsureComparisons()
        {
            if (_comparisonResult == null)
            {
                if (!CreateTemplatesToCompare())
                {
                    throw new Exception("Error creating the templates to compare.");
                }

                DirectoryComparer comparer = new DirectoryComparer(BaselineDir, ComparisonDir);
                _comparisonResult = comparer.Compare();
            }
        }
        private bool CreateTemplatesToCompare()
        {
            Console.WriteLine("Generating baseline master data.");
            if (!TemplateCommandRunner.RunTemplateCommands(NewCommand, BaselineDir, TemplateCommands))
            {
                return false;
            }

            Console.WriteLine("Generating comparison data.");
            if (!TemplateCommandRunner.RunTemplateCommands(NewCommand, ComparisonDir, TemplateCommands))
            {
                return false;
            }

            return true;
        }
    }
}
