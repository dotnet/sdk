// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Test;

namespace Microsoft.DotNet.Cli
{
    internal static class TestingPlatformOptions
    {
        public static readonly Option<string> MaxParallelTestModulesOption = new("--max-parallel-test-modules", "-mptm")
        {
            Description = LocalizableStrings.CmdMaxParallelTestModulesDescription,
        };

        public static readonly Option<string> AdditionalMSBuildParametersOption = new("--additional-msbuild-parameters")
        {
            Description = LocalizableStrings.CmdAdditionalMSBuildParametersDescription,
        };

        public static readonly Option<string> TestModulesFilterOption = new("--test-modules")
        {
            Description = LocalizableStrings.CmdTestModulesDescription
        };

        public static readonly Option<string> TestModulesRootDirectoryOption = new("--root-directory")
        {
            Description = LocalizableStrings.CmdTestModulesRootDirectoryDescription
        };

        public static readonly Option<string> NoBuildOption = new("--no-build")
        {
            Description = LocalizableStrings.CmdNoBuildDescription,
            Arity = ArgumentArity.Zero
        };

        public static readonly Option<string> NoRestoreOption = new("--no-restore")
        {
            Description = LocalizableStrings.CmdNoRestoreDescription,
            Arity = ArgumentArity.Zero
        };

        public static readonly Option<string> ArchitectureOption = new("--arch")
        {
            Description = LocalizableStrings.CmdArchitectureDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string> ConfigurationOption = new("--configuration")
        {
            Description = LocalizableStrings.CmdConfigurationDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string> ProjectOption = new("--project")
        {
            Description = LocalizableStrings.CmdProjectDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string> ListTestsOption = new("--list-tests")
        {
            Description = LocalizableStrings.CmdListTestsDescription,
            Arity = ArgumentArity.Zero
        };

        public static readonly Option<string> SolutionOption = new("--solution")
        {
            Description = LocalizableStrings.CmdSolutionDescription,
            Arity = ArgumentArity.ExactlyOne
        };

        public static readonly Option<string> DirectoryOption = new("--directory")
        {
            Description = LocalizableStrings.CmdDirectoryDescription,
            Arity = ArgumentArity.ExactlyOne
        };
    }
}
