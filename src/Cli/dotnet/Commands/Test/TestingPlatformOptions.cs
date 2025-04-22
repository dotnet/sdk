﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class TestingPlatformOptions
{
    public static readonly Option<string> ProjectOption = new("--project")
    {
        Description = CliCommandStrings.CmdProjectDescription,
        HelpName = CliCommandStrings.CmdProjectPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> SolutionOption = new("--solution")
    {
        Description = CliCommandStrings.CmdSolutionDescription,
        HelpName = CliCommandStrings.CmdSolutionPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> DirectoryOption = new("--directory")
    {
        Description = CliCommandStrings.CmdDirectoryDescription,
        HelpName = CliCommandStrings.CmdDirectoryPathName,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> TestModulesFilterOption = new("--test-modules")
    {
        Description = CliCommandStrings.CmdTestModulesDescription,
        HelpName = CliCommandStrings.CmdExpressionName
    };

    public static readonly Option<string> TestModulesRootDirectoryOption = new("--root-directory")
    {
        Description = CliCommandStrings.CmdTestModulesRootDirectoryDescription,
        HelpName = CliCommandStrings.CmdRootPathName,
    };

    public static readonly Option<string> MaxParallelTestModulesOption = new("--max-parallel-test-modules")
    {
        Description = CliCommandStrings.CmdMaxParallelTestModulesDescription,
        HelpName = CliCommandStrings.CmdNumberName
    };

    public static readonly Option<string> ConfigurationOption = CommonOptions.ConfigurationOption(CliCommandStrings.TestConfigurationOptionDescription);

    public static readonly Option<string> FrameworkOption = CommonOptions.FrameworkOption(CliCommandStrings.TestFrameworkOptionDescription);

    public static readonly Option<bool> NoBuildOption = new("--no-build")
    {
        Description = CliCommandStrings.CmdNoBuildDescription
    };

    public static readonly Option<bool> NoAnsiOption = new("--no-ansi")
    {
        Description = CliCommandStrings.CmdNoAnsiDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile")
    {
        Description = CliCommandStrings.CommandOptionNoLaunchProfileDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> NoLaunchProfileArgumentsOption = new("--no-launch-profile-arguments")
    {
        Description = CliCommandStrings.CommandOptionNoLaunchProfileArgumentsDescription
    };

    public static readonly Option<bool> NoProgressOption = new("--no-progress")
    {
        Description = CliCommandStrings.CmdNoProgressDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<OutputOptions> OutputOption = new("--output")
    {
        Description = CliCommandStrings.CmdTestOutputDescription,
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string> ListTestsOption = new("--list-tests")
    {
        Description = CliCommandStrings.CmdListTestsDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<string> HelpOption = new("--help", ["-h", "-?"])
    {
        Arity = ArgumentArity.Zero
    };
}

internal enum OutputOptions
{
    Normal,
    Detailed
}
