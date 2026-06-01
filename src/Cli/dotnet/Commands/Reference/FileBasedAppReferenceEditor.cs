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
                yield return projectDirective.Name;
            }
            else if (directive is CSharpDirective.Ref refDirective)
            {
                yield return refDirective.Name;
            }
        }
    }

    public int AddProjectReferences(IEnumerable<(string ProjectFilePath, string DirectiveInclude)> references)
    {
        int numberOfAddedReferences = 0;

        foreach (var reference in references)
        {
            if (TryGetProjectReferenceDisplay(reference.ProjectFilePath, out var existingReferenceDisplay))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    existingReferenceDisplay));
                continue;
            }

            numberOfAddedReferences++;
            _sourceEditor.Add(new CSharpDirective.Project(default, reference.DirectiveInclude));
            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, reference.DirectiveInclude));
        }

        return numberOfAddedReferences;
    }

    public int AddFileBasedAppReferences(IEnumerable<(string FilePath, string DirectiveInclude)> references)
    {
        int numberOfAddedReferences = 0;

        foreach (var reference in references)
        {
            if (TryGetFileBasedAppReferenceDisplay(reference.FilePath, out var existingReferenceDisplay))
            {
                Reporter.Output.WriteLine(string.Format(
                    CliStrings.ProjectAlreadyHasAreference,
                    existingReferenceDisplay));
                continue;
            }

            numberOfAddedReferences++;
            _sourceEditor.Add(new CSharpDirective.Ref(default, reference.DirectiveInclude));
            Reporter.Output.WriteLine(string.Format(CliStrings.ReferenceAddedToTheProject, reference.DirectiveInclude));
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
            Reporter.Output.WriteLine(string.Format(CliStrings.ProjectReferenceRemoved, directive.Name));
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
            Reporter.Output.WriteLine(string.Format(CliStrings.ProjectReferenceRemoved, directive.Name));
        }

        return matchingDirectives.Count;
    }

    private bool TryGetProjectReferenceDisplay(string projectFilePath, out string display)
    {
        var directive = _sourceEditor.Directives
            .OfType<CSharpDirective.Project>()
            .FirstOrDefault(d => PathsEqual(GetProjectDirectiveFullPath(d), projectFilePath));

        if (directive is null)
        {
            display = string.Empty;
            return false;
        }

        display = directive.Name;
        return true;
    }

    private bool TryGetFileBasedAppReferenceDisplay(string filePath, out string display)
    {
        var directive = _sourceEditor.Directives
            .OfType<CSharpDirective.Ref>()
            .FirstOrDefault(d => PathsEqual(GetRefDirectiveFullPath(d), filePath));

        if (directive is null)
        {
            display = string.Empty;
            return false;
        }

        display = directive.Name;
        return true;
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
