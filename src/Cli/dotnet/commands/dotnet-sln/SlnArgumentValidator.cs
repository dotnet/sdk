// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Sln;

internal static class SlnArgumentValidator
{
    private static readonly SearchValues<char> s_invalidCharactersInSolutionFolderName = SearchValues.Create("/:?\\*\"<>|");
    private static readonly string[] s_invalidSolutionFolderNames =
    [
        // system reserved names per https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file#naming-conventions
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        // relative path components
        ".", "..",
    ];

    public enum CommandType
    {
        Add,
        Remove
    }
    public static void ParseAndValidateArguments(IReadOnlyList<string> _arguments, CommandType commandType, bool _inRoot = false, string relativeRoot = null, string subcommand = null)
    {
        if (_arguments.Count == 0)
        {
            string message = commandType == CommandType.Add
                ? CommonLocalizableStrings.SpecifyAtLeastOneProjectToAdd
                : CommonLocalizableStrings.SpecifyAtLeastOneProjectToRemove;
            throw new GracefulException(message);
        }

        bool hasRelativeRoot = !string.IsNullOrEmpty(relativeRoot);

        if (_inRoot && hasRelativeRoot)
        {
            // These two options are mutually exclusive
            throw new GracefulException(LocalizableStrings.SolutionFolderAndInRootMutuallyExclusive);
        }

        // Something is wrong if there is a .sln file as an argument, so suggest that the arguments may have been misplaced.
        // However, it is possible to add .sln file as a solution item, so don't suggest in the case of dotnet sln add file.
        var slnFile = _arguments.FirstOrDefault(path => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        if (slnFile == null || subcommand == "file")
        {
            return;
        }

        string options = _inRoot
            ? $"{SlnAddParser.InRootOption.Name} "
            : hasRelativeRoot
                ? $"{SlnAddParser.SolutionFolderOption.Name} {string.Join(" ", relativeRoot)} "
                : "";

        var nonSolutionArguments = string.Join(
            " ",
            _arguments.Where(a => !a.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)));

        string command = commandType switch
        {
            CommandType.Add => "add",
            CommandType.Remove => "remove",
            _ => throw new InvalidOperationException($"Unable to handle command type {commandType}"),
        };
        throw new GracefulException(
        [
            string.Format(CommonLocalizableStrings.SolutionArgumentMisplaced, slnFile),
                CommonLocalizableStrings.DidYouMean,
                subcommand == null
                    ? $"  dotnet solution {slnFile} {command} {options}{nonSolutionArguments}"
                    : $"  dotnet solution {slnFile} {command} {subcommand} {options}{nonSolutionArguments}"
        ]);
    }

    public static bool IsValidSolutionFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return false;

        if (folderName.AsSpan().IndexOfAny(s_invalidCharactersInSolutionFolderName) >= 0)
            return false;

        if (folderName.Any(char.IsControl))
            return false;

        if (folderName.Any(char.IsSurrogate))
            return false;

        if (s_invalidSolutionFolderNames.Contains(folderName, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
