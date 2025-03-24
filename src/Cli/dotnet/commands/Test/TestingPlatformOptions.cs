// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli;

internal static class TestingPlatformOptions
{
    public static readonly CliOption<string> ProjectOption = new("--project")
    {
        Description = LocalizableStrings.CmdProjectDescription,
        HelpName = LocalizableStrings.CmdProjectPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliOption<string> SolutionOption = new("--solution")
    {
        Description = LocalizableStrings.CmdSolutionDescription,
        HelpName = LocalizableStrings.CmdSolutionPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliOption<string> DirectoryOption = new("--directory")
    {
        Description = LocalizableStrings.CmdDirectoryDescription,
        HelpName = LocalizableStrings.CmdDirectoryPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliOption<string> TestModulesFilterOption = new("--test-modules")
    {
        Description = LocalizableStrings.CmdTestModulesDescription,
        HelpName = LocalizableStrings.CmdExpressionName
    };

    public static readonly CliOption<string> TestModulesRootDirectoryOption = new("--root-directory")
    {
        Description = LocalizableStrings.CmdTestModulesRootDirectoryDescription,
        HelpName = LocalizableStrings.CmdRootPathName,
    };

    public static readonly CliOption<string> MaxParallelTestModulesOption = new("--max-parallel-test-modules")
    {
        Description = LocalizableStrings.CmdMaxParallelTestModulesDescription,
        HelpName = LocalizableStrings.CmdNumberName
    };

    public static readonly CliOption<string> ConfigurationOption = CommonOptions.ConfigurationOption(LocalizableStrings.ConfigurationOptionDescription);

    public static readonly CliOption<string> FrameworkOption = CommonOptions.FrameworkOption(LocalizableStrings.FrameworkOptionDescription);

    public static readonly CliOption<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
    {
        Description = LocalizableStrings.CmdNoBuildDescription
    }.ForwardAs("-property:MTPNoBuild=true");

    public static readonly CliOption<bool> NoAnsiOption = new("--no-ansi")
    {
        Description = LocalizableStrings.CmdNoAnsiDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> NoProgressOption = new("--no-progress")
    {
        Description = LocalizableStrings.CmdNoProgressDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<OutputOptions> OutputOption = new("--output")
    {
        Description = LocalizableStrings.CmdTestOutputDescription,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly CliOption<string> ListTestsOption = new("--list-tests")
    {
        Description = LocalizableStrings.CmdListTestsDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<string> HelpOption = new("--help", ["-h", "-?"])
    {
        Arity = ArgumentArity.Zero
    };
}

internal enum OutputOptions
{
    Normal,
    Detailed
}
