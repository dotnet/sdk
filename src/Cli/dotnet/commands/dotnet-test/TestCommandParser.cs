// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-test";

        public static readonly Option<string> SettingsOption = new ForwardedOption<string>(new string[] { "-s", "--settings" }, LocalizableStrings.CmdSettingsDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdSettingsFile
        }.ForwardAsSingle(o => $"-property:VSTestSetting={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public static readonly Option<bool> ListTestsOption = new ForwardedOption<bool>(new string[] { "-t", "--list-tests" }, LocalizableStrings.CmdListTestsDescription)
              .ForwardAs("-property:VSTestListTests=true");

        public static readonly Option<IEnumerable<string>> EnvOption = new Option<IEnumerable<string>>(new string[] { "-e", "--environment" }, LocalizableStrings.CmdEnvironmentVariableDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdEnvironmentVariableExpression
        }.AllowSingleArgPerToken();

        public static readonly Option<string> FilterOption = new ForwardedOption<string>("--filter", LocalizableStrings.CmdTestCaseFilterDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdTestCaseFilterExpression
        }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={SurroundWithDoubleQuotes(o)}");

        public static readonly Option<IEnumerable<string>> AdapterOption = new ForwardedOption<IEnumerable<string>>(new string[] { "--test-adapter-path" }, LocalizableStrings.CmdTestAdapterPathDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdTestAdapterPath
        }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath={SurroundWithDoubleQuotes(string.Join(";", o.Select(CommandDirectoryContext.GetFullPath)))}")
        .AllowSingleArgPerToken();

        public static readonly Option<IEnumerable<string>> LoggerOption = new ForwardedOption<IEnumerable<string>>(new string[] { "-l", "--logger" }, LocalizableStrings.CmdLoggerDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdLoggerOption
        }.ForwardAsSingle(o =>
        {
            var loggersString = string.Join(";", GetSemiColonEscapedArgs(o));

            return $"-property:VSTestLogger={SurroundWithDoubleQuotes(loggersString)}";
        })
        .AllowSingleArgPerToken();

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdOutputDir
        }
        .ForwardAsOutputPath("OutputPath", true);

        public static readonly Option<string> DiagOption = new ForwardedOption<string>(new string[] { "-d", "--diag" }, LocalizableStrings.CmdPathTologFileDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdPathToLogFile
        }
        .ForwardAsSingle(o => $"-property:VSTestDiag={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build", LocalizableStrings.CmdNoBuildDescription)
            .ForwardAs("-property:VSTestNoBuild=true");

        public static readonly Option<string> ResultsOption = new ForwardedOption<string>(new string[] { "--results-directory" }, LocalizableStrings.CmdResultsDirectoryDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdPathToResultsDirectory
        }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public static readonly Option<IEnumerable<string>> CollectOption = new ForwardedOption<IEnumerable<string>>("--collect", LocalizableStrings.cmdCollectDescription)
        {
            ArgumentHelpName = LocalizableStrings.cmdCollectFriendlyName
        }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", GetSemiColonEscapedArgs(o))}\"")
        .AllowSingleArgPerToken();

        public static readonly Option<bool> BlameOption = new ForwardedOption<bool>("--blame", LocalizableStrings.CmdBlameDescription)
            .ForwardAs("-property:VSTestBlame=true");

        public static readonly Option<bool> BlameCrashOption = new ForwardedOption<bool>("--blame-crash", LocalizableStrings.CmdBlameCrashDescription)
            .ForwardAs("-property:VSTestBlameCrash=true");

        public static readonly Option<string> BlameCrashDumpOption = CreateBlameCrashDumpOption();

        private static Option<string> CreateBlameCrashDumpOption()
        {
            Option<string> result = new ForwardedOption<string>("--blame-crash-dump-type", LocalizableStrings.CmdBlameCrashDumpTypeDescription)
            {
                ArgumentHelpName = LocalizableStrings.CrashDumpTypeArgumentName,
            }
            .ForwardAsMany(o => new[] { "-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}" });
            result.AcceptOnlyFromAmong(new string[] { "full", "mini" });
            return result;
        }

        public static readonly Option<bool> BlameCrashAlwaysOption = new ForwardedOption<bool>("--blame-crash-collect-always", LocalizableStrings.CmdBlameCrashCollectAlwaysDescription)
            .ForwardAsMany(o => new[] { "-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true" });

        public static readonly Option<bool> BlameHangOption = new ForwardedOption<bool>("--blame-hang", LocalizableStrings.CmdBlameHangDescription)
            .ForwardAs("-property:VSTestBlameHang=true");

        public static readonly Option<string> BlameHangDumpOption = CreateBlameHangDumpOption();

        private static Option<string> CreateBlameHangDumpOption()
        {
            Option<string> result = new ForwardedOption<string>("--blame-hang-dump-type", LocalizableStrings.CmdBlameHangDumpTypeDescription)
            {
                ArgumentHelpName = LocalizableStrings.HangDumpTypeArgumentName
            }
            .ForwardAsMany(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}" });
            result.AcceptOnlyFromAmong(new string[] { "full", "mini", "none" });
            return result;
        }

        public static readonly Option<string> BlameHangTimeoutOption = new ForwardedOption<string>("--blame-hang-timeout", LocalizableStrings.CmdBlameHangTimeoutDescription)
        {
            ArgumentHelpName = LocalizableStrings.HangTimeoutArgumentName
        }.ForwardAsMany(o => new[] { "-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}" });

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs("-property:VSTestNoLogo=nologo");

        public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

        public static readonly Option ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("test", DocsLink, LocalizableStrings.AppFullName);
            command.TreatUnmatchedTokensAsErrors = false;

            // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
            // MSBuild command so we are letting it flow.

            command.Options.Add(SettingsOption);
            command.Options.Add(ListTestsOption);
            command.Options.Add(EnvOption);
            command.Options.Add(FilterOption);
            command.Options.Add(AdapterOption);
            command.Options.Add(LoggerOption);
            command.Options.Add(OutputOption);
            command.Options.Add(DiagOption);
            command.Options.Add(NoBuildOption);
            command.Options.Add(ResultsOption);
            command.Options.Add(CollectOption);
            command.Options.Add(BlameOption);
            command.Options.Add(BlameCrashOption);
            command.Options.Add(BlameCrashDumpOption);
            command.Options.Add(BlameCrashAlwaysOption);
            command.Options.Add(BlameHangOption);
            command.Options.Add(BlameHangDumpOption);
            command.Options.Add(BlameHangTimeoutOption);
            command.Options.Add(NoLogoOption);
            command.Options.Add(ConfigurationOption);
            command.Options.Add(FrameworkOption);
            command.Options.Add(CommonOptions.RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.Options.Add(NoRestoreOption);
            command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
            command.Options.Add(CommonOptions.VerbosityOption);
            command.Options.Add(CommonOptions.ArchitectureOption);
            command.Options.Add(CommonOptions.OperatingSystemOption);

            command.SetHandler(TestCommand.Run);

            return command;
        }

        private static string GetSemiColonEscapedstring(string arg)
        {
            if (arg.IndexOf(";") != -1)
            {
                return arg.Replace(";", "%3b");
            }

            return arg;
        }

        private static string[] GetSemiColonEscapedArgs(IEnumerable<string> args)
        {
            int counter = 0;
            string[] array = new string[args.Count()];

            foreach (string arg in args)
            {
                array[counter++] = GetSemiColonEscapedstring(arg);
            }

            return array;
        }

        /// <summary>
        /// Adding double quotes around the property helps MSBuild arguments parser and avoid incorrect splits on ',' or ';'.
        /// </summary>
        internal /* for testing purposes */ static string SurroundWithDoubleQuotes(string input)
        {
            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            // If already escaped by double quotes then return original string.
            if (input.StartsWith("\"", StringComparison.Ordinal)
                && input.EndsWith("\"", StringComparison.Ordinal))
            {
                return input;
            }

            // We want to count the number of trailing backslashes to ensure
            // we will have an even number before adding the final double quote.
            // Otherwise the last \" will be interpreted as escaping the double
            // quote rather than a backslash and a double quote.
            var trailingBackslashesCount = 0;
            for (int i = input.Length - 1; i >= 0; i--)
            {
                if (input[i] == '\\')
                {
                    trailingBackslashesCount++;
                }
                else
                {
                    break;
                }
            }

            return trailingBackslashesCount % 2 == 0
                ? string.Concat("\"", input, "\"")
                : string.Concat("\"", input, "\\\"");
        }
    }
}
