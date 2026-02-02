// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract partial class TestCommandDefinition
{
    public sealed class VSTest : TestCommandDefinition
    {
        public readonly Option<string> SettingsOption = new Option<string>("--settings", "-s")
        {
            Description = CommandDefinitionStrings.CmdSettingsDescription,
            HelpName = CommandDefinitionStrings.CmdSettingsFile
        }.ForwardAsSingle(o => $"-property:VSTestSetting={MSBuildPropertyParser.SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public readonly Option<bool> ListTestsOption = new Option<bool>("--list-tests", "-t")
        {
            Description = CommandDefinitionStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:VSTestListTests=true");

        public readonly Option<IReadOnlyDictionary<string, string>> TestEnvOption = CommonOptions.CreateEnvOption(CommandDefinitionStrings.CmdTestEnvironmentVariableDescription);

        public readonly Option<string> FilterOption = new Option<string>("--filter")
        {
            Description = CommandDefinitionStrings.CmdTestCaseFilterDescription,
            HelpName = CommandDefinitionStrings.CmdTestCaseFilterExpression
        }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={MSBuildPropertyParser.SurroundWithDoubleQuotes(o!)}");

        public readonly Option<IEnumerable<string>> AdapterOption = new Option<IEnumerable<string>>("--test-adapter-path")
        {
            Description = CommandDefinitionStrings.CmdTestAdapterPathDescription,
            HelpName = CommandDefinitionStrings.CmdTestAdapterPath
        }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath={MSBuildPropertyParser.SurroundWithDoubleQuotes(string.Join(";", o!.Select(CommandDirectoryContext.GetFullPath)))}")
        .AllowSingleArgPerToken();

        public readonly Option<IEnumerable<string>> LoggerOption = new Option<IEnumerable<string>>("--logger", "-l")
        {
            Description = CommandDefinitionStrings.CmdLoggerDescription,
            HelpName = CommandDefinitionStrings.CmdLoggerOption
        }.ForwardAsSingle(o =>
        {
            var loggersString = string.Join(";", GetSemiColonEscapedArgs(o!));
            return $"-property:VSTestLogger={MSBuildPropertyParser.SurroundWithDoubleQuotes(loggersString)}";
        })
        .AllowSingleArgPerToken();

        public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
        {
            Description = CommandDefinitionStrings.CmdOutputDescription,
            HelpName = CommandDefinitionStrings.TestCmdOutputDir
        }
        .ForwardAsOutputPath("OutputPath", true);

        public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();

        public readonly Option<string> DiagOption = new Option<string>("--diag", "-d")
        {
            Description = CommandDefinitionStrings.CmdPathTologFileDescription,
            HelpName = CommandDefinitionStrings.CmdPathToLogFile
        }
        .ForwardAsSingle(o => $"-property:VSTestDiag={MSBuildPropertyParser.SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
        {
            Description = CommandDefinitionStrings.CmdNoBuildDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:VSTestNoBuild=true");

        public readonly Option<string> ResultsOption = new Option<string>("--results-directory")
        {
            Description = CommandDefinitionStrings.CmdResultsDirectoryDescription,
            HelpName = CommandDefinitionStrings.CmdPathToResultsDirectory
        }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={MSBuildPropertyParser.SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public readonly Option<IEnumerable<string>> CollectOption = new Option<IEnumerable<string>>("--collect")
        {
            Description = CommandDefinitionStrings.cmdCollectDescription,
            HelpName = CommandDefinitionStrings.cmdCollectFriendlyName
        }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", GetSemiColonEscapedArgs(o!))}\"")
        .AllowSingleArgPerToken();

        public readonly Option<bool> BlameOption = new Option<bool>("--blame")
        {
            Description = CommandDefinitionStrings.CmdBlameDescription,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled("-property:VSTestBlame=true");

        public readonly Option<bool> BlameCrashOption = new Option<bool>("--blame-crash")
        {
            Description = CommandDefinitionStrings.CmdBlameCrashDescription,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled("-property:VSTestBlameCrash=true");

        public readonly Option<string> BlameCrashDumpOption = CreateBlameCrashDumpOption();

        public readonly Option<bool> BlameCrashAlwaysOption = new Option<bool>("--blame-crash-collect-always")
        {
            Description = CommandDefinitionStrings.CmdBlameCrashCollectAlwaysDescription,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled(["-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true"]);

        public readonly Option<bool> BlameHangOption = new Option<bool>("--blame-hang")
        {
            Description = CommandDefinitionStrings.CmdBlameHangDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:VSTestBlameHang=true");

        public readonly Option<string> BlameHangDumpOption = CreateBlameHangDumpOption();

        public readonly Option<string> BlameHangTimeoutOption = new Option<string>("--blame-hang-timeout")
        {
            Description = CommandDefinitionStrings.CmdBlameHangTimeoutDescription,
            HelpName = CommandDefinitionStrings.HangTimeoutArgumentName
        }.ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}"]);

        public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption(forwardAs: "--property:VSTestNoLogo=true", description: CommandDefinitionStrings.TestCmdNoLogo);

        public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

        public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();

        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.CreatePropertyOption();

        public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();

        public readonly Option<string[]> VsTestTargetOption = CommonOptions.CreateRequiredMSBuildTargetOption("VSTest");

        private static Option<string> CreateBlameCrashDumpOption()
        {
            Option<string> result = new Option<string>("--blame-crash-dump-type")
            {
                Description = CommandDefinitionStrings.CmdBlameCrashDumpTypeDescription,
                HelpName = CommandDefinitionStrings.CrashDumpTypeArgumentName,
            }
            .ForwardAsMany(o => ["-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}"]);
            result.AcceptOnlyFromAmong(["full", "mini"]);
            return result;
        }

        private static Option<string> CreateBlameHangDumpOption()
        {
            Option<string> result = new Option<string>("--blame-hang-dump-type")
            {
                Description = CommandDefinitionStrings.CmdBlameHangDumpTypeDescription,
                HelpName = CommandDefinitionStrings.HangDumpTypeArgumentName
            }
            .ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}"]);
            result.AcceptOnlyFromAmong(["full", "mini", "none"]);
            return result;
        }

        public VSTest()
            : base(CommandDefinitionStrings.DotnetTestCommandVSTestDescription)
        {
            // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
            // MSBuild command so we are letting it flow.

            Options.Add(SettingsOption);
            Options.Add(ListTestsOption);
            Options.Add(TestEnvOption);
            Options.Add(FilterOption);
            Options.Add(AdapterOption);
            Options.Add(LoggerOption);
            Options.Add(OutputOption);
            Options.Add(ArtifactsPathOption);
            Options.Add(DiagOption);
            Options.Add(NoBuildOption);
            Options.Add(ResultsOption);
            Options.Add(CollectOption);
            Options.Add(BlameOption);
            Options.Add(BlameCrashOption);
            Options.Add(BlameCrashDumpOption);
            Options.Add(BlameCrashAlwaysOption);
            Options.Add(BlameHangOption);
            Options.Add(BlameHangDumpOption);
            Options.Add(BlameHangTimeoutOption);
            Options.Add(NoLogoOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            Options.Add(NoRestoreOption);
            Options.Add(InteractiveOption);
            Options.Add(VerbosityOption);
            TargetPlatformOptions.AddTo(Options);
            Options.Add(PropertiesOption);
            Options.Add(DisableBuildServersOption);
            Options.Add(VsTestTargetOption);
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
