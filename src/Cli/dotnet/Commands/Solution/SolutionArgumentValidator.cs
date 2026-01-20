// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Commands.Solution.Add;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Solution;

internal static class SolutionArgumentValidator
{
    public enum CommandType
    {
        Add,
        Remove
    }

    public static void ParseAndValidateArguments(string _fileOrDirectory, IReadOnlyCollection<string> _arguments, CommandType commandType, bool _inRoot = false, string relativeRoot = null)
    {
        if (_arguments.Count == 0)
        {
            string message = commandType == CommandType.Add ? CliStrings.SpecifyAtLeastOneProjectToAdd : CliStrings.SpecifyAtLeastOneProjectToRemove;
            throw new GracefulException(message);
        }

        bool hasRelativeRoot = !string.IsNullOrEmpty(relativeRoot);

        if (_inRoot && hasRelativeRoot)
        {
            // These two options are mutually exclusive
            throw new GracefulException(CliCommandStrings.SolutionFolderAndInRootMutuallyExclusive);
        }

        var slnFile = _arguments.FirstOrDefault(path => path.HasExtension(".sln") || path.HasExtension(".slnx"));
        if (slnFile != null)
        {
            string args;
            if (_inRoot)
            {
                args = $"--{SolutionAddCommandParser.InRootOption.Name} ";
            }
            else if (hasRelativeRoot)
            {
                args = $"--{SolutionAddCommandParser.SolutionFolderOption.Name} {string.Join(" ", relativeRoot)} ";
            }
            else
            {
                args = "";
            }

            var projectArgs = string.Join(" ", _arguments.Where(path => !path.HasExtension(".sln") && !path.HasExtension(".slnx")));
            string command = commandType == CommandType.Add ? "add" : "remove";
            throw new GracefulException(
            [
                string.Format(CliStrings.SolutionArgumentMisplaced, slnFile),
                CliStrings.DidYouMean,
                $"  dotnet solution {slnFile} {command} {args}{projectArgs}"
            ]);
        }
    }
}
