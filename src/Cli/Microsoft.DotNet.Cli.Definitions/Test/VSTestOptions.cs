// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class VSTestOptions
{
    public static readonly Option<string> SettingsOption = new Option<string>("--settings", "-s")
    {
        Description = CliCommandStrings.CmdSettingsDescription,
        HelpName = CliCommandStrings.CmdSettingsFile
    }.ForwardAsSingle(o => $"-property:VSTestSetting={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

    public static readonly Option<bool> ListTestsOption = new Option<bool>("--list-tests", "-t")
    {
        Description = CliCommandStrings.CmdListTestsDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:VSTestListTests=true");

    public static readonly Option<string> FilterOption = new Option<string>("--filter")
    {
        Description = CliCommandStrings.CmdTestCaseFilterDescription,
        HelpName = CliCommandStrings.CmdTestCaseFilterExpression
    }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={SurroundWithDoubleQuotes(o!)}");

    public static readonly Option<IEnumerable<string>> AdapterOption = new Option<IEnumerable<string>>("--test-adapter-path")
    {
        Description = CliCommandStrings.CmdTestAdapterPathDescription,
        HelpName = CliCommandStrings.CmdTestAdapterPath
    }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath={SurroundWithDoubleQuotes(string.Join(";", o!.Select(CommandDirectoryContext.GetFullPath)))}")
    .AllowSingleArgPerToken();

    public static readonly Option<IEnumerable<string>> LoggerOption = new Option<IEnumerable<string>>("--logger", "-l")
    {
        Description = CliCommandStrings.CmdLoggerDescription,
        HelpName = CliCommandStrings.CmdLoggerOption
    }.ForwardAsSingle(o =>
    {
        var loggersString = string.Join(";", GetSemiColonEscapedArgs(o!));

        return $"-property:VSTestLogger={SurroundWithDoubleQuotes(loggersString)}";
    })
    .AllowSingleArgPerToken();

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliCommandStrings.CmdOutputDescription,
        HelpName = CliCommandStrings.TestCmdOutputDir
    }
    .ForwardAsOutputPath("OutputPath", true);

    public static readonly Option<string> DiagOption = new Option<string>("--diag", "-d")
    {
        Description = CliCommandStrings.CmdPathTologFileDescription,
        HelpName = CliCommandStrings.CmdPathToLogFile
    }
    .ForwardAsSingle(o => $"-property:VSTestDiag={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

    public static readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
    {
        Description = CliCommandStrings.CmdNoBuildDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:VSTestNoBuild=true");

    public static readonly Option<string> ResultsOption = new Option<string>("--results-directory")
    {
        Description = CliCommandStrings.CmdResultsDirectoryDescription,
        HelpName = CliCommandStrings.CmdPathToResultsDirectory
    }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

    public static readonly Option<IEnumerable<string>> CollectOption = new Option<IEnumerable<string>>("--collect")
    {
        Description = CliCommandStrings.cmdCollectDescription,
        HelpName = CliCommandStrings.cmdCollectFriendlyName
    }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", GetSemiColonEscapedArgs(o!))}\"")
    .AllowSingleArgPerToken();

    public static readonly Option<bool> BlameOption = new Option<bool>("--blame")
    {
        Description = CliCommandStrings.CmdBlameDescription,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled("-property:VSTestBlame=true");

    public static readonly Option<bool> BlameCrashOption = new Option<bool>("--blame-crash")
    {
        Description = CliCommandStrings.CmdBlameCrashDescription,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled("-property:VSTestBlameCrash=true");

    public static readonly Option<string> BlameCrashDumpOption = CreateBlameCrashDumpOption();

    private static Option<string> CreateBlameCrashDumpOption()
    {
        Option<string> result = new Option<string>("--blame-crash-dump-type")
        {
            Description = CliCommandStrings.CmdBlameCrashDumpTypeDescription,
            HelpName = CliCommandStrings.CrashDumpTypeArgumentName,
        }
        .ForwardAsMany(o => ["-property:VSTestBlameCrash=true", $"-property:VSTestBlameCrashDumpType={o}"]);
        result.AcceptOnlyFromAmong(["full", "mini"]);
        return result;
    }

    public static readonly Option<bool> BlameCrashAlwaysOption = new Option<bool>("--blame-crash-collect-always")
    {
        Description = CliCommandStrings.CmdBlameCrashCollectAlwaysDescription,
        Arity = ArgumentArity.Zero
    }.ForwardIfEnabled(["-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true"]);

    public static readonly Option<bool> BlameHangOption = new Option<bool>("--blame-hang")
    {
        Description = CliCommandStrings.CmdBlameHangDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:VSTestBlameHang=true");

    public static readonly Option<string> BlameHangDumpOption = CreateBlameHangDumpOption();

    private static Option<string> CreateBlameHangDumpOption()
    {
        Option<string> result = new Option<string>("--blame-hang-dump-type")
        {
            Description = CliCommandStrings.CmdBlameHangDumpTypeDescription,
            HelpName = CliCommandStrings.HangDumpTypeArgumentName
        }
        .ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangDumpType={o}"]);
        result.AcceptOnlyFromAmong(["full", "mini", "none"]);
        return result;
    }

    public static readonly Option<string> BlameHangTimeoutOption = new Option<string>("--blame-hang-timeout")
    {
        Description = CliCommandStrings.CmdBlameHangTimeoutDescription,
        HelpName = CliCommandStrings.HangTimeoutArgumentName
    }.ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}"]);

    public static readonly Option<bool> NoLogoOption = CommonOptions.NoLogoOption(forwardAs: "--property:VSTestNoLogo=true", description: CliCommandStrings.TestCmdNoLogo);

    public static readonly Option<string[]> VsTestTargetOption = CommonOptions.RequiredMSBuildTargetOption("VSTest");

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
