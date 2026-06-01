// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal static class VirtualProjectReferenceReflector
{
    /// <summary>
    /// Gathers <c>#:ref</c>s that should be shown to the user when listing references.
    /// The key is the virtual project path used in the <c>ProjectReference</c> include and the value is the display text.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> GetFileBasedAppReferenceDisplayIncludes(ProjectRootElement projectRootElement, string entryPointFilePath)
    {
        var editor = FileBasedAppSourceEditor.Load(SourceFile.Load(entryPointFilePath));
        var projectInstance = new ProjectInstance(projectRootElement);
        var displayIncludes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var refDirective in editor.Directives.OfType<CSharpDirective.Ref>())
        {
            var expandedName = projectInstance.ExpandString(refDirective.Name);
            var expandedDirective = refDirective.WithName(expandedName, CSharpDirective.Ref.NameKind.Expanded);
            var resolvedDirective = expandedDirective.EnsureResolvedPath(ErrorReporters.IgnoringReporter);
            var virtualProjectPath = NormalizeProjectReferencePath(Path.GetFullPath(VirtualProjectBuilder.GetVirtualProjectPath(resolvedDirective.Name)));

            // Preserve shorthand directive text for display unless MSBuild expansion changed what the path means.
            displayIncludes[virtualProjectPath] = string.Equals(refDirective.Name, expandedName, StringComparison.Ordinal)
                ? NormalizeProjectReferencePath(refDirective.Name)
                : NormalizeProjectReferencePath(resolvedDirective.Name);
        }

        return displayIncludes;
    }

    internal static void ReflectChangesToDirectives(ProjectRootElement projectRootElement, string entryPointFilePath)
    {
        var editor = FileBasedAppSourceEditor.Load(SourceFile.Load(entryPointFilePath));
        var directives = editor.Directives;
        var projectInstance = new ProjectInstance(projectRootElement);
        var unmatchedProjectReferences = new List<string>();
        var unmatchedFileBasedAppReferences = new List<string>();

        foreach (var itemGroup in projectRootElement.ItemGroups)
        {
            foreach (var item in itemGroup.Items)
            {
                if (!string.Equals(item.ItemType, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryGetFileBasedAppReferenceInclude(item, projectRootElement, projectInstance, out var fileBasedAppReferenceInclude))
                {
                    unmatchedFileBasedAppReferences.Add(NormalizeProjectReferencePath(fileBasedAppReferenceInclude));
                }
                else
                {
                    unmatchedProjectReferences.Add(NormalizeProjectReferencePath(item.Include));
                }
            }
        }

        for (int i = directives.Length - 1; i >= 0; i--)
        {
            if (directives[i] is CSharpDirective.Project projectDirective)
            {
                var directivePath = GetResolvedProjectDirectivePath(projectDirective, projectInstance);
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
            else if (directives[i] is CSharpDirective.Ref refDirective)
            {
                var directivePath = GetResolvedRefDirectivePath(refDirective, projectInstance);
                int matchingIndex = unmatchedFileBasedAppReferences.IndexOf(directivePath);

                if (matchingIndex >= 0)
                {
                    unmatchedFileBasedAppReferences.RemoveAt(matchingIndex);
                }
                else
                {
                    editor.Remove(refDirective);
                }
            }
        }

        foreach (var projectReference in unmatchedProjectReferences)
        {
            editor.Add(new CSharpDirective.Project(default, projectReference));
        }

        foreach (var fileBasedAppReference in unmatchedFileBasedAppReferences)
        {
            editor.Add(new CSharpDirective.Ref(default, fileBasedAppReference));
        }

        editor.SourceFile.Save();
    }

    private static bool TryGetFileBasedAppReferenceInclude(
        ProjectItemElement item,
        ProjectRootElement projectRootElement,
        ProjectInstance projectInstance,
        [NotNullWhen(true)]
        out string? fileBasedAppReferenceInclude)
    {
        var directiveInclude = GetRefDirectiveInclude(item);
        if (directiveInclude is null)
        {
            fileBasedAppReferenceInclude = null;
            return false;
        }

        var expandedDirectiveInclude = projectInstance.ExpandString(directiveInclude);
        var resolvedDirectiveInclude = Path.GetFullPath(Path.Combine(projectRootElement.DirectoryPath, expandedDirectiveInclude.Replace('\\', '/')));
        var expectedVirtualProjectPath = NormalizeProjectReferencePath(Path.GetFullPath(VirtualProjectBuilder.GetVirtualProjectPath(resolvedDirectiveInclude)));
        var actualVirtualProjectPath = NormalizeProjectReferencePath(Path.GetFullPath(item.Include));

        if (!string.Equals(actualVirtualProjectPath, expectedVirtualProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            fileBasedAppReferenceInclude = null;
            return false;
        }

        fileBasedAppReferenceInclude = directiveInclude;
        return true;
    }

    /// <summary>
    /// Whether the given <c>ProjectReference</c> <paramref name="item"/> is a file-based app reference, i.e., it comes from a <c>#:ref</c> directive.
    /// </summary>
    internal static bool IsFileBasedAppReference(ProjectItemElement item, ProjectRootElement projectRootElement, ProjectInstance projectInstance)
        => TryGetFileBasedAppReferenceInclude(item, projectRootElement, projectInstance, out _);

    private static string? GetRefDirectiveInclude(ProjectItemElement item)
        => item.Metadata.FirstOrDefault(
            m => string.Equals(m.Name, VirtualProjectBuilder.RefDirectiveIncludeMetadataName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string GetResolvedProjectDirectivePath(CSharpDirective.Project projectDirective, ProjectInstance projectInstance)
    {
        var expandedDirective = projectDirective.WithName(
            projectInstance.ExpandString(projectDirective.Name),
            CSharpDirective.Project.NameKind.Expanded);

        return NormalizeProjectReferencePath(expandedDirective.EnsureProjectFilePath(ErrorReporters.IgnoringReporter).Name);
    }

    private static string GetResolvedRefDirectivePath(CSharpDirective.Ref refDirective, ProjectInstance projectInstance)
    {
        var expandedDirective = refDirective.WithName(
            projectInstance.ExpandString(refDirective.Name),
            CSharpDirective.Ref.NameKind.Expanded);

        return NormalizeProjectReferencePath(expandedDirective.EnsureResolvedPath(ErrorReporters.IgnoringReporter).Name);
    }

    internal static string NormalizeProjectReferencePath(string projectReferencePath)
        => projectReferencePath.Replace('\\', '/');
}
