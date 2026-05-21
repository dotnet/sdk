// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal sealed class TestModulesFilterHandler : ITestHandler
{
    private readonly string _testModules;
    private readonly string? _testModulesRoot;
    private readonly List<string> _testModulePaths;

    public TestModulesFilterHandler(string testModules, ParseResult parseResult)
    {
        _testModules = testModules;

        var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

        // If the module path pattern(s) was provided, we will use that to filter the test modules
        // If the root directory was provided, we will use that to search for the test modules
        // Otherwise, we will use the current directory
        string? rootDirectory = Directory.GetCurrentDirectory();
        if (parseResult.HasOption(definition.TestModulesRootDirectoryOption))
        {
            rootDirectory = parseResult.GetValue(definition.TestModulesRootDirectoryOption);
        }

        _testModulesRoot = rootDirectory;
        _testModulePaths = GetMatchedModulePaths(_testModules, _testModulesRoot);
    }

    public bool Initialize()
    {
        // If the root directory is not valid, we simply return
        if (string.IsNullOrEmpty(_testModulesRoot) || !Directory.Exists(_testModulesRoot))
        {
            Reporter.Output.WriteLine(string.Format(CliCommandStrings.CmdNonExistentRootDirectoryErrorDescription, _testModulesRoot).Yellow());
            return false;
        }

        // If no matches were found, we simply return
        if (_testModulePaths.Count == 0)
        {
            Reporter.Output.WriteLine(string.Format(CliCommandStrings.CmdNoTestModulesErrorDescription, _testModules, _testModulesRoot).Yellow());
            return false;
        }

        return true;
    }

    public int RunTestApplications(TestApplicationActionQueue actionQueue)
    {
        var muxerPath = new Muxer().MuxerPath;
        foreach (string testModule in _testModulePaths)
        {
            // We want to produce the right RunCommand and RunArguments for TestApplication implementation to consume directly.
            // We don't want TestApplication class to be concerned about whether it's running dll via test module or not.
            // If we are given dll, we use dotnet exec. Otherwise, we run the executable directly.
            RunProperties runProperties = testModule.HasExtension(CliConstants.DLLExtension)
                ? new RunProperties(muxerPath, $@"exec ""{testModule}""", null)
                : new RunProperties(testModule, null, null);

            var testApp = new ParallelizableTestModuleGroupWithSequentialInnerModules(new TestModule(runProperties, null, null, true, null, testModule, DotnetRootArchVariableName: null));
            // Write the test application to the channel
            actionQueue.Enqueue(testApp);
        }

        return actionQueue.CompleteEnqueueAndWait();
    }

    private static List<string> GetMatchedModulePaths(string testModules, string? rootDirectory)
    {
        if (string.IsNullOrEmpty(rootDirectory))
        {
            return new List<string>();
        }

        var testModulePatterns = testModules.Split([';'], StringSplitOptions.RemoveEmptyEntries);

        Matcher matcher = new();
        matcher.AddIncludePatterns(testModulePatterns);

        // Make sure we have a non-lazy collection, so that if we enumerate multiple times we guarantee the same result.
        var results = matcher.GetResultsInFullPath(rootDirectory);
        if (results is List<string> resultsList)
        {
            return resultsList;
        }

        return results.ToList();
    }
}
