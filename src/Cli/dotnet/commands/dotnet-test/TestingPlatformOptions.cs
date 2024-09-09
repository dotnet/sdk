// Licensed to the .NET Foundation under one or more agreements.
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

        public static readonly CliOption<string> AdditionalMSBuildParametersOption = new("--additional-msbuild-parameters")
        {
            Description = LocalizableStrings.CmdAdditionalMSBuildParametersDescription,
        };

        public static readonly CliOption<string> TestModulesFilterOption = new("--test-modules")
        {
            Description = LocalizableStrings.CmdTestModulesDescription
        };

        public static readonly CliOption<string> TestModulesRootDirectoryOption = new("--root-directory")
        {
            Description = LocalizableStrings.CmdTestModulesRootDirectoryDescription
        };

        public static readonly CliOption<string> NoBuildOption = new("--no-build")
        {
            Description = LocalizableStrings.CmdNoBuildDescription,
            Arity = ArgumentArity.Zero
        };

        public static readonly CliOption<string> NoRestoreOption = new("--no-restore")
        {
            Description = LocalizableStrings.CmdNoRestoreDescription,
            Arity = ArgumentArity.Zero
        };

        public static readonly CliOption<string> ArchitectureOption = new("--arch")
        {
            Description = LocalizableStrings.CmdArchitectureDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly CliOption<string> ConfigurationOption = new("--configuration")
        {
            Description = LocalizableStrings.CmdConfigurationDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly CliOption<string> ProjectOption = new("--project")
        {
            Description = LocalizableStrings.CmdProjectDescription,
            Arity = ArgumentArity.ExactlyOne
        };
    }
}
