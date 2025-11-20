// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestCommandDefinition
{
    public enum TestRunner
    {
        VSTest,
        MicrosoftTestingPlatform
    }

    private sealed class GlobalJsonModel
    {
        [JsonPropertyName("test")]
        public GlobalJsonTestNode Test { get; set; } = null!;
    }

    private sealed class GlobalJsonTestNode
    {
        [JsonPropertyName("runner")]
        public string RunnerName { get; set; } = null!;
    }

    public const string Name = "test";
    public static readonly string DocsLink = "https://aka.ms/dotnet-test";

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

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public static readonly Option ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.VerbosityOption();
    public static readonly Option<string[]> VsTestTargetOption = CommonOptions.RequiredMSBuildTargetOption("VSTest");
    public static readonly Option<string[]> MTPTargetOption = CommonOptions.RequiredMSBuildTargetOption(CliConstants.MTPTarget);

    public static TestRunner GetTestRunner()
    {
        string? globalJsonPath = GetGlobalJsonPath(Environment.CurrentDirectory);
        if (!File.Exists(globalJsonPath))
        {
            return TestRunner.VSTest;
        }

        string jsonText = File.ReadAllText(globalJsonPath);

        // This code path is hit exactly once during the whole life of the dotnet process.
        // So, no concern about caching JsonSerializerOptions.
        var globalJson = JsonSerializer.Deserialize<GlobalJsonModel>(jsonText, new JsonSerializerOptions()
        {
            AllowDuplicateProperties = false,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
        });

        var name = globalJson?.Test?.RunnerName;

        if (name is null || name.Equals(CliConstants.VSTest, StringComparison.OrdinalIgnoreCase))
        {
            return TestRunner.VSTest;
        }

        if (name.Equals(CliConstants.MicrosoftTestingPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return TestRunner.MicrosoftTestingPlatform;
        }

        throw new InvalidOperationException(string.Format(CliCommandStrings.CmdUnsupportedTestRunnerDescription, name));
    }

    private static string? GetGlobalJsonPath(string? startDir)
    {
        string? directory = startDir;
        while (directory != null)
        {
            string globalJsonPath = Path.Combine(directory, "global.json");
            if (File.Exists(globalJsonPath))
            {
                return globalJsonPath;
            }

            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    public static Command Create()
    {
        var command = new Command(Name);

        switch (GetTestRunner())
        {
            case TestRunner.VSTest:
                ConfigureVSTestCommand(command);
                break;

            case TestRunner.MicrosoftTestingPlatform:
                ConfigureTestingPlatformCommand(command);
                break;

            default:
                throw new InvalidOperationException();
        };

        return command;
    }

    public static void ConfigureTestingPlatformCommand(Command command)
    {
        command.Description = CliCommandStrings.DotnetTestCommandMTPDescription;
        command.Options.Add(MicrosoftTestingPlatformOptions.ProjectOrSolutionOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.SolutionOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.TestModulesFilterOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.TestModulesRootDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ResultsDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.ConfigFileOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.DiagnosticOutputDirectoryOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MaxParallelTestModulesOption);
        command.Options.Add(MicrosoftTestingPlatformOptions.MinimumExpectedTestsOption);
        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.EnvOption);
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
    }

    public static void ConfigureVSTestCommand(Command command)
    {
        command.Description = CliCommandStrings.DotnetTestCommandVSTestDescription;
        command.TreatUnmatchedTokensAsErrors = false;
        command.DocsLink = DocsLink;

        // We are on purpose not capturing the solution, project or directory here. We want to pass it to the
        // MSBuild command so we are letting it flow.

        command.Options.Add(SettingsOption);
        command.Options.Add(ListTestsOption);
        command.Options.Add(CommonOptions.TestEnvOption);
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
