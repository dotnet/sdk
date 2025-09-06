// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestModulesFilterHandler(TestApplicationActionQueue actionQueue, TerminalTestReporter output)
{
    private readonly TestApplicationActionQueue _actionQueue = actionQueue;
    private readonly TerminalTestReporter _output = output;

    public bool RunWithTestModulesFilter(ParseResult parseResult)
    {
        // If the module path pattern(s) was provided, we will use that to filter the test modules
        string testModules = parseResult.GetValue(MicrosoftTestingPlatformOptions.TestModulesFilterOption);

        // If the root directory was provided, we will use that to search for the test modules
        // Otherwise, we will use the current directory
        string rootDirectory = Directory.GetCurrentDirectory();
        if (parseResult.HasOption(MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption))
        {
            rootDirectory = parseResult.GetValue(MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption);

            // If the root directory is not valid, we simply return
            if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                _output.WriteMessage(string.Format(CliCommandStrings.CmdNonExistentRootDirectoryErrorDescription, rootDirectory),
                    new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow });
                return false;
            }
        }

        var testModulePaths = GetMatchedModulePaths(testModules, rootDirectory);

        // If no matches were found, we simply return
        if (!testModulePaths.Any())
        {
            _output.WriteMessage(string.Format(CliCommandStrings.CmdNoTestModulesErrorDescription, testModules, rootDirectory),
                new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow });
            return false;
        }

        var muxerPath = new Muxer().MuxerPath;
        foreach (string testModule in testModulePaths)
        {
            // We want to produce the right RunCommand and RunArguments for TestApplication implementation to consume directly.
            // We don't want TestApplication class to be concerned about whether it's running dll via test module or not.
            // If we are given dll, we use dotnet exec. Otherwise, we run the executable directly.
            RunProperties runProperties = testModule.HasExtension(CliConstants.DLLExtension)
                ? new RunProperties(muxerPath, $@"exec ""{testModule}""", null)
                : new RunProperties(testModule, null, null);

            var testApp = new ParallelizableTestModuleGroupWithSequentialInnerModules(new TestModule(runProperties, null, null, true, true, null, testModule, DotnetRootArchVariableName: null));
            // Write the test application to the channel
            _actionQueue.Enqueue(testApp);
        }

        return true;
    }

    private static IEnumerable<string> GetMatchedModulePaths(string testModules, string rootDirectory)
    {
        var testModulePatterns = testModules.Split([';'], StringSplitOptions.RemoveEmptyEntries);

        Matcher matcher = new();
        matcher.AddIncludePatterns(testModulePatterns);

        return matcher.GetResultsInFullPath(rootDirectory);
    }
}
