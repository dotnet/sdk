// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.FileBasedPrograms;

/// <summary>
/// A proxy to avoid exposing internals unnecessarily.
/// </summary>
public static class FileBasedProgramAccessor
{
    public static string? GetPropertyFromSourceFile(string sourceFilePath, string propertyName)
        => VirtualProjectBuilder.GetPropertyFromSourceFile(LanguageService.Instance, sourceFilePath, propertyName);

    public static IProjectInstance CreateProjectInstance(
        IBuildService buildService,
        string entryPointFilePath,
        string targetFramework,
        IProjectCollection projectCollection,
        Action<string, int, string> errorReporter)
        => VirtualProjectBuilder.CreateProjectInstance(
            buildService,
            LanguageService.Instance,
            entryPointFilePath,
            targetFramework,
            projectCollection,
            errorReporter);
}
