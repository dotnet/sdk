// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test;
using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class TestCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-test";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore,
        };

        public static readonly Option<string> SettingsOption = new ForwardedOption<string>(new string[] { "-s", "--settings" }, LocalizableStrings.CmdSettingsDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdSettingsFile
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("VSTestSetting", CommandDirectoryContext.GetFullPath(o), true));

        public static readonly Option<bool> ListTestsOption = new ForwardedOption<bool>(new string[] { "-t", "--list-tests" }, LocalizableStrings.CmdListTestsDescription)
              .ForwardAs(CommonOptions.BuildProperty("VSTestListTests", true));

        public static readonly Option<IEnumerable<string>> EnvOption = new Option<IEnumerable<string>>(new string[] { "-e", "--environment" }, LocalizableStrings.CmdEnvironmentVariableDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdEnvironmentVariableExpression
        }.AllowSingleArgPerToken();

        public static readonly Option<string> FilterOption = new ForwardedOption<string>("--filter", LocalizableStrings.CmdTestCaseFilterDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdTestCaseFilterExpression
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("VSTestTestCaseFilter", o, true));

        public static readonly Option<IEnumerable<string>> AdapterOption = new ForwardedOption<IEnumerable<string>>(new string[] { "--test-adapter-path" }, LocalizableStrings.CmdTestAdapterPathDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdTestAdapterPath
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("VSTestTestAdapterPath", string.Join(";", o.Select(CommandDirectoryContext.GetFullPath)), true))
        .AllowSingleArgPerToken();

        public static readonly Option<IEnumerable<string>> LoggerOption = new ForwardedOption<IEnumerable<string>>(new string[] { "-l", "--logger" }, LocalizableStrings.CmdLoggerDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdLoggerOption
        }.ForwardAsSingle(o =>
        {
            var loggersString = string.Join(";", GetSemiColonEscapedArgs(o));

            return CommonOptions.BuildProperty("VSTestLogger", loggersString, true);
        })
        .AllowSingleArgPerToken();

        public static readonly Option<string> OutputOption = new ForwardedOption<string>(new string[] { "-o", "--output" }, LocalizableStrings.CmdOutputDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdOutputDir
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("OutputPath", CommandDirectoryContext.GetFullPath(o), true));

        public static readonly Option<string> DiagOption = new ForwardedOption<string>(new string[] { "-d", "--diag" }, LocalizableStrings.CmdPathTologFileDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdPathToLogFile
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("VSTestDiag", CommandDirectoryContext.GetFullPath(o), true));

        public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build", LocalizableStrings.CmdNoBuildDescription)
            .ForwardAs(CommonOptions.BuildProperty("VSTestNoBuild", true));

        public static readonly Option<string> ResultsOption = new ForwardedOption<string>(new string[] { "--results-directory" }, LocalizableStrings.CmdResultsDirectoryDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdPathToResultsDirectory
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("VSTestResultsDirectory", CommandDirectoryContext.GetFullPath(o), true));

        public static readonly Option<IEnumerable<string>> CollectOption = new ForwardedOption<IEnumerable<string>>("--collect", LocalizableStrings.cmdCollectDescription)
        {
            ArgumentHelpName = LocalizableStrings.cmdCollectFriendlyName
        }.ForwardAsSingle(o => CommonOptions.BuildProperty("VSTestCollect", string.Join(";", GetSemiColonEscapedArgs(o)), true))
        .AllowSingleArgPerToken();

        public static readonly Option<bool> BlameOption = new ForwardedOption<bool>("--blame", LocalizableStrings.CmdBlameDescription)
            .ForwardAs(CommonOptions.BuildProperty("VSTestBlame", true));

        public static readonly Option<bool> BlameCrashOption = new ForwardedOption<bool>("--blame-crash", LocalizableStrings.CmdBlameCrashDescription)
            .ForwardAs(CommonOptions.BuildProperty("VSTestBlameCrash", true));

        public static readonly Argument<string> BlameCrashDumpArgument = new Argument<string>(LocalizableStrings.CrashDumpTypeArgumentName).FromAmong(new string[] { "full", "mini" });

        public static readonly Option<string> BlameCrashDumpOption = new ForwardedOption<string>("--blame-crash-dump-type", LocalizableStrings.CmdBlameCrashDumpTypeDescription)
            .ForwardAsMany(o => new[] { CommonOptions.BuildProperty("VSTestBlameCrash", true), CommonOptions.BuildProperty("VSTestBlameCrashDumpType", o) });

        public static readonly Option<string> BlameCrashAlwaysOption = new ForwardedOption<string>("--blame-crash-collect-always", LocalizableStrings.CmdBlameCrashCollectAlwaysDescription)
            .ForwardAsMany(o => new[] { CommonOptions.BuildProperty("VSTestBlameCrash", true), CommonOptions.BuildProperty("VSTestBlameCrashCollectAlways", true) });

        public static readonly Option<bool> BlameHangOption = new ForwardedOption<bool>("--blame-hang", LocalizableStrings.CmdBlameHangDescription)
            .ForwardAs(CommonOptions.BuildProperty("VSTestBlameHang", true));

        public static readonly Argument<string> BlameHangDumpArgument = new Argument<string>(LocalizableStrings.HangDumpTypeArgumentName).FromAmong(new string[] { "full", "mini", "none" });

        public static readonly Option<string> BlameHangDumpOption = new ForwardedOption<string>("--blame-hang-dump-type", LocalizableStrings.CmdBlameHangDumpTypeDescription)
            .ForwardAsMany(o => new[] { CommonOptions.BuildProperty("VSTestBlameHang", true), CommonOptions.BuildProperty("VSTestBlameHangDumpType", o) });

        public static readonly Option<string> BlameHangTimeoutOption = new ForwardedOption<string>("--blame-hang-timeout", LocalizableStrings.CmdBlameHangTimeoutDescription)
        {
            ArgumentHelpName = LocalizableStrings.HangTimeoutArgumentName
        }.ForwardAsMany(o => new[] { CommonOptions.BuildProperty("VSTestBlameHang", true), CommonOptions.BuildProperty("VSTestBlameHangTimeout", o) });

        public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo", LocalizableStrings.CmdNoLogo)
            .ForwardAs(CommonOptions.BuildProperty("VSTestNoLogo", "nologo"));

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

            command.AddArgument(SlnOrProjectArgument);

            command.AddOption(SettingsOption);
            command.AddOption(ListTestsOption);
            command.AddOption(EnvOption);
            command.AddOption(FilterOption);
            command.AddOption(AdapterOption);
            command.AddOption(LoggerOption);
            command.AddOption(OutputOption);
            command.AddOption(DiagOption);
            command.AddOption(NoBuildOption);
            command.AddOption(ResultsOption);
            command.AddOption(CollectOption);
            command.AddOption(BlameOption);
            command.AddOption(BlameCrashOption);
            command.AddOption(BlameCrashDumpOption);
            command.AddOption(BlameCrashAlwaysOption);
            command.AddOption(BlameHangOption);
            command.AddOption(BlameHangDumpOption);
            command.AddOption(BlameHangTimeoutOption);
            command.AddOption(NoLogoOption);
            command.AddOption(ConfigurationOption);
            command.AddOption(FrameworkOption);
            command.AddOption(CommonOptions.RuntimeOption.WithHelpDescription(command, LocalizableStrings.RuntimeOptionDescription));
            command.AddOption(NoRestoreOption);
            command.AddOption(CommonOptions.InteractiveMsBuildForwardOption);
            command.AddOption(CommonOptions.VerbosityOption);
            command.AddOption(CommonOptions.ArchitectureOption);
            command.AddOption(CommonOptions.OperatingSystemOption);

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
    }
}
