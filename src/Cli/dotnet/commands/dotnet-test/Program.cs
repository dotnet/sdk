// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestCommand : RestoringCommand
    {
        public TestCommand(
            IEnumerable<string> msbuildArgs,
            bool noRestore,
            string msbuildPath = null)
            : base(msbuildArgs, noRestore, msbuildPath)
        {
        }

        public static TestCommand FromParseResult(ParseResult result, string[] settings, string msbuildPath = null)
        {
            result.ShowHelpOrErrorIfAppropriate();

            var msbuildArgs = new List<string>()
            {
                "-target:VSTest",
                "-nodereuse:false", // workaround for https://github.com/Microsoft/vstest/issues/1503
                "-nologo"
            };

            msbuildArgs.AddRange(result.OptionValuesToBeForwarded(TestCommandParser.GetCommand()));

            msbuildArgs.AddRange(result.GetValueForArgument(TestCommandParser.SlnOrProjectArgument) ?? Array.Empty<string>());

            if (settings.Any())
            {
                // skip '--' and escape every \ to be \\ and every " to be \" to survive the next hop
                string[] escaped = settings.Skip(1).Select(s => s.Replace("\\", "\\\\").Replace("\"", "\\\"")).ToArray();

                string runSettingsArg = string.Join(";", escaped);
                msbuildArgs.Add($"-property:VSTestCLIRunSettings=\"{runSettingsArg}\"");
            }

            string verbosityArg = result.ForwardedOptionValues<IReadOnlyCollection<string>>(TestCommandParser.GetCommand(), "verbosity")?.SingleOrDefault() ?? null;
            if (verbosityArg != null)
            {
                string[] verbosity = verbosityArg.Split(':', 2);
                if (verbosity.Length == 2)
                {
                    msbuildArgs.Add($"-property:VSTestVerbosity={verbosity[1]}");
                }
            }

            if (!FeatureFlag.Instance.IsSet(FeatureFlag.DISABLE_ARTIFACTS_POSTPROCESSING))
            {
                // Add artifacts processing mode and test session id for the artifact post-processing
                msbuildArgs.Add("-property:VSTestArtifactsProcessingMode=collect");
                msbuildArgs.Add($"-property:VSTestSessionCorrelationId={testSessionCorrelationId}");
            }

            bool noRestore = result.HasOption(TestCommandParser.NoRestoreOption) || result.HasOption(TestCommandParser.NoBuildOption);

            TestCommand testCommand = new(
                msbuildArgs,
                noRestore,
                msbuildPath);

            // Apply environment variables provided by the user via --environment (-e) parameter, if present
            SetEnvironmentVariablesFromParameters(testCommand, result);

            // Set DOTNET_PATH if it isn't already set in the environment as it is required
            // by the testhost which uses the apphost feature (Windows only).
            (bool hasRootVariable, string rootVariableName, string rootValue) = VSTestForwardingApp.GetRootVariable();
            if (!hasRootVariable)
            {
                testCommand.EnvironmentVariable(rootVariableName, rootValue);
                VSTestTrace.SafeWriteTrace(() => $"Root variable set {rootVariableName}:{rootValue}");
            }

            VSTestTrace.SafeWriteTrace(() => $"Starting test using MSBuild with arguments '{testCommand.GetArgumentsToMSBuild()}' custom MSBuild path '{msbuildPath}' norestore '{noRestore}'");
            return testCommand;
        }

        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            var args = parseResult.GetArguments();

            // settings parameters are after -- (including --), these should not be considered by the parser
            var settings = args.SkipWhile(a => a != "--").ToArray();
            // all parameters before --
            args = args.TakeWhile(a => a != "--").ToArray();

            // VSTest runner will save artifacts inside a temp folder if needed.
            string expectedArtifactDirectory = Path.Combine(Path.GetTempPath(), testSessionCorrelationId);
            if (!Directory.Exists(expectedArtifactDirectory))
            {
                VSTestTrace.SafeWriteTrace(() => "No artifact found, post-processing won't run.");
                return 0;
            }

            VSTestTrace.SafeWriteTrace(() => $"Artifacts directory found '{expectedArtifactDirectory}', running post-processing.");

            var artifactsPostProcessArgs = new List<string> { "--artifactsProcessingMode-postprocess", $"--testSessionCorrelationId:{testSessionCorrelationId}" };

            if (parseResult.HasOption(TestCommandParser.DiagOption))
            {
                artifactsPostProcessArgs.Add($"--diag:{parseResult.GetValueForOption(TestCommandParser.DiagOption)}");
            }

            try
            {
                Environment.SetEnvironmentVariable(NodeWindowEnvironmentName, "1");
                return FromParseResult(parseResult, settings).Execute();
            }
            finally
            {
                if (Directory.Exists(expectedArtifactDirectory))
                {
                    VSTestTrace.SafeWriteTrace(() => $"Cleaning artifact directory '{expectedArtifactDirectory}'.");
                    try
                    {
                        Directory.Delete(expectedArtifactDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        VSTestTrace.SafeWriteTrace(() => $"Exception during artifact cleanup: \n{ex}");
                    }
                }
            }
        }

        private static bool ContainsBuiltTestSources(string[] args)
        {
            foreach (string arg in args)
            {
                if (!arg.StartsWith("-") &&
                    (arg.EndsWith("dll", StringComparison.OrdinalIgnoreCase) || arg.EndsWith("exe", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        private static void SetEnvironmentVariablesFromParameters(TestCommand testCommand, ParseResult parseResult)
        {
            Option<IEnumerable<string>> option = TestCommandParser.EnvOption;

            if (!parseResult.HasOption(option))
            {
                return;
            }

            foreach (var env in parseResult.GetValueForOption(option))
            {
                string name = env;
                string value = string.Empty;

                int equalsIndex = env.IndexOf('=');
                if (equalsIndex > 0)
                {
                    name = env.Substring(0, equalsIndex);
                    value = env.Substring(equalsIndex + 1);
                }

                testCommand.EnvironmentVariable(name, value);
            }
        }
    }
}
