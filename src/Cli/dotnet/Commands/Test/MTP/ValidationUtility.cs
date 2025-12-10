// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.CommandLine;
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

            if (parseResult.HasOption(MicrosoftTestingPlatformOptions.ProjectOrSolutionOption))
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
                parseResult.HasOption(TestCommandDefinition.ConfigurationOption) ||
                parseResult.HasOption(TestCommandDefinition.FrameworkOption) ||
                parseResult.HasOption(CommonOptions.OperatingSystemOption) ||
                parseResult.HasOption(CommonOptions.RuntimeOptionName))
            {
                throw new GracefulException(CliCommandStrings.CmdOptionCannotBeUsedWithTestModulesDescription);
            }
        }
    }

    public static bool ValidateBuildPathOptions(PathOptions pathOptions, [NotNullWhen(true)] out string? projectOrSolutionFilePath, out bool isSolution)
    {
        if (!string.IsNullOrEmpty(pathOptions.ProjectOrSolutionPath))
        {
            return ValidateProjectOrSolutionPath(pathOptions.ProjectOrSolutionPath, out projectOrSolutionFilePath, out isSolution);
        }

        if (!string.IsNullOrEmpty(pathOptions.SolutionPath))
        {
            isSolution = true;
            return ValidateSolutionPath(pathOptions.SolutionPath, out projectOrSolutionFilePath);
        }

        return TryGetProjectOrSolutionFromDirectory(Directory.GetCurrentDirectory(), onlyConsiderSolutions: false, out projectOrSolutionFilePath, out isSolution);
    }

    private static bool TryGetProjectOrSolutionFromDirectory(
        string directory,
        bool onlyConsiderSolutions,
        [NotNullWhen(true)] out string? projectOrSolutionFilePath,
        out bool isSolution)
    {
        bool foundSolutionOrProjectInDirectory;
        string? message;
        if (onlyConsiderSolutions)
        {
            isSolution = true;
            (foundSolutionOrProjectInDirectory, message) = SolutionAndProjectUtility.TryGetSolutionFilePath(directory, out projectOrSolutionFilePath);
        }
        else
        {
            (foundSolutionOrProjectInDirectory, message) = SolutionAndProjectUtility.TryGetProjectOrSolutionFilePath(directory, out projectOrSolutionFilePath, out isSolution);
        }

        if (!foundSolutionOrProjectInDirectory)
        {
            Reporter.Error.WriteLine(message);
            projectOrSolutionFilePath = null;
            isSolution = false;
            return false;
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

    private static bool ValidateSolutionPath(string solutionFileOrDirectory, [NotNullWhen(true)] out string? solutionFile)
    {
        // If it's a directory, just check if it exists
        if (Directory.Exists(solutionFileOrDirectory))
        {
            return TryGetProjectOrSolutionFromDirectory(solutionFileOrDirectory, onlyConsiderSolutions: true, out solutionFile, out _);
        }

        solutionFile = solutionFileOrDirectory;

        // If it's not a directory, validate as a file path
        if (!CliConstants.SolutionExtensions.Contains(Path.GetExtension(solutionFileOrDirectory)))
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdInvalidSolutionFileExtensionErrorDescription, solutionFileOrDirectory));
            return false;
        }

        return ValidateFilePathExists(solutionFile);
    }

    private static bool ValidateProjectOrSolutionPath(string projectOrSolutionFileOrDirectory, [NotNullWhen(true)] out string? projectOrSolutionFile, out bool isSolution)
    {
        // If it's a directory, just check if it exists
        if (Directory.Exists(projectOrSolutionFileOrDirectory))
        {
            return TryGetProjectOrSolutionFromDirectory(projectOrSolutionFileOrDirectory, onlyConsiderSolutions: false, out projectOrSolutionFile, out isSolution);
        }

        var extension = Path.GetExtension(projectOrSolutionFileOrDirectory);
        isSolution = CliConstants.SolutionExtensions.Contains(extension);
        projectOrSolutionFile = projectOrSolutionFileOrDirectory;
        // If it's not a directory, validate as a file path
        if (!isSolution && !extension.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
        {
            projectOrSolutionFile = null;
            isSolution = false;
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.CmdInvalidProjectFileExtensionErrorDescription, projectOrSolutionFileOrDirectory));
            return false;
        }

        return ValidateFilePathExists(projectOrSolutionFileOrDirectory);
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
