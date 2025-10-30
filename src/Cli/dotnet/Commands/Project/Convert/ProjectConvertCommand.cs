// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly string _file = parseResult.GetValue(ProjectConvertCommandParser.FileArgument) ?? string.Empty;
    private readonly string? _outputDirectory = parseResult.GetValue(SharedOptions.OutputOption)?.FullName;
    private readonly bool _force = parseResult.GetValue(ProjectConvertCommandParser.ForceOption);

    public override int Execute()
    {
        // Check the entry point file path.
        string file = Path.GetFullPath(_file);
        if (!VirtualProjectBuildingCommand.IsValidEntryPointPath(file))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, file);
        }

        string targetDirectory = DetermineOutputDirectory(file);

        // Find directives (this can fail, so do this before creating the target directory).
        var sourceFile = SourceFile.Load(file);
        var diagnostics = DiagnosticBag.ThrowOnFirst();
        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: !_force, diagnostics);

        // Create a project instance for evaluation.
        var projectCollection = new ProjectCollection();
        var command = new VirtualProjectBuildingCommand(
            entryPointFileFullPath: file,
            msbuildArgs: MSBuildArgs.FromOtherArgs([]))
        {
            Directives = directives,
        };
        var projectInstance = command.CreateProjectInstance(projectCollection);

        // Evaluate directives.
        directives = VirtualProjectBuildingCommand.EvaluateDirectives(projectInstance, directives, sourceFile, diagnostics);
        command.Directives = directives;
        projectInstance = command.CreateProjectInstance(projectCollection);

        // Find other items to copy over, e.g., default Content items like JSON files in Web apps.
        var includeItems = FindIncludedItems().ToList();

        bool dryRun = _parseResult.GetValue(ProjectConvertCommandParser.DryRunOption);

        CreateDirectory(targetDirectory);

        var targetFile = Path.Join(targetDirectory, Path.GetFileName(file));

        // Process the entry point file.
        if (dryRun)
        {
            Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCopyFile, file, targetFile);
            Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldConvertFile, targetFile);
        }
        else
        {
            VirtualProjectBuildingCommand.RemoveDirectivesFromFile(directives, sourceFile.Text, targetFile);
        }

        // Create project file.
        string projectFile = Path.Join(targetDirectory, Path.GetFileNameWithoutExtension(file) + ".csproj");
        if (dryRun)
        {
            Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCreateFile, projectFile);
        }
        else
        {
            using var stream = File.Open(projectFile, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            VirtualProjectBuildingCommand.WriteProjectFile(writer, UpdateDirectives(directives), isVirtualProject: false,
                userSecretsId: DetermineUserSecretsId(),
                excludeDefaultProperties: FindDefaultPropertiesToExclude());
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
            if (dryRun)
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
            if (dryRun)
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
            string entryPointFileDirectory = PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(file)!);

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
                    if (Path.IsPathFullyQualified(project.Name))
                    {
                        // If the path is absolute and has no `$(..)` vars, just keep it.
                        if (project.UnresolvedName == project.OriginalName)
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
                            project = project.WithName(Path.GetRelativePath(relativeTo: targetDirectory, path: project.Name));
                            result.Add(project);
                            continue;
                        }
                    }

                    // If the original path is to a directory, just append the resolved file name
                    // but preserve the variables from the original, e.g., `../$(..)/Directory/Project.csproj`.
                    if (Directory.Exists(Path.Combine(sourceDirectory, project.UnresolvedName)))
                    {
                        var projectFileName = Path.GetFileName(project.Name);
                        project = project.WithName(Path.Join(project.OriginalName, projectFileName));
                    }

                    project = project.WithName(Path.GetRelativePath(relativeTo: targetDirectory, path: Path.Combine(sourceDirectory, project.Name)));
                    result.Add(project);
                    continue;
                }

                result.Add(directive);
            }

            return result.DrainToImmutable();
        }

        IEnumerable<string> FindDefaultPropertiesToExclude()
        {
            foreach (var (name, defaultValue) in VirtualProjectBuildingCommand.DefaultProperties)
            {
                string projectValue = projectInstance.GetPropertyValue(name);
                if (!string.Equals(projectValue, defaultValue, StringComparison.OrdinalIgnoreCase))
                {
                    yield return name;
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
