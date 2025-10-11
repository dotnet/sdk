﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
            if (parseResult.HasOption(MicrosoftTestingPlatformOptions.TestModulesFilterOption))
                count++;

            if (parseResult.HasOption(MicrosoftTestingPlatformOptions.SolutionOption))
                count++;

            if (parseResult.HasOption(MicrosoftTestingPlatformOptions.ProjectOption))
                count++;

            if (count > 1)
                throw new GracefulException(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);
        }

        static void ValidateOptionsIrrelevantToModulesFilter(ParseResult parseResult)
        {
            if (!parseResult.HasOption(MicrosoftTestingPlatformOptions.TestModulesFilterOption))
            {
                return;
            }

            if (parseResult.HasOption(CommonOptions.ArchitectureOption) ||
                parseResult.HasOption(MicrosoftTestingPlatformOptions.ConfigurationOption) ||
                parseResult.HasOption(MicrosoftTestingPlatformOptions.FrameworkOption) ||
                parseResult.HasOption(CommonOptions.OperatingSystemOption) ||
                parseResult.HasOption(CommonOptions.RuntimeOptionName))
            {
                throw new GracefulException(CliCommandStrings.CmdOptionCannotBeUsedWithTestModulesDescription);
            }
        }
    }

    public static bool ValidateBuildPathOptions(BuildOptions buildPathOptions)
    {
        PathOptions pathOptions = buildPathOptions.PathOptions;

        if (!string.IsNullOrEmpty(pathOptions.ProjectPath))
        {
            return ValidateProjectPath(pathOptions.ProjectPath);
        }

        if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            return ValidateSolutionPath(pathOptions.SolutionPath);
        }

        return true;
    }

    /// <summary>
    /// Validates that arguments requiring specific command-line switches are used correctly for Microsoft.Testing.Platform.
    /// Provides helpful error messages when users provide file/directory arguments without proper switches.
    /// </summary>
    public static void ValidateSolutionOrProjectOrDirectoryOrModulesArePassedCorrectly(ParseResult parseResult)
    {
        if (Environment.GetEnvironmentVariable("DOTNET_TEST_DISABLE_SWITCH_VALIDATION") is "true" or "1")
        {
            // In case there is a valid case, users can opt-out.
            // Note that the validation here is added to have a "better" error message for scenarios that will already fail.
            // So, disabling validation is okay if the user scenario is valid.
            return;
        }

        foreach (string token in parseResult.UnmatchedTokens)
        {
            // Check for .sln files
            if ((token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                token.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                throw new GracefulException(CliCommandStrings.TestCommandUseSolution);
            }
            else if ((token.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                     token.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                     token.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)) && File.Exists(token))
            {
                throw new GracefulException(CliCommandStrings.TestCommandUseProject);
            }
            else if ((token.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                      token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) &&
                     File.Exists(token))
            {
                throw new GracefulException(CliCommandStrings.TestCommandUseTestModules);
            }
            else if (Directory.Exists(token))
            {
                throw new GracefulException(CliCommandStrings.TestCommandUseDirectoryWithSwitch);
            }
        }
    }

    private static bool ValidateSolutionPath(string path)
    {
        // If it's a directory, just check if it exists
        if (Directory.Exists(path))
        {
            return true;
        }

        // If it's not a directory, validate as a file path
        if (!CliConstants.SolutionExtensions.Contains(Path.GetExtension(path)))
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdInvalidSolutionFileExtensionErrorDescription, path));
            return false;
        }

        return ValidateFilePathExists(path);
    }

    private static bool ValidateProjectPath(string path)
    {
        // If it's a directory, just check if it exists
        if (Directory.Exists(path))
        {
            return true;
        }

        // If it's not a directory, validate as a file path
        if (!Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase))
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdInvalidProjectFileExtensionErrorDescription, path));
            return false;
        }

        return ValidateFilePathExists(path);
    }

    private static bool ValidateFilePathExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdNonExistentFileErrorDescription, Path.GetFullPath(filePath)));
            return false;
        }

        return true;
    }
}
