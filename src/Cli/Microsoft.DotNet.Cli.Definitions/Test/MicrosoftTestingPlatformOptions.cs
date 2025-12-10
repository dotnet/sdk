// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class MicrosoftTestingPlatformOptions
{
    public static readonly Option<string> ProjectOrSolutionOption = new("--project")
    {
        Description = CliDefinitionResources.CmdProjectOrSolutionDescriptionFormat,
        HelpName = CliDefinitionResources.CmdProjectOrSolutionPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> SolutionOption = new("--solution")
    {
        Description = CliDefinitionResources.CmdSolutionDescription,
        HelpName = CliDefinitionResources.CmdSolutionPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> TestModulesFilterOption = new("--test-modules")
    {
        Description = CliDefinitionResources.CmdTestModulesDescription,
        HelpName = CliDefinitionResources.CmdExpressionName
    };

    public static readonly Option<string> TestModulesRootDirectoryOption = new("--root-directory")
    {
        Description = CliDefinitionResources.CmdTestModulesRootDirectoryDescription,
        HelpName = CliDefinitionResources.CmdRootPathName,
    };

    public static readonly Option<string> ResultsDirectoryOption = new("--results-directory")
    {
        Description = CliDefinitionResources.CmdResultsDirectoryDescription,
        HelpName = CliDefinitionResources.CmdPathToResultsDirectory,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> ConfigFileOption = new("--config-file")
    {
        Description = CliDefinitionResources.CmdConfigFileDescription,
        HelpName = CliDefinitionResources.CmdConfigFilePath,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> DiagnosticOutputDirectoryOption = new("--diagnostic-output-directory")
    {
        Description = CliDefinitionResources.CmdDiagnosticOutputDirectoryDescription,
        HelpName = CliDefinitionResources.CmdDiagnosticOutputDirectoryPath,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<int> MaxParallelTestModulesOption = new("--max-parallel-test-modules")
    {
        Description = CliDefinitionResources.CmdMaxParallelTestModulesDescription,
        HelpName = CliDefinitionResources.CmdNumberName
    };

    public static readonly Option<int> MinimumExpectedTestsOption = new("--minimum-expected-tests")
    {
        Description = CliDefinitionResources.CmdMinimumExpectedTestsDescription,
        HelpName = CliDefinitionResources.CmdNumberName
    };

    public static readonly Option<bool> NoBuildOption = new("--no-build")
    {
        Description = CliDefinitionResources.CmdNoBuildDescription
    };

    public static readonly Option<bool> NoAnsiOption = new("--no-ansi")
    {
        Description = CliDefinitionResources.CmdNoAnsiDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
    {
        Description = CliDefinitionResources.CommandOptionNoLaunchProfileDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
    {
        Description = CliDefinitionResources.CommandOptionNoLaunchProfileArgumentsDescription
    };

    public static readonly Option<bool> NoProgressOption = new("--no-progress")
    {
        Description = CliDefinitionResources.CmdNoProgressDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<OutputOptions> OutputOption = new("--output")
    {
        Description = CliDefinitionResources.CmdTestOutputDescription,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> ListTestsOption = new("--list-tests")
    {
        Description = CliDefinitionResources.CmdListTestsDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string[]> MTPTargetOption = CommonOptions.RequiredMSBuildTargetOption(CliConstants.MTPTarget);
}

internal enum OutputOptions
{
    Normal,
    Detailed
}
