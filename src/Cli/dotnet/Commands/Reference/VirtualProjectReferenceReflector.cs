// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class VirtualProjectReferenceReflector
{
    internal static bool IsFileBasedAppReference(string projectReferenceInclude)
    {
        var projectReferenceFullPath = Path.GetFullPath(projectReferenceInclude);

        return VirtualProjectBuilder.TryGetEntryPointFilePathFromVirtualProjectPath(projectReferenceFullPath, out var entryPointFilePath) &&
            VirtualProjectBuilder.IsValidEntryPointPath(entryPointFilePath);
    }

    internal static void ReflectChangesToDirectives(ProjectRootElement projectRootElement, string entryPointFilePath)
    {
        var projectReferences = GetProjectReferenceIncludes(projectRootElement).ToList();
        var unmatchedProjectReferences = new List<string>(projectReferences);
        var editor = FileBasedAppSourceEditor.Load(SourceFile.Load(entryPointFilePath));
        var directives = editor.Directives;

        for (int i = directives.Length - 1; i >= 0; i--)
        {
            if (directives[i] is not CSharpDirective.Project projectDirective)
            {
                continue;
            }

            var directivePath = NormalizeProjectReferencePath(projectDirective.EnsureProjectFilePath(ErrorReporters.IgnoringReporter).Name);
            int matchingIndex = unmatchedProjectReferences.IndexOf(directivePath);

            if (matchingIndex >= 0)
            {
                unmatchedProjectReferences.RemoveAt(matchingIndex);
            }
            else
            {
                editor.Remove(projectDirective);
            }
        }

        foreach (var projectReference in unmatchedProjectReferences)
        {
            editor.Add(new CSharpDirective.Project(default, projectReference));
        }

        editor.SourceFile.Save();
    }

    private static IEnumerable<string> GetProjectReferenceIncludes(ProjectRootElement projectRootElement)
    {
        foreach (var itemGroup in projectRootElement.ItemGroups)
        {
            foreach (var item in itemGroup.Items)
            {
                if (string.Equals(item.ItemType, "ProjectReference", StringComparison.OrdinalIgnoreCase) &&
                    !IsFileBasedAppReference(item.Include))
                {
                    yield return NormalizeProjectReferencePath(item.Include);
                }
            }
        }
    }

    private static string NormalizeProjectReferencePath(string projectReferencePath)
        => projectReferencePath.Replace('\\', '/');
}
