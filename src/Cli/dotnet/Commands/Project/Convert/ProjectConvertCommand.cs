// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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
        string file = Path.GetFullPath(_file);
        if (!VirtualProjectBuildingCommand.IsValidEntryPointPath(file))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, file);
        }

        string targetDirectory = _outputDirectory ?? Path.ChangeExtension(file, null);
        if (Directory.Exists(targetDirectory))
        {
            throw new GracefulException(CliCommandStrings.DirectoryAlreadyExists, targetDirectory);
        }

        // Find directives (this can fail, so do this before creating the target directory).
        var sourceFile = VirtualProjectBuildingCommand.LoadSourceFile(file);
        var directives = VirtualProjectBuildingCommand.FindDirectives(sourceFile, reportAllErrors: !_force, errors: null);

        // Find other items to copy over, e.g., default Content items like JSON files in Web apps.
        var includeItems = FindIncludedItems().ToList();

        Directory.CreateDirectory(targetDirectory);

        var targetFile = Path.Join(targetDirectory, Path.GetFileName(file));

        // If there were any directives, remove them from the file.
        if (directives.Length != 0)
        {
            VirtualProjectBuildingCommand.RemoveDirectivesFromFile(directives, sourceFile.Text, targetFile);
            File.Delete(file);
        }
        else
        {
            File.Move(file, targetFile);
        }

        // Create project file.
        string projectFile = Path.Join(targetDirectory, Path.GetFileNameWithoutExtension(file) + ".csproj");
        using var stream = File.Open(projectFile, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        VirtualProjectBuildingCommand.WriteProjectFile(writer, directives, isVirtualProject: false);

        // Copy over included items.
        foreach (var item in includeItems)
        {
            string targetItemFullPath = Path.Combine(targetDirectory, item.RelativePath);

            // Ignore already-copied files.
            if (File.Exists(targetItemFullPath))
            {
                continue;
            }

            string targetItemDirectory = Path.GetDirectoryName(targetItemFullPath)!;
            Directory.CreateDirectory(targetItemDirectory);
            File.Copy(item.FullPath, targetItemFullPath);
        }

        return 0;

        IEnumerable<(string FullPath, string RelativePath)> FindIncludedItems()
        {
            string entryPointFileDirectory = PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(file)!);
            var projectCollection = new ProjectCollection();
            var command = new VirtualProjectBuildingCommand(
                entryPointFileFullPath: file,
                msbuildArgs: MSBuildArgs.FromOtherArgs([]))
            {
                Directives = directives,
            };
            var projectInstance = command.CreateProjectInstance(projectCollection);

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
    }
}
