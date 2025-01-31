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
            if ((!string.IsNullOrEmpty(buildPathOptions.ProjectPath) && !string.IsNullOrEmpty(buildPathOptions.SolutionPath)) ||
                (!string.IsNullOrEmpty(buildPathOptions.ProjectPath) && !string.IsNullOrEmpty(buildPathOptions.DirectoryPath)) ||
                (!string.IsNullOrEmpty(buildPathOptions.SolutionPath) && !string.IsNullOrEmpty(buildPathOptions.DirectoryPath)))
            {
                output.WriteMessage(LocalizableStrings.CmdMultipleBuildPathOptionsErrorDescription);
                return false;
            }

            if (!string.IsNullOrEmpty(buildPathOptions.ProjectPath))
            {
                return ValidateFilePath(buildPathOptions.ProjectPath, CliConstants.ProjectExtensions, LocalizableStrings.CmdInvalidProjectFileExtensionErrorDescription, output);
            }

            if (!string.IsNullOrEmpty(buildPathOptions.SolutionPath))
            {
                return ValidateFilePath(buildPathOptions.SolutionPath, CliConstants.SolutionExtensions, LocalizableStrings.CmdInvalidSolutionFileExtensionErrorDescription, output);
            }

            if (!string.IsNullOrEmpty(buildPathOptions.DirectoryPath) && !Directory.Exists(buildPathOptions.DirectoryPath))
            {
                output.WriteMessage(string.Format(LocalizableStrings.CmdNonExistentDirectoryErrorDescription, buildPathOptions.DirectoryPath));
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
