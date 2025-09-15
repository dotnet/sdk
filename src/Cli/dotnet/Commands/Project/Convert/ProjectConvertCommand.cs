// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: !_force, DiagnosticBag.ThrowOnFirst());

        // Create a project instance for evaluation.
        string entryPointFileDirectory = PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(file)!);
        var projectCollection = new ProjectCollection();
        var command = new VirtualProjectBuildingCommand(
            entryPointFileFullPath: file,
            msbuildArgs: MSBuildArgs.FromOtherArgs([]))
        {
            Directives = directives,
        };
        var projectInstance = command.CreateProjectInstance(projectCollection);

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
            VirtualProjectBuildingCommand.WriteProjectFile(writer, directives, isVirtualProject: false,
                userSecretsId: DetermineUserSecretsId());
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
