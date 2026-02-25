// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommand : CommandBase<ProjectConvertCommandDefinition>
{
    private readonly string _file;
    private readonly string? _outputDirectory;
    private readonly bool _force;
    private readonly bool _dryRun;

    public ProjectConvertCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _file = parseResult.GetValue(Definition.FileArgument) ?? string.Empty;
        _outputDirectory = parseResult.GetValue(Definition.OutputOption)?.FullName;
        _force = parseResult.GetValue(Definition.ForceOption);
        _dryRun = parseResult.GetValue(Definition.DryRunOption);
    }

    public override int Execute()
    {
        // Check the entry point file path.
        string file = Path.GetFullPath(_file);
        if (!VirtualProjectBuilder.IsValidEntryPointPath(file))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, file);
        }

        string targetDirectory = DetermineOutputDirectory(file);

        // Create a project instance for evaluation.
        var projectCollection = new ProjectCollection();

        var builder = new VirtualProjectBuilder(file, VirtualProjectBuildingCommand.TargetFrameworkVersion);

        builder.CreateProjectInstance(
            projectCollection,
            VirtualProjectBuildingCommand.ThrowingReporter,
            out var projectInstance,
            out var evaluatedDirectives,
            validateAllDirectives: !_force);

        // Find other items to copy over, e.g., default Content items like JSON files in Web apps.
        var includeItems = FindIncludedItems().ToList();


        CreateDirectory(targetDirectory);

        var targetFile = Path.Join(targetDirectory, Path.GetFileName(file));

        // Process the entry point file.
        if (_dryRun)
        {
            Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCopyFile, file, targetFile);
            Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldConvertFile, targetFile);
        }
        else
        {
            VirtualProjectBuilder.RemoveDirectivesFromFile(evaluatedDirectives, builder.EntryPointSourceFile.Text, targetFile);
        }

        // Create project file.
        string projectFile = Path.Join(targetDirectory, Path.GetFileNameWithoutExtension(file) + ".csproj");
        if (_dryRun)
        {
            Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCreateFile, projectFile);
        }
        else
        {
            using var stream = File.Open(projectFile, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            VirtualProjectBuilder.WriteProjectFile(writer, UpdateDirectives(evaluatedDirectives), isVirtualProject: false,
                userSecretsId: DetermineUserSecretsId(),
                defaultProperties: GetDefaultProperties());
        }

        // Copy or move over included items.
        foreach (var item in includeItems)
        {
            string targetItemFullPath = Path.Combine(targetDirectory, item.RelativePath);

            // Ignore already-copied files.
            if (File.Exists(targetItemFullPath))
            {
                continue;
            }

            string targetItemDirectory = Path.GetDirectoryName(targetItemFullPath)!;
            CreateDirectory(targetItemDirectory);
            CopyFile(item.FullPath, targetItemFullPath);
        }

        return 0;

        void CreateDirectory(string path)
        {
            if (_dryRun)
            {
                if (!Directory.Exists(path))
                {
                    Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCreateDirectory, path);
                }
            }
            else
            {
                Directory.CreateDirectory(path);
            }
        }

        void CopyFile(string source, string target)
        {
            if (_dryRun)
            {
                Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCopyFile, source, target);
            }
            else
            {
                File.Copy(source, target);
            }
        }

        IEnumerable<(string FullPath, string RelativePath)> FindIncludedItems()
        {
            string entryPointFileDirectory = PathUtilities.EnsureTrailingSlash(Path.GetDirectoryName(file)!);

            // Include only items we know are files.
            string[] itemTypes = ["Content", "None", "Compile", "EmbeddedResource"];
            var items = itemTypes.SelectMany(t => projectInstance.GetItems(t));

            foreach (var item in items)
            {
                // Escape hatch - exclude items that have metadata `ExcludeFromFileBasedAppConversion` set to `true`.
                string include = item.GetMetadataValue("ExcludeFromFileBasedAppConversion");
                if (string.Equals(include, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Exclude items that are not contained within the entry point file directory.
                string itemFullPath = Path.GetFullPath(path: item.GetMetadataValue("FullPath"), basePath: entryPointFileDirectory);
                if (!itemFullPath.StartsWith(entryPointFileDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Exclude items that do not exist.
                if (!File.Exists(itemFullPath))
                {
                    continue;
                }

                string itemRelativePath = Path.GetRelativePath(relativeTo: entryPointFileDirectory, path: itemFullPath);
                yield return (FullPath: itemFullPath, RelativePath: itemRelativePath);
            }
        }

        string? DetermineUserSecretsId()
        {
            var implicitValue = projectInstance.GetPropertyValue("_ImplicitFileBasedProgramUserSecretsId");
            var actualValue = projectInstance.GetPropertyValue("UserSecretsId");
            return implicitValue == actualValue ? actualValue : null;
        }

        ImmutableArray<CSharpDirective> UpdateDirectives(ImmutableArray<CSharpDirective> directives)
        {
            var sourceDirectory = Path.GetDirectoryName(file)!;

            var result = ImmutableArray.CreateBuilder<CSharpDirective>(directives.Length);

            foreach (var directive in directives)
            {
                // Fixup relative project reference paths (they need to be relative to the output directory instead of the source directory,
                // and preserve MSBuild interpolation variables like `$(..)`
                // while also pointing to the project file rather than a directory).
                if (directive is CSharpDirective.Project project)
                {
                    Debug.Assert(project.ExpandedName != null && project.ProjectFilePath != null && project.ProjectFilePath == project.Name);

                    if (Path.IsPathFullyQualified(project.Name))
                    {
                        // If the path is absolute and has no `$(..)` vars, just keep it.
                        if (project.ExpandedName == project.OriginalName)
                        {
                            result.Add(project);
                            continue;
                        }

                        // If the path is absolute and it *starts* with some `$(..)` vars,
                        // turn it into a relative path (it might be in the form `$(ProjectDir)/../Lib`
                        // and we don't want that to be turned into an absolute path in the converted project).
                        //
                        // If the path is absolute but the `$(..)` vars are *inside* of it (like `C:\$(..)\Lib`),
                        // instead of at the start, we can keep those vars, i.e., skip this `if` block.
                        //
                        // The `OriginalName` is absolute if there are no `$(..)` vars at the start.
                        if (!Path.IsPathFullyQualified(project.OriginalName))
                        {
                            project = project.WithName(Path.GetRelativePath(relativeTo: targetDirectory, path: project.Name), CSharpDirective.Project.NameKind.Final);
                            result.Add(project);
                            continue;
                        }
                    }

                    // If the original path is to a directory, just append the resolved file name
                    // but preserve the variables from the original, e.g., `../$(..)/Directory/Project.csproj`.
                    if (Directory.Exists(Path.Combine(sourceDirectory, project.ExpandedName)))
                    {
                        var projectFileName = Path.GetFileName(project.Name);
                        project = project.WithName(Path.Join(project.OriginalName, projectFileName), CSharpDirective.Project.NameKind.Final);
                    }

                    project = project.WithName(Path.GetRelativePath(relativeTo: targetDirectory, path: Path.Combine(sourceDirectory, project.Name)), CSharpDirective.Project.NameKind.Final);
                    result.Add(project);
                    continue;
                }

                result.Add(directive);
            }

            return result.DrainToImmutable();
        }

        IEnumerable<(string name, string value)> GetDefaultProperties()
        {
            foreach (var (name, defaultValue) in VirtualProjectBuilder.GetDefaultProperties(VirtualProjectBuildingCommand.TargetFrameworkVersion))
            {
                string projectValue = projectInstance.GetPropertyValue(name);
                if (string.Equals(projectValue, defaultValue, StringComparison.OrdinalIgnoreCase))
                {
                    yield return (name, defaultValue);
                }
            }
        }
    }

    private string DetermineOutputDirectory(string file)
    {
        string defaultValue = Path.ChangeExtension(file, null);
        string defaultValueRelative = Path.GetRelativePath(relativeTo: Environment.CurrentDirectory, defaultValue);
        string targetDirectory = _outputDirectory
            ?? TryAskForOutputDirectory(defaultValueRelative)
            ?? defaultValue;
        if (Directory.Exists(targetDirectory))
        {
            throw new GracefulException(CliCommandStrings.DirectoryAlreadyExists, targetDirectory);
        }

        return targetDirectory;
    }

    private string? TryAskForOutputDirectory(string defaultValueRelative)
    {
        return InteractiveConsole.Ask<string?>(
            string.Format(CliCommandStrings.ProjectConvertAskForOutputDirectory, defaultValueRelative),
            _parseResult,
            (path, out result, [NotNullWhen(returnValue: false)] out error) =>
            {
                if (Directory.Exists(path))
                {
                    result = null;
                    error = string.Format(CliCommandStrings.DirectoryAlreadyExists, Path.GetFullPath(path));
                    return false;
                }

                result = path is null ? null : Path.GetFullPath(path);
                error = null;
                return true;
            },
            out var result)
            ? result
            : null;
    }
}
