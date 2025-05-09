// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly string _fileOrDirectory = parseResult.GetValue(ProjectConvertCommandParser.FileOrDirectoryArgument)!;
    private readonly string? _outputDirectory = parseResult.GetValue(SharedOptions.OutputOption)?.FullName;
    private readonly bool _force = parseResult.GetValue(ProjectConvertCommandParser.ForceOption);
    private readonly string _sharedDirectoryName = parseResult.GetValue(ProjectConvertCommandParser.SharedDirectoryNameOption)!;

    public override int Execute()
    {
        // Check target directory.
        if (_outputDirectory != null && Directory.Exists(_outputDirectory))
        {
            throw new GracefulException(CliCommandStrings.DirectoryAlreadyExists, _outputDirectory);
        }

        // Check entry-point file path.
        string fileOrDirectory = Path.GetFullPath(_fileOrDirectory);
        bool isFile = VirtualProjectBuildingCommand.IsValidEntryPointPath(fileOrDirectory);
        if (!isFile && (File.Exists(fileOrDirectory) || !Directory.Exists(fileOrDirectory)))
        {
            throw new GracefulException(CliCommandStrings.InvalidFileOrDirectoryPath, fileOrDirectory);
        }

        // Discover other C# files.
        SourceFile? entryPointSourceFile = isFile ? VirtualProjectBuildingCommand.LoadSourceFile(fileOrDirectory) : null;
        VirtualProjectBuildingCommand.DiscoverOtherFiles(
            entryPointFile: entryPointSourceFile,
            entryDirectory: isFile ? null : new DirectoryInfo(fileOrDirectory),
            parseDirectivesFromOtherEntryPoints: true,
            reportAllDirectiveErrors: !_force,
            otherEntryPoints: out var otherEntryPoints,
            parsedFiles: out var parsedFiles);

        // If there are other entry points, a directory must be specified (so it's clear that we convert all the entry points, not just the specified one).
        if (isFile && otherEntryPoints.Length != 0)
        {
            throw new GracefulException(CliCommandStrings.DirectoryMustBeSpecified, fileOrDirectory);
        }

        ReadOnlySpan<string> currentEntryPoint = entryPointSourceFile is { } file ? [file.Path] : [];
        ReadOnlySpan<string> allEntryPoints = [.. currentEntryPoint, .. otherEntryPoints];

        // Check there are some entry points.
        if (allEntryPoints.Length == 0)
        {
            throw new GracefulException(CliCommandStrings.NoEntryPoints, fileOrDirectory);
        }

        // Discover other non-C# files and directories at the top level.
        string sourceDirectory = isFile ? Path.GetDirectoryName(fileOrDirectory)! : fileOrDirectory;
        string[] nonCSharpTopLevelFiles = Directory.EnumerateFiles(sourceDirectory)
            .Where(f => !f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string[] topLevelDirs = Directory.GetDirectories(sourceDirectory);

        // We create a plan of what to do first. No changes are done here so we don't fail in an intermediate state.
        // First we need to create Shared folder and copy all existing folders and non-entry-point or non-C# files to it.
        // Then we convert the C# files (remove directives from them and create csproj files for the entry-point ones).
        // That way we re-create the source directory structure inside the Shared folder and also handle a situation where user has a folder with the same name as one of the entry points
        // (we need to move the folder first to Shared and then convert the entry point which will re-create the folder and copy the converted entry point into it).
        var actions = new List<Action>();

        // Determine the base target directory.
        string baseTargetDirectory;
        if (_outputDirectory != null)
        {
            baseTargetDirectory = _outputDirectory;
            actions.Add(() => Directory.CreateDirectory(baseTargetDirectory));
        }
        else
        {
            baseTargetDirectory = sourceDirectory;
        }

        string? sharedDirectory;
        bool creatingSharedDirectory;

        // If there are multiple entry points and some non-C# or non-entry-point files/dirs, we need a Shared folder.
        Debug.Assert(parsedFiles.Count >= allEntryPoints.Length);
        if (allEntryPoints.Length > 1 && (nonCSharpTopLevelFiles.Length > 0 || topLevelDirs.Length > 0 || parsedFiles.Count > allEntryPoints.Length))
        {
            sharedDirectory = Path.Join(baseTargetDirectory, _sharedDirectoryName);
            actions.Add(() => Directory.CreateDirectory(sharedDirectory));
            creatingSharedDirectory = true;
        }
        else
        {
            // We also need to move other files to the target folder if it's specified.
            sharedDirectory = _outputDirectory != null ? baseTargetDirectory : null;
            creatingSharedDirectory = false;
        }

        // Move non-C# files and directories.
        if (nonCSharpTopLevelFiles.Length > 0 || topLevelDirs.Length > 0)
        {
            if (sharedDirectory != null)
            {
                actions.Add(() =>
                {
                    foreach (var dir in topLevelDirs)
                    {
                        string target = GetTargetTopLevelPath(sharedDirectory, dir);
                        PathUtility.SafeRenameDirectory(dir, target);
                    }

                    foreach (var file in nonCSharpTopLevelFiles)
                    {
                        string target = GetTargetTopLevelPath(sharedDirectory, file);
                        File.Move(file, target);
                    }
                });
            }
        }

        // Process C# files.
        foreach (var parsed in parsedFiles.Values)
        {
            string targetDirectory;
            bool deleteSourceFiles;

            if (parsed.IsEntryPoint)
            {
                Debug.Assert(string.IsNullOrEmpty(Path.GetDirectoryName(Path.GetRelativePath(relativeTo: sourceDirectory, path: parsed.File.Path))),
                    "Entry points are expected to be at the top level.");

                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(parsed.File.Path);

                if (creatingSharedDirectory && string.Equals(fileNameWithoutExtension, _sharedDirectoryName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new GracefulException(CliCommandStrings.SharedDirectoryNameConflicts, _sharedDirectoryName);
                }

                // If there is a single entry point, generate the project directly in the output folder, otherwise create a subfolder.
                if (allEntryPoints.Length > 1)
                {
                    targetDirectory = Path.Join(baseTargetDirectory, fileNameWithoutExtension);
                    actions.Add(() => Directory.CreateDirectory(targetDirectory));
                    deleteSourceFiles = true;
                }
                else
                {
                    targetDirectory = baseTargetDirectory;
                    deleteSourceFiles = _outputDirectory != null;
                }

                // Generate a project file.
                string projectFile = Path.Join(targetDirectory, fileNameWithoutExtension + ".csproj");
                actions.Add(() =>
                {
                    using (var csprojStream = File.Open(projectFile, FileMode.Create, FileAccess.Write))
                    using (var csprojWriter = new StreamWriter(csprojStream, Encoding.UTF8))
                    {
                        VirtualProjectBuildingCommand.WriteProjectFile(csprojWriter, parsed.SortedDirectives, options: new ProjectWritingOptions.Converted
                        {
                            SharedDirectoryName = creatingSharedDirectory ? _sharedDirectoryName : null,
                        });
                    }
                });
            }
            else
            {
                // If the file is nested, we have already moved it to the shared folder, so process it in place.
                string? relativeDirectoryPath = Path.GetDirectoryName(Path.GetRelativePath(relativeTo: sourceDirectory, path: parsed.File.Path));
                if (!string.IsNullOrEmpty(relativeDirectoryPath))
                {
                    targetDirectory = Path.Join(sharedDirectory, relativeDirectoryPath);
                    deleteSourceFiles = false;
                }
                else if (sharedDirectory != null)
                {
                    targetDirectory = sharedDirectory;
                    deleteSourceFiles = true;
                }
                else
                {
                    Debug.Assert(_outputDirectory == null);
                    targetDirectory = baseTargetDirectory;
                    deleteSourceFiles = false;
                }
            }

            // Remove directives. Write the converted file or move it if no conversion is needed.
            string targetFilePath = GetTargetTopLevelPath(targetDirectory, parsed.File.Path);
            actions.Add(() =>
            {
                if (VirtualProjectBuildingCommand.RemoveDirectivesFromFile(parsed.Directives, parsed.File.Text) is { } convertedEntryPointFileText)
                {
                    using var stream = File.Open(targetFilePath, FileMode.Create, FileAccess.Write);
                    using var writer = new StreamWriter(stream, Encoding.UTF8);
                    convertedEntryPointFileText.Write(writer);

                    if (deleteSourceFiles)
                    {
                        File.Delete(parsed.File.Path);
                    }
                }
                else if (deleteSourceFiles)
                {
                    File.Move(parsed.File.Path, targetFilePath);
                }
            });
        }

        // Execute actions.
        actions.ForEach(static action => action());

        return 0;

        static string GetTargetTopLevelPath(string targetDirectory, string sourceFilePath)
        {
            return Path.Join(targetDirectory, Path.GetFileName(sourceFilePath));
        }
    }
}
