// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.Help;
using Command = System.CommandLine.Command;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal abstract class TestCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-test";

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public TestCommandDefinition(string description)
        : base("test", description)
    {
        this.DocsLink = Link;
        TreatUnmatchedTokensAsErrors = false;
    }

    public static TestCommandDefinition Create()
    {
        string? globalJsonPath = GetGlobalJsonPath(Environment.CurrentDirectory);
        if (!File.Exists(globalJsonPath))
        {
            return new VSTest();
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
            return new VSTest();
        }

        if (name.Equals(CliConstants.MicrosoftTestingPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return new MicrosoftTestingPlatform();
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

    public sealed class MicrosoftTestingPlatform : TestCommandDefinition, ICustomHelp
    {
        public readonly Option<string> ProjectOrSolutionOption = new("--project")
        {
            Description = CliCommandStrings.CmdProjectOrSolutionDescriptionFormat,
            HelpName = CliCommandStrings.CmdProjectOrSolutionPathName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> SolutionOption = new("--solution")
        {
            Description = CliCommandStrings.CmdSolutionDescription,
            HelpName = CliCommandStrings.CmdSolutionPathName,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<string> TestModulesFilterOption = new("--test-modules")
        {
            Description = CliCommandStrings.CmdTestModulesDescription,
            HelpName = CliCommandStrings.CmdExpressionName
        };

        public readonly Option<string> TestModulesRootDirectoryOption = new("--root-directory")
        {
            Description = CliCommandStrings.CmdTestModulesRootDirectoryDescription,
            HelpName = CliCommandStrings.CmdRootPathName,
        };

        public const string ResultsDirectoryOptionName = "--results-directory";

        public readonly Option<string> ResultsDirectoryOption = new(ResultsDirectoryOptionName)
        {
            Description = CliCommandStrings.CmdResultsDirectoryDescription,
            HelpName = CliCommandStrings.CmdPathToResultsDirectory,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string ConfigFileOptionName = "--config-file";

        public readonly Option<string> ConfigFileOption = new(ConfigFileOptionName)
        {
            Description = CliCommandStrings.CmdConfigFileDescription,
            HelpName = CliCommandStrings.CmdConfigFilePath,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string DiagnosticOutputDirectoryOptionName = "--diagnostic-output-directory";

        public readonly Option<string> DiagnosticOutputDirectoryOption = new(DiagnosticOutputDirectoryOptionName)
        {
            Description = CliCommandStrings.CmdDiagnosticOutputDirectoryDescription,
            HelpName = CliCommandStrings.CmdDiagnosticOutputDirectoryPath,
            Arity = ArgumentArity.ExactlyOne
        };

        public readonly Option<int> MaxParallelTestModulesOption = new("--max-parallel-test-modules")
        {
            Description = CliCommandStrings.CmdMaxParallelTestModulesDescription,
            HelpName = CliCommandStrings.CmdNumberName
        };

        public readonly Option<int> MinimumExpectedTestsOption = new("--minimum-expected-tests")
        {
            Description = CliCommandStrings.CmdMinimumExpectedTestsDescription,
            HelpName = CliCommandStrings.CmdNumberName
        };

        public readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;

        public readonly Option<IReadOnlyDictionary<string, string>> EnvOption = CommonOptions.EnvOption;

        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.PropertiesOption;

        public readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;

        public readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.TestRuntimeOptionDescription);

        public readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public readonly Option<bool> NoBuildOption = new("--no-build")
        {
            Description = CliCommandStrings.CmdNoBuildDescription
        };

        public readonly Option<bool> NoAnsiOption = new("--no-ansi")
        {
            Description = CliCommandStrings.CmdNoAnsiDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoProgressOption = new("--no-progress")
        {
            Description = CliCommandStrings.CmdNoProgressDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<OutputOptions> OutputOption = new("--output")
        {
            Description = CliCommandStrings.CmdTestOutputDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public const string ListTestsOptionName = "--list-tests";

        public readonly Option<string> ListTestsOption = new(ListTestsOptionName)
        {
            Description = CliCommandStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
        {
            Description = CliCommandStrings.CommandOptionNoLaunchProfileDescription,
            Arity = ArgumentArity.Zero
        };

        public readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
        {
            Description = CliCommandStrings.CommandOptionNoLaunchProfileArgumentsDescription
        };

        public readonly Option<string[]> MTPTargetOption = CommonOptions.CreateRequiredMSBuildTargetOption(CliConstants.MTPTarget);

        public ICustomHelp? CustomHelpLayoutProvider { get; set; }

        public MicrosoftTestingPlatform()
            : base(CliCommandStrings.DotnetTestCommandMTPDescription)
        {
            Options.Add(ProjectOrSolutionOption);
            Options.Add(SolutionOption);
            Options.Add(TestModulesFilterOption);
            Options.Add(TestModulesRootDirectoryOption);
            Options.Add(ResultsDirectoryOption);
            Options.Add(ConfigFileOption);
            Options.Add(DiagnosticOutputDirectoryOption);
            Options.Add(MaxParallelTestModulesOption);
            Options.Add(MinimumExpectedTestsOption);
            Options.Add(ArchitectureOption);
            Options.Add(EnvOption);
            Options.Add(PropertiesOption);
            Options.Add(ConfigurationOption);
            Options.Add(FrameworkOption);
            Options.Add(OperatingSystemOption);
            Options.Add(RuntimeOption);
            Options.Add(VerbosityOption);
            Options.Add(NoRestoreOption);
            Options.Add(NoBuildOption);
            Options.Add(NoAnsiOption);
            Options.Add(NoProgressOption);
            Options.Add(OutputOption);
            Options.Add(ListTestsOption);
            Options.Add(NoLaunchProfileOption);
            Options.Add(NoLaunchProfileArgumentsOption);
            Options.Add(MTPTargetOption);
        }

        public IEnumerable<Action<HelpContext>> CustomHelpLayout()
            => CustomHelpLayoutProvider?.CustomHelpLayout() ?? [];
    }

    public sealed class VSTest : TestCommandDefinition
    {
        public readonly Option<string> SettingsOption = new Option<string>("--settings", "-s")
        {
            Description = CliCommandStrings.CmdSettingsDescription,
            HelpName = CliCommandStrings.CmdSettingsFile
        }.ForwardAsSingle(o => $"-property:VSTestSetting={VSTestOptions.SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public readonly Option<bool> ListTestsOption = new Option<bool>("--list-tests", "-t")
        {
            Description = CliCommandStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:VSTestListTests=true");

        public readonly Option<IReadOnlyDictionary<string, string>> TestEnvOption = CommonOptions.TestEnvOption;

        public readonly Option<string> FilterOption = new Option<string>("--filter")
        {
            Description = CliCommandStrings.CmdTestCaseFilterDescription,
            HelpName = CliCommandStrings.CmdTestCaseFilterExpression
        }.ForwardAsSingle(o => $"-property:VSTestTestCaseFilter={VSTestOptions.SurroundWithDoubleQuotes(o!)}");

        public readonly Option<IEnumerable<string>> AdapterOption = new Option<IEnumerable<string>>("--test-adapter-path")
        {
            Description = CliCommandStrings.CmdTestAdapterPathDescription,
            HelpName = CliCommandStrings.CmdTestAdapterPath
        }.ForwardAsSingle(o => $"-property:VSTestTestAdapterPath={VSTestOptions.SurroundWithDoubleQuotes(string.Join(";", o!.Select(CommandDirectoryContext.GetFullPath)))}")
        .AllowSingleArgPerToken();

        public readonly Option<IEnumerable<string>> LoggerOption = new Option<IEnumerable<string>>("--logger", "-l")
        {
            Description = CliCommandStrings.CmdLoggerDescription,
            HelpName = CliCommandStrings.CmdLoggerOption
        }.ForwardAsSingle(o =>
        {
            var loggersString = string.Join(";", VSTestOptions.GetSemiColonEscapedArgs(o!));
            return $"-property:VSTestLogger={VSTestOptions.SurroundWithDoubleQuotes(loggersString)}";
        })
        .AllowSingleArgPerToken();

        public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
        {
            Description = CliCommandStrings.CmdOutputDescription,
            HelpName = CliCommandStrings.TestCmdOutputDir
        }
        .ForwardAsOutputPath("OutputPath", true);

        public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();

        public readonly Option<string> DiagOption = new Option<string>("--diag", "-d")
        {
            Description = CliCommandStrings.CmdPathTologFileDescription,
            HelpName = CliCommandStrings.CmdPathToLogFile
        }
        .ForwardAsSingle(o => $"-property:VSTestDiag={VSTestOptions.SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
        {
            Description = CliCommandStrings.CmdNoBuildDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:VSTestNoBuild=true");

        public readonly Option<string> ResultsOption = new Option<string>("--results-directory")
        {
            Description = CliCommandStrings.CmdResultsDirectoryDescription,
            HelpName = CliCommandStrings.CmdPathToResultsDirectory
        }.ForwardAsSingle(o => $"-property:VSTestResultsDirectory={VSTestOptions.SurroundWithDoubleQuotes(CommandDirectoryContext.GetFullPath(o))}");

        public readonly Option<IEnumerable<string>> CollectOption = new Option<IEnumerable<string>>("--collect")
        {
            Description = CliCommandStrings.cmdCollectDescription,
            HelpName = CliCommandStrings.cmdCollectFriendlyName
        }.ForwardAsSingle(o => $"-property:VSTestCollect=\"{string.Join(";", VSTestOptions.GetSemiColonEscapedArgs(o!))}\"")
        .AllowSingleArgPerToken();

        public readonly Option<bool> BlameOption = new Option<bool>("--blame")
        {
            Description = CliCommandStrings.CmdBlameDescription,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled("-property:VSTestBlame=true");

        public readonly Option<bool> BlameCrashOption = new Option<bool>("--blame-crash")
        {
            Description = CliCommandStrings.CmdBlameCrashDescription,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled("-property:VSTestBlameCrash=true");

        public readonly Option<string> BlameCrashDumpOption = CreateBlameCrashDumpOption();

        public readonly Option<bool> BlameCrashAlwaysOption = new Option<bool>("--blame-crash-collect-always")
        {
            Description = CliCommandStrings.CmdBlameCrashCollectAlwaysDescription,
            Arity = ArgumentArity.Zero
        }.ForwardIfEnabled(["-property:VSTestBlameCrash=true", "-property:VSTestBlameCrashCollectAlways=true"]);

        public readonly Option<bool> BlameHangOption = new Option<bool>("--blame-hang")
        {
            Description = CliCommandStrings.CmdBlameHangDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:VSTestBlameHang=true");

        public readonly Option<string> BlameHangDumpOption = CreateBlameHangDumpOption();

        public readonly Option<string> BlameHangTimeoutOption = new Option<string>("--blame-hang-timeout")
        {
            Description = CliCommandStrings.CmdBlameHangTimeoutDescription,
            HelpName = CliCommandStrings.HangTimeoutArgumentName
        }.ForwardAsMany(o => ["-property:VSTestBlameHang=true", $"-property:VSTestBlameHangTimeout={o}"]);

        public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption(forwardAs: "--property:VSTestNoLogo=true", description: CliCommandStrings.TestCmdNoLogo);

        public readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.TestRuntimeOptionDescription);

        public readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

        public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();

        public readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;

        public readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;

        public readonly Option<ReadOnlyDictionary<string, string>?> PropertiesOption = CommonOptions.PropertiesOption;

        public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();

        public readonly Option<string[]> VsTestTargetOption = CommonOptions.CreateRequiredMSBuildTargetOption("VSTest");

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

        public VSTest()
            : base(CliCommandStrings.DotnetTestCommandVSTestDescription)
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
            Options.Add(RuntimeOption);
            Options.Add(NoRestoreOption);
            Options.Add(InteractiveOption);
            Options.Add(VerbosityOption);
            Options.Add(ArchitectureOption);
            Options.Add(OperatingSystemOption);
            Options.Add(PropertiesOption);
            Options.Add(DisableBuildServersOption);
            Options.Add(VsTestTargetOption);
        }
    }
}
