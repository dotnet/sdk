// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestModulesFilterHandler(TerminalTestReporter output)
{
    private readonly TerminalTestReporter _output = output;
    private List<ParallelizableTestModuleGroupWithSequentialInnerModules> _testApplications = [];

    public bool RunWithTestModulesFilter(ParseResult parseResult, string testModules)
    {
        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        // If the module path pattern(s) was provided, we will use that to filter the test modules
        // If the root directory was provided, we will use that to search for the test modules
        // Otherwise, we will use the current directory
        string? rootDirectory = Directory.GetCurrentDirectory();
        if (parseResult.HasOption(definition.TestModulesRootDirectoryOption))
        {
            rootDirectory = parseResult.GetValue(definition.TestModulesRootDirectoryOption);

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

        _testApplications = BuildTestApplications(testModulePaths);
        return true;
    }

    public IEnumerable<TestModule> EnumerateTestModules()
        => _testApplications.SelectMany(static moduleGroup => moduleGroup);

    public void EnqueueTestApplications(TestApplicationActionQueue actionQueue)
    {
        foreach (ParallelizableTestModuleGroupWithSequentialInnerModules testApplication in _testApplications)
        {
            actionQueue.Enqueue(testApplication);
        }
    }

    private static List<ParallelizableTestModuleGroupWithSequentialInnerModules> BuildTestApplications(IEnumerable<string> testModulePaths)
    {
        var muxerPath = new Muxer().MuxerPath;
        List<ParallelizableTestModuleGroupWithSequentialInnerModules> testApplications = [];
        foreach (string testModule in testModulePaths)
        {
            // We want to produce the right RunCommand and RunArguments for TestApplication implementation to consume directly.
            // We don't want TestApplication class to be concerned about whether it's running dll via test module or not.
            // If we are given dll, we use dotnet exec. Otherwise, we run the executable directly.
            RunProperties runProperties = testModule.HasExtension(CliConstants.DLLExtension)
                ? new RunProperties(muxerPath, $@"exec ""{testModule}""", null)
                : new RunProperties(testModule, null, null);

            testApplications.Add(new ParallelizableTestModuleGroupWithSequentialInnerModules(
                new TestModule(runProperties, null, null, true, null, testModule, DotnetRootArchVariableName: null)));
        }

        return testApplications;
    }

    private static IEnumerable<string> GetMatchedModulePaths(string testModules, string rootDirectory)
    {
        var testModulePatterns = testModules.Split([';'], StringSplitOptions.RemoveEmptyEntries);

        Matcher matcher = new();
        matcher.AddIncludePatterns(testModulePatterns);

        return matcher.GetResultsInFullPath(rootDirectory);
    }
}
