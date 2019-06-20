using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using Reporting;

namespace PerformanceTestsResultGenerator
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            Option outputPath = new Option(
                "--output",
                "path of output file",
                new Argument<FileInfo>());

            Option repositoryRoot = new Option(
                "--repository-root",
                "repository root that contain .git directory",
                new Argument<DirectoryInfo>());

            // Add them to the root command
            RootCommand rootCommand = new RootCommand();
            rootCommand.Description = "Performance tests result generator";
            rootCommand.AddOption(outputPath);
            rootCommand.AddOption(repositoryRoot);

            var result = rootCommand.Parse(args);
            var outputPathValue = result.ValueForOption<FileInfo>("output");

            if (outputPathValue == null)
            {
                throw new PerformanceTestsResultGeneratorException("--output option is required." + outputPathValue);
            }

            var repositoryRootValue = result.ValueForOption<DirectoryInfo>("repository-root");

            if (repositoryRootValue == null)
            {
                throw new PerformanceTestsResultGeneratorException("--repository-root is required." + repositoryRootValue);
            }

            Reporter reporter = Reporter.CreateReporter(repositoryRootValue);
            string generatedJson = reporter.GetJson();
            Console.WriteLine("Generated json:" + Environment.NewLine + generatedJson);
            File.WriteAllText(outputPathValue.FullName, generatedJson);

            return 0;
        }
    }
}
