// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.FileBasedPrograms;

/// <summary>
/// Allow convenient access to resources which are relevant to Cli scenarios but actually defined in other resource files.
/// </summary>
internal static class CliStringsExtensions
{
    extension(CliStrings)
    {
        public static string CouldNotFindAnyProjectInDirectory => FileBasedProgramsResources.CouldNotFindAnyProjectInDirectory;
        public static string CouldNotFindProjectOrDirectory => FileBasedProgramsResources.CouldNotFindProjectOrDirectory;
        public static string MoreThanOneProjectInDirectory => FileBasedProgramsResources.MoreThanOneProjectInDirectory;
    }
}
