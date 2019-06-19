using Reporting;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace PerformanceTestsResultGenerator
{
    class Program
    {
        static int Main(string[] args)
        {
            Option optionThatTakesFileInfo = new Option(
                "--file-option",
                "An option whose argument is parsed as a FileInfo",
                new Argument<FileInfo>());

            // Add them to the root command
            var rootCommand = new RootCommand();
            rootCommand.Description = "Performance tests result generator";
            rootCommand.AddOption(optionThatTakesFileInfo);

            rootCommand.Handler = CommandHandler.Create<int, bool, FileInfo>((intOption, boolOption, fileOption) =>
            {
                Console.WriteLine($"The value for --file-option is: {fileOption?.FullName ?? "null"}");
                Reporter reporter = Reporter.CreateReporter();
                Console.WriteLine(reporter.GetJson());
            });

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
