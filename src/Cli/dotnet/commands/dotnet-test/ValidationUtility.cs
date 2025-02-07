// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test;
using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.DotNet.Cli
{
    internal static class ValidationUtility
    {
        public static bool ValidateBuildPathOptions(BuildOptions buildPathOptions, TerminalTestReporter output)
        {
            PathOptions pathOptions = buildPathOptions.PathOptions;
            if ((!string.IsNullOrEmpty(pathOptions.ProjectPath) && !string.IsNullOrEmpty(pathOptions.SolutionPath)) ||
                (!string.IsNullOrEmpty(pathOptions.ProjectPath) && !string.IsNullOrEmpty(pathOptions.DirectoryPath)) ||
                (!string.IsNullOrEmpty(pathOptions.SolutionPath) && !string.IsNullOrEmpty(pathOptions.DirectoryPath)))
            {
                output.WriteMessage(LocalizableStrings.CmdMultipleBuildPathOptionsErrorDescription);
                return false;
            }

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
}
