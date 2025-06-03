// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

internal static class ValidationUtility
{
    public static void ValidateMutuallyExclusiveOptions(ParseResult parseResult)
    {
        ValidatePathOptions(parseResult);
        ValidateOptionsIrrelevantToModulesFilter(parseResult);

        static void ValidatePathOptions(ParseResult parseResult)
        {
            var count = 0;
            if (parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption))
                count++;

            if (parseResult.HasOption(TestingPlatformOptions.DirectoryOption))
                count++;

            if (parseResult.HasOption(TestingPlatformOptions.SolutionOption))
                count++;

            if (parseResult.HasOption(TestingPlatformOptions.ProjectOption))
                count++;

            if (count > 1)
                throw new GracefulException(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);
        }

        static void ValidateOptionsIrrelevantToModulesFilter(ParseResult parseResult)
        {
            if (!parseResult.HasOption(TestingPlatformOptions.TestModulesFilterOption))
            {
                return;
            }

            if (parseResult.HasOption(CommonOptions.ArchitectureOption) ||
                parseResult.HasOption(TestingPlatformOptions.ConfigurationOption) ||
                parseResult.HasOption(TestingPlatformOptions.FrameworkOption) ||
                parseResult.HasOption(CommonOptions.OperatingSystemOption) ||
                parseResult.HasOption(CommonOptions.RuntimeOptionName))
            {
                throw new GracefulException(CliCommandStrings.CmdOptionCannotBeUsedWithTestModulesDescription);
            }
        }
    }
    public static bool ValidateBuildPathOptions(BuildOptions buildPathOptions, TerminalTestReporter output)
    {
        PathOptions pathOptions = buildPathOptions.PathOptions;

        if (!string.IsNullOrEmpty(pathOptions.ProjectPath))
        {
            return ValidateProjectFilePath(pathOptions.ProjectPath, output);
        }

        if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            return ValidateSolutionFilePath(pathOptions.SolutionPath, output);
        }

        if (!string.IsNullOrEmpty(pathOptions.DirectoryPath) && !Directory.Exists(pathOptions.DirectoryPath))
        {
            output.WriteMessage(string.Format(CliCommandStrings.CmdNonExistentDirectoryErrorDescription, pathOptions.DirectoryPath));
            return false;
        }

        return true;
    }

    private static bool ValidateSolutionFilePath(string filePath, TerminalTestReporter output)
    {
        if (!CliConstants.SolutionExtensions.Contains(Path.GetExtension(filePath)))
        {
            output.WriteMessage(string.Format(CliCommandStrings.CmdInvalidSolutionFileExtensionErrorDescription, filePath));
            return false;
        }

        return ValidateFilePathExists(filePath, output);
    }

    private static bool ValidateProjectFilePath(string filePath, TerminalTestReporter output)
    {
        if (!Path.GetExtension(filePath).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
        {
            output.WriteMessage(string.Format(CliCommandStrings.CmdInvalidProjectFileExtensionErrorDescription, filePath));
            return false;
        }

        return ValidateFilePathExists(filePath, output);
    }

    private static bool ValidateFilePathExists(string filePath, TerminalTestReporter output)
    {
        if (!File.Exists(filePath))
        {
            output.WriteMessage(string.Format(CliCommandStrings.CmdNonExistentFileErrorDescription, Path.GetFullPath(filePath)));
            return false;
        }

        return true;
    }
}
