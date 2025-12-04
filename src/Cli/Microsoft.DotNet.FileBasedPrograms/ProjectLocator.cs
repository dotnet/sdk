// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ProjectTools;

internal static class ProjectLocator
{
    // https://github.com/dotnet/sdk/issues/51487: Delete copies of methods from MsbuildProject and MSBuildUtilities from the source package, sharing the original method(s) under src/Cli instead.
    public static bool TryGetProjectFileFromDirectory(string projectDirectory, [NotNullWhen(true)] out string? projectFilePath, [NotNullWhen(false)] out string? error)
    {
        projectFilePath = null;
        error = null;

        DirectoryInfo? dir;
        try
        {
            dir = new DirectoryInfo(projectDirectory);
        }
        catch (ArgumentException)
        {
            dir = null;
        }

        if (dir == null || !dir.Exists)
        {
            error = string.Format(FileBasedProgramsResources.CouldNotFindProjectOrDirectory, projectDirectory);
            return false;
        }

        FileInfo[] files = dir.GetFiles("*proj");
        if (files.Length == 0)
        {
            error = string.Format(FileBasedProgramsResources.CouldNotFindAnyProjectInDirectory, projectDirectory);
            return false;
        }

        if (files.Length > 1)
        {
            error = string.Format(FileBasedProgramsResources.MoreThanOneProjectInDirectory, projectDirectory);
            return false;
        }

        projectFilePath = files.First().FullName;
        return true;
    }
}
