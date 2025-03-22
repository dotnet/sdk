// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

using LocalizableStrings = Microsoft.DotNet.Tools.Test.LocalizableStrings;

namespace Microsoft.DotNet.Cli;

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
                throw new GracefulException(LocalizableStrings.CmdMultipleBuildPathOptionsErrorDescription);
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
                parseResult.HasOption(CommonOptions.RuntimeOption)
                )
            {
                throw new GracefulException(LocalizableStrings.CmdOptionCannotBeUsedWithTestModulesDescription);
            }
        }
    }
    public static bool ValidateBuildPathOptions(BuildOptions buildPathOptions, TerminalTestReporter output)
    {
        PathOptions pathOptions = buildPathOptions.PathOptions;

        if (!string.IsNullOrEmpty(pathOptions.ProjectPath))
        {
            return ValidateFilePath(pathOptions.ProjectPath, CliConstants.ProjectExtensions, LocalizableStrings.CmdInvalidProjectFileExtensionErrorDescription, output);
        }

        if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            return ValidateFilePath(pathOptions.SolutionPath, CliConstants.SolutionExtensions, LocalizableStrings.CmdInvalidSolutionFileExtensionErrorDescription, output);
        }

        if (!string.IsNullOrEmpty(pathOptions.DirectoryPath) && !Directory.Exists(pathOptions.DirectoryPath))
        {
            output.WriteMessage(string.Format(LocalizableStrings.CmdNonExistentDirectoryErrorDescription, pathOptions.DirectoryPath));
            return false;
        }

        return true;
    }

    private static bool ValidateFilePath(string filePath, string[] validExtensions, string errorMessage, TerminalTestReporter output)
    {
        if (!validExtensions.Contains(Path.GetExtension(filePath)))
        {
            output.WriteMessage(string.Format(errorMessage, filePath));
            return false;
        }

        if (!File.Exists(filePath))
        {
            output.WriteMessage(string.Format(LocalizableStrings.CmdNonExistentFileErrorDescription, Path.GetFullPath(filePath)));
            return false;
        }

        return true;
    }
}
