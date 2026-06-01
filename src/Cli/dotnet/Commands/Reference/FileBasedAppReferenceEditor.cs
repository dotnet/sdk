// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Reference;

internal sealed class FileBasedAppReferenceEditor
{
    private readonly FileBasedAppSourceEditor _sourceEditor;
    private readonly ProjectInstance _projectInstance;

    public FileBasedAppReferenceEditor(string entryPointFilePath)
    {
        EntryPointFilePath = Path.GetFullPath(entryPointFilePath);
        EntryPointFileDirectory = Path.GetDirectoryName(EntryPointFilePath)
            ?? throw new InvalidOperationException($"Source file path '{EntryPointFilePath}' does not have a containing directory.");
        _sourceEditor = FileBasedAppSourceEditor.Load(SourceFile.Load(EntryPointFilePath));
        _projectInstance = CreateExpansionProjectInstance(EntryPointFilePath);
    }

    public string EntryPointFilePath { get; }

    public string EntryPointFileDirectory { get; }

    public IEnumerable<string> GetReferencesForDisplay()
    {
        foreach (var directive in _sourceEditor.Directives)
        {
            if (directive is CSharpDirective.Project projectDirective)
            {
                yield return GetProjectDirectiveDisplay(projectDirective);
            }
            else if (directive is CSharpDirective.Ref refDirective)
            {
                yield return GetRefDirectiveDisplay(refDirective);
            }
        }
    }

    public int AddProjectReferences(IEnumerable<(string ProjectFilePath, string DirectiveInclude, string DisplayInclude)> references)
    {
        int numberOfAddedReferences = 0;

        foreach (var reference in references)
        {
            string displayReference = PathUtility.GetPathWithBackSlashes(reference.DisplayInclude);
            if (ContainsProjectReference(reference.ProjectFilePath))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    displayReference));
                continue;
            }

            numberOfAddedReferences++;
            _sourceEditor.Add(new CSharpDirective.Project(default, reference.DirectiveInclude));
            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, displayReference));
        }

        return numberOfAddedReferences;
    }

    public int AddFileBasedAppReferences(IEnumerable<(string FilePath, string DirectiveInclude, string DisplayInclude)> references)
    {
        int numberOfAddedReferences = 0;

        foreach (var reference in references)
        {
            string displayReference = PathUtility.GetPathWithBackSlashes(reference.DisplayInclude);
            if (ContainsFileBasedAppReference(reference.FilePath))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    displayReference));
                continue;
            }

            numberOfAddedReferences++;
            _sourceEditor.Add(new CSharpDirective.Ref(default, reference.DirectiveInclude));
            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, displayReference));
        }

        return numberOfAddedReferences;
    }

    public int RemoveReferences(IEnumerable<string> references)
    {
        int totalNumberOfRemovedReferences = 0;

        foreach (var reference in references)
        {
            int numberOfRemovedReferences = RemoveFileBasedAppReferences(reference);

            if (numberOfRemovedReferences == 0)
            {
                numberOfRemovedReferences = RemoveProjectReferences(reference);
            }

            if (numberOfRemovedReferences == 0)
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectReferenceCouldNotBeFound,
                    reference));
            }

            totalNumberOfRemovedReferences += numberOfRemovedReferences;
        }

        return totalNumberOfRemovedReferences;
    }

    public void Save() => _sourceEditor.SourceFile.Save();

    private int RemoveProjectReferences(string reference)
    {
        var referencePaths = GetProjectReferenceArgumentFullPathAlternatives(reference).ToList();
        var matchingDirectives = _sourceEditor.Directives
            .OfType<CSharpDirective.Project>()
            .Where(d => referencePaths.Any(p => PathsEqual(GetProjectDirectiveFullPath(d), p)))
            .OrderByDescending(d => d.Info.Span.Start)
            .ToList();

        foreach (var directive in matchingDirectives)
        {
            _sourceEditor.Remove(directive);
            Reporter.Output.WriteLine(string.Format(CliStrings.ProjectReferenceRemoved, GetProjectRemovalDisplay(directive, reference)));
        }

        return matchingDirectives.Count;
    }

    private int RemoveFileBasedAppReferences(string reference)
    {
        var referencePath = Path.GetFullPath(reference);
        var matchingDirectives = _sourceEditor.Directives
            .OfType<CSharpDirective.Ref>()
            .Where(d => PathsEqual(GetRefDirectiveFullPath(d), referencePath))
            .OrderByDescending(d => d.Info.Span.Start)
            .ToList();

        foreach (var directive in matchingDirectives)
        {
            _sourceEditor.Remove(directive);
            Reporter.Output.WriteLine(string.Format(CliStrings.ProjectReferenceRemoved, PathUtility.GetPathWithBackSlashes(reference)));
        }

        return matchingDirectives.Count;
    }

    private bool ContainsProjectReference(string projectFilePath)
        => _sourceEditor.Directives
            .OfType<CSharpDirective.Project>()
            .Any(d => PathsEqual(GetProjectDirectiveFullPath(d), projectFilePath));

    private bool ContainsFileBasedAppReference(string filePath)
        => _sourceEditor.Directives
            .OfType<CSharpDirective.Ref>()
            .Any(d => PathsEqual(GetRefDirectiveFullPath(d), filePath));

    private string GetProjectDirectiveDisplay(CSharpDirective.Project projectDirective)
    {
        string expandedName = _projectInstance.ExpandString(projectDirective.Name);
        if (string.Equals(projectDirective.Name, expandedName, StringComparison.Ordinal))
        {
            return NormalizePath(projectDirective.Name);
        }

        return PathUtility.GetPathWithBackSlashes(GetProjectDirectiveFullPath(projectDirective));
    }

    private string GetProjectRemovalDisplay(CSharpDirective.Project projectDirective, string reference)
    {
        string expandedName = _projectInstance.ExpandString(projectDirective.Name);
        if (!string.Equals(projectDirective.Name, expandedName, StringComparison.Ordinal))
        {
            return PathUtility.GetPathWithBackSlashes(GetProjectDirectiveFullPath(projectDirective));
        }

        var directiveDisplays = new[]
        {
            projectDirective.Name,
            Path.GetRelativePath(EntryPointFileDirectory, GetProjectDirectiveFullPath(projectDirective)),
        };

        foreach (var alternative in GetProjectReferenceDisplayAlternatives(reference))
        {
            if (directiveDisplays.Any(d => string.Equals(NormalizePath(d), NormalizePath(alternative), StringComparison.OrdinalIgnoreCase)))
            {
                return alternative;
            }
        }

        return GetProjectDirectiveDisplay(projectDirective);
    }

    private string GetRefDirectiveDisplay(CSharpDirective.Ref refDirective)
    {
        string expandedName = _projectInstance.ExpandString(refDirective.Name);
        return string.Equals(refDirective.Name, expandedName, StringComparison.Ordinal)
            ? NormalizePath(refDirective.Name)
            : NormalizePath(GetRefDirectiveFullPath(refDirective));
    }

    private string GetProjectDirectiveFullPath(CSharpDirective.Project projectDirective)
    {
        var expandedDirective = projectDirective.WithName(
            _projectInstance.ExpandString(projectDirective.Name),
            CSharpDirective.Project.NameKind.Expanded);

        var resolvedDirective = expandedDirective.EnsureProjectFilePath(ErrorReporters.IgnoringReporter);
        return GetFullPathRelativeToEntryPointFileDirectory(resolvedDirective.Name);
    }

    private string GetRefDirectiveFullPath(CSharpDirective.Ref refDirective)
    {
        var expandedDirective = refDirective.WithName(
            _projectInstance.ExpandString(refDirective.Name),
            CSharpDirective.Ref.NameKind.Expanded);

        return expandedDirective.EnsureResolvedPath(ErrorReporters.IgnoringReporter).Name;
    }

    private IEnumerable<string> GetProjectReferenceArgumentFullPathAlternatives(string reference)
    {
        yield return ResolveProjectReferenceArgumentFullPath(Path.GetFullPath(reference));

        var appRelativePath = Path.GetFullPath(Path.Combine(EntryPointFileDirectory, reference.Replace('\\', '/')));
        if (!PathsEqual(appRelativePath, reference))
        {
            yield return ResolveProjectReferenceArgumentFullPath(appRelativePath);
        }
    }

    private IEnumerable<string> GetProjectReferenceDisplayAlternatives(string reference)
    {
        yield return reference;

        var fullPath = Path.GetFullPath(reference);
        yield return fullPath;

        var projectFilePath = ResolveProjectReferenceArgumentFullPath(fullPath);
        yield return Path.GetRelativePath(EntryPointFileDirectory, projectFilePath);
    }

    private static string ResolveProjectReferenceArgumentFullPath(string fullPath)
        => Directory.Exists(fullPath)
            ? MsbuildProject.GetProjectFileFromDirectory(fullPath)
            : fullPath;

    private string GetFullPathRelativeToEntryPointFileDirectory(string path)
        => Path.GetFullPath(Path.Combine(EntryPointFileDirectory, path.Replace('\\', '/')));

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            NormalizePath(Path.GetFullPath(left)),
            NormalizePath(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static ProjectInstance CreateExpansionProjectInstance(string entryPointFilePath)
    {
        var projectRootElement = ProjectRootElement.Create();
        projectRootElement.FullPath = VirtualProjectBuilder.GetVirtualProjectPath(entryPointFilePath);
        return new ProjectInstance(projectRootElement);
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
