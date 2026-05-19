// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Provides the CliStrings properties needed by linked source files (SlnFileFactory, Parser)
// in the AOT project context, where the full CliStrings.resx is not auto-generated.

namespace Microsoft.DotNet.Cli;

internal static class CliStrings
{
    public static string CouldNotFindSolutionOrDirectory => "Could not find solution or directory `{0}`.";
    public static string CouldNotFindSolutionIn => "Specified solution file {0} does not exist, or there is no solution file in the directory.";
    public static string MoreThanOneSolutionInDirectory => "Found more than one solution file in {0}. Specify which one to use.";
    public static string InvalidSolutionFormatString => "Invalid solution `{0}`. {1}.";
    public static string ProjectNotFoundInTheSolution => "Project `{0}` could not be found in the solution.";
    public static string NoProjectsFound => "No projects found in the solution.";
}
