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
    public static void ValidateMutuallyExclusiveOptions(ParseResult parseResult, PathOptions pathOptions)
    {
        ValidatePathOptions(pathOptions);
        ValidateOptionsIrrelevantToModulesFilter(parseResult, pathOptions.TestModules);

        static void ValidatePathOptions(PathOptions pathOptions)
        {
            var count = 0;
            if (pathOptions.TestModules is not null)
                count++;

            if (pathOptions.SolutionPath is not null)
                count++;

            if (pathOptions.ProjectOrSolutionPath is not null)
                count++;

            if (count > 1)
                throw new GracefulException(CliCommandStrings.CmdMultipleBuildPathOptionsErrorDescription);
        }

        static void ValidateOptionsIrrelevantToModulesFilter(ParseResult parseResult, string? testModules)
        {
            if (testModules is null)
            {
                return;
            }

            var definition = (TestCommandDefinition.MicrosoftTestingPlatform)parseResult.CommandResult.Command;

            if (parseResult.HasOption(definition.TargetPlatformOptions.ArchitectureOption) ||
                parseResult.HasOption(definition.ConfigurationOption) ||
                parseResult.HasOption(definition.FrameworkOption) ||
                parseResult.HasOption(definition.TargetPlatformOptions.OperatingSystemOption) ||
                parseResult.HasOption(definition.TargetPlatformOptions.RuntimeOption))
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
