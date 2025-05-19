﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestModulesFilterHandler(TestApplicationActionQueue actionQueue, TerminalTestReporter output)
{
    private readonly TestApplicationActionQueue _actionQueue = actionQueue;
    private readonly TerminalTestReporter _output = output;

    public bool RunWithTestModulesFilter(ParseResult parseResult)
    {
        // If the module path pattern(s) was provided, we will use that to filter the test modules
        string testModules = parseResult.GetValue(TestingPlatformOptions.TestModulesFilterOption);

        // If the root directory was provided, we will use that to search for the test modules
        // Otherwise, we will use the current directory
        string rootDirectory = Directory.GetCurrentDirectory();
        if (parseResult.HasOption(TestingPlatformOptions.TestModulesRootDirectoryOption))
        {
            rootDirectory = parseResult.GetValue(TestingPlatformOptions.TestModulesRootDirectoryOption);

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

        foreach (string testModule in testModulePaths)
        {
            var testApp = new ParallelizableTestModuleGroupWithSequentialInnerModules(new TestModule(new RunProperties(testModule, null, null), null, null, true, true, null));
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
