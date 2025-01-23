// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Test;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Microsoft.DotNet.Cli
{
    internal sealed class TestModulesFilterHandler
    {
        private readonly List<string> _args;

        private readonly TestApplicationActionQueue _actionQueue;

        public TestModulesFilterHandler(List<string> args, TestApplicationActionQueue actionQueue)
        {
            _args = args;
            _actionQueue = actionQueue;
        }

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
                    VSTestTrace.SafeWriteTrace(() => $"The provided root directory does not exist: {rootDirectory}");
                    return false;
                }
            }

            var testModulePaths = GetMatchedModulePaths(testModules, rootDirectory);

            // If no matches were found, we simply return
            if (!testModulePaths.Any())
            {
                VSTestTrace.SafeWriteTrace(() => $"No test modules found for the given test module pattern: {testModules} with root directory: {rootDirectory}");
                return false;
            }

            foreach (string testModule in testModulePaths)
            {
                var testApp = new TestApplication(new Module(testModule, null, null, null, true, true), _args);
                // Write the test application to the channel
                _actionQueue.Enqueue(testApp);
            }

            return true;
        }

        private static IEnumerable<string> GetMatchedModulePaths(string testModules, string rootDirectory)
        {
            var testModulePatterns = testModules.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            Matcher matcher = new();
            matcher.AddIncludePatterns(testModulePatterns);

            return MatcherExtensions.GetResultsInFullPath(matcher, rootDirectory);
        }
    }
}
