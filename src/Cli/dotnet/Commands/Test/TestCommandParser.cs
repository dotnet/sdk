// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.Extensions.Configuration;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-test";

    public static readonly Option<string> SettingsOption = new ForwardedOption<string>("--settings", "-s")
    {
        Description = CliCommandStrings.CmdSettingsDescription,
        HelpName = CliCommandStrings.CmdSettingsFile
    }.ForwardAsSingle(o => $"-property:VSTestSetting={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

    public static readonly Option<bool> ListTestsOption = new ForwardedOption<bool>("--list-tests", "-t")
    {
        Description = CliCommandStrings.CmdListTestsDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:VSTestListTests=true");

    public static readonly Option<string> FilterOption = new ForwardedOption<string>("--filter")
    {
        Description = CliCommandStrings.CmdTestCaseFilterDescription,
        HelpName = CliCommandStrings.CmdTestCaseFilterExpression
    }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={SurroundWithDoubleQuotes(o!)}");

    public static readonly Option<IEnumerable<string>> AdapterOption = new ForwardedOption<IEnumerable<string>>("--test-adapter-path")
    {
        Description = CliCommandStrings.CmdTestAdapterPathDescription,
        HelpName = CliCommandStrings.CmdTestAdapterPath
    }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath={SurroundWithDoubleQuotes(string.Join(";", o!.Select(CommandDirectoryContext.GetFullPath)))}")
    .AllowSingleArgPerToken();

    public static readonly Option<IEnumerable<string>> LoggerOption = new ForwardedOption<IEnumerable<string>>("--logger", "-l")
    {
        Description = CliCommandStrings.CmdLoggerDescription,
        HelpName = CliCommandStrings.CmdLoggerOption
    }.ForwardAsSingle(o =>
    {
        var loggersString = string.Join(";", GetSemiColonEscapedArgs(o!));

        return $"-property:VSTestLogger={SurroundWithDoubleQuotes(loggersString)}";
    })
    .AllowSingleArgPerToken();

    public static readonly Option<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.CmdOutputDescription,
        HelpName = CliCommandStrings.TestCmdOutputDir
    }
    .ForwardAsOutputPath("OutputPath", true);

    public static readonly Option<string> DiagOption = new ForwardedOption<string>("--diag", "-d")
    {
        Description = CliCommandStrings.CmdPathTologFileDescription,
        HelpName = CliCommandStrings.CmdPathToLogFile
    }
    .ForwardAsSingle(o => $"-property:VSTestDiag={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

    public static readonly Option<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
    {
        Description = CliCommandStrings.CmdNoBuildDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:VSTestNoBuild=true");

    public static readonly Option<string> ResultsOption = new ForwardedOption<string>("--results-directory")
    {
        Description = CliCommandStrings.CmdResultsDirectoryDescription,
        HelpName = CliCommandStrings.CmdPathToResultsDirectory
    }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

    public static readonly Option<IEnumerable<string>> CollectOption = new ForwardedOption<IEnumerable<string>>("--collect")
    {
        Description = CliCommandStrings.cmdCollectDescription,
        HelpName = CliCommandStrings.cmdCollectFriendlyName
    }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", GetSemiColonEscapedArgs(o!))}\"")
    .AllowSingleArgPerToken();

    public static readonly Option<bool> BlameOption = new ForwardedOption<bool>("--blame")
    {
        Description = CliCommandStrings.CmdBlameDescription,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled("-property:VSTestBlame=true");

    public static readonly Option<bool> BlameCrashOption = new ForwardedOption<bool>("--blame-crash")
    {
        Description = CliCommandStrings.CmdBlameCrashDescription,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled("-property:VSTestBlameCrash=true");

    public static readonly Option<string> BlameCrashDumpOption = CreateBlameCrashDumpOption();

    private static Option<string> CreateBlameCrashDumpOption()
    {
        Option<string> result = new ForwardedOption<string>("--blame-crash-dump-type")
        {
            Description = CliCommandStrings.CmdBlameCrashDumpTypeDescription,
            HelpName = CliCommandStrings.CrashDumpTypeArgumentName,
        }
        .ForwardAsMany(o => ["-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}"]);
        result.AcceptOnlyFromAmong(["full", "mini"]);
        return result;
    }

    public static readonly Option<bool> BlameCrashAlwaysOption = new ForwardedOption<bool>("--blame-crash-collect-always")
    {
        Description = CliCommandStrings.CmdBlameCrashCollectAlwaysDescription,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled(["-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true"]);

    public static readonly Option<bool> BlameHangOption = new ForwardedOption<bool>("--blame-hang")
    {
        Description = CliCommandStrings.CmdBlameHangDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:VSTestBlameHang=true");

    public static readonly Option<string> BlameHangDumpOption = CreateBlameHangDumpOption();

    private static Option<string> CreateBlameHangDumpOption()
    {
        Option<string> result = new ForwardedOption<string>("--blame-hang-dump-type")
        {
            Description = CliCommandStrings.CmdBlameHangDumpTypeDescription,
            HelpName = CliCommandStrings.HangDumpTypeArgumentName
        }
        .ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}"]);
        result.AcceptOnlyFromAmong(["full", "mini", "none"]);
        return result;
    }

    public static readonly Option<string> BlameHangTimeoutOption = new ForwardedOption<string>("--blame-hang-timeout")
    {
        Description = CliCommandStrings.CmdBlameHangTimeoutDescription,
        HelpName = CliCommandStrings.HangTimeoutArgumentName
    }.ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}"]);

    public static readonly Option<bool> NoLogoOption = new ForwardedOption<bool>("--nologo")
    {
        Description = CliCommandStrings.TestCmdNoLogo,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled("-property:VSTestNoLogo=true");

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public static readonly Option ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.VerbosityOption();
    public static readonly Option<string[]> VsTestTargetOption = CommonOptions.RequiredMSBuildTargetOption("VSTest");
    public static readonly Option<string[]> MTPTargetOption = CommonOptions.RequiredMSBuildTargetOption(CliConstants.MTPTarget);


    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    public static string GetTestRunnerName()
    {
        var builder = new ConfigurationBuilder();

        string? dotnetConfigPath = GetDotnetConfigPath(Environment.CurrentDirectory);
        if (!File.Exists(dotnetConfigPath))
        {
            return CliConstants.VSTest;
        }

        builder.AddIniFile(dotnetConfigPath);

        IConfigurationRoot config = builder.Build();
        var testSection = config.GetSection("dotnet.test.runner");

        if (!testSection.Exists())
        {
            return CliConstants.VSTest;
        }

        string? runnerNameSection = testSection["name"];

        if (string.IsNullOrEmpty(runnerNameSection))
        {
            return CliConstants.VSTest;
        }

        return runnerNameSection;
    }

    private static string? GetDotnetConfigPath(string? startDir)
    {
        string? directory = startDir;
        while (directory != null)
        {
            string dotnetConfigPath = Path.Combine(directory, "dotnet.config");
            if (File.Exists(dotnetConfigPath))
            {
                return dotnetConfigPath;
            }

            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    private static Command ConstructCommand()
    {
        string testRunnerName = GetTestRunnerName();

        if (testRunnerName.Equals(CliConstants.VSTest, StringComparison.OrdinalIgnoreCase))
        {
            return GetVSTestCliCommand();
        }
        else if (testRunnerName.Equals(CliConstants.MicrosoftTestingPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return GetTestingPlatformCliCommand();
        }

        throw new InvalidOperationException(string.Format(CliCommandStrings.CmdUnsupportedTestRunnerDescription, testRunnerName));
    }

    private static Command GetTestingPlatformCliCommand()
    {
        var command = new MicrosoftTestingPlatformTestCommand("test", CliCommandStrings.DotnetTestCommand);
        command.SetAction(parseResult => command.Run(parseResult));
        command.Options.Add(MicrosoftTestingPlatformOptions.ProjectOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.SolutionOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.TestModulesFilterOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ResultsDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ConfigFileOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MaxParallelTestModulesOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.PropertiesOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ConfigurationOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.FrameworkOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.RuntimeOption(CliCommandStrings.TestRuntimeOptionDescription));
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.NoRestoreOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoBuildOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoAnsiOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoProgressOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.OutputOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ListTestsOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoLaunchProfileOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.NoLaunchProfileArgumentsOption);
        command.Options.Add(MTPTargetOption);

        return command;
    }

    private static Command GetVSTestCliCommand()
    {
        DocumentedCommand command = new("test", DocsLink, CliCommandStrings.TestAppFullName)
        {
            TreatUnmatchedTokensAsErrors = false
        };

        // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
        // MSBuild command so we are letting it flow.

        command.Options.Add(SettingsOption);
        command.Options.Add(ListTestsOption);
        command.Options.Add(CommonOptions.EnvOption);
        command.Options.Add(FilterOption);
        command.Options.Add(AdapterOption);
        command.Options.Add(LoggerOption);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
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
        command.Options.Add(CommonOptions.RuntimeOption(CliCommandStrings.TestRuntimeOptionDescription));
        command.Options.Add(NoRestoreOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.PropertiesOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(VsTestTargetOption);
        command.SetAction(TestCommand.Run);

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
