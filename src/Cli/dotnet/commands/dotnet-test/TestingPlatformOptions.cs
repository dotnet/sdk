﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal static class TestingPlatformOptions
    {
        public static readonly CliOption<string> MaxParallelTestModulesOption = new("--max-parallel-test-modules", "-mptm")
        {
            Description = LocalizableStrings.CmdMaxParallelTestModulesDescription,
        };

        public static readonly CliOption<string> TestModulesFilterOption = new("--test-modules")
        {
            Description = LocalizableStrings.CmdTestModulesDescription
        };

        public static readonly CliOption<string> TestModulesRootDirectoryOption = new("--root-directory")
        {
            Description = LocalizableStrings.CmdTestModulesRootDirectoryDescription
        };

        public static readonly CliOption<bool> NoBuildOption = new ForwardedOption<bool>("--no-build")
        {
            Description = LocalizableStrings.CmdNoBuildDescription
        }.ForwardAs("-property:MTPNoBuild=true");

        public static readonly CliOption<string> ArchitectureOption = new ForwardedOption<string>("--arch", "-a")
        {
            Description = LocalizableStrings.CmdArchitectureDescription,
            Arity = ArgumentArity.ExactlyOne
        }.SetForwardingFunction(CommonOptions.ResolveArchOptionToRuntimeIdentifier);

        public static readonly CliOption<string> ConfigurationOption = new ForwardedOption<string>("--configuration", "-c")
        {
            Description = LocalizableStrings.CmdConfigurationDescription,
            Arity = ArgumentArity.ExactlyOne
        }.ForwardAsSingle(p => $"/p:configuration={p}");

        public static readonly CliOption<string> ProjectOption = new("--project")
        {
            Description = LocalizableStrings.CmdProjectDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly CliOption<string> ListTestsOption = new("--list-tests")
        {
            Description = LocalizableStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
        };

        public static readonly CliOption<string> SolutionOption = new("--solution")
        {
            Description = LocalizableStrings.CmdSolutionDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly CliOption<string> DirectoryOption = new("--directory")
        {
            Description = LocalizableStrings.CmdDirectoryDescription,
            Arity = ArgumentArity.ExactlyOne
        };

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
    }

    internal enum OutputOptions
    {
        Normal,
        Detailed
    }
}
