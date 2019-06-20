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

            // Add them to the root command
            RootCommand rootCommand = new RootCommand();
            rootCommand.Description = "Performance tests result generator";
            rootCommand.AddOption(outputPath);

            var result = rootCommand.Parse(args);
            var outputPathValue = result.ValueForOption<FileInfo>("output");

            if (outputPathValue == null)
            {
                throw new PerformanceTestsResultGeneratorException("--output option is required." + outputPathValue);
            }

            Reporter reporter = Reporter.CreateReporter();
            string generatedJson = reporter.GetJson();
            Console.WriteLine("Generated json:" + Environment.NewLine + generatedJson);
            File.WriteAllText(outputPathValue.FullName, generatedJson);

            return 0;
        }
    }
}
