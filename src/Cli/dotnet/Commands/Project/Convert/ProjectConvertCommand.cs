// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;
using Microsoft.Extensions.FileSystemGlobbing;
using Spectre.Console;

namespace Microsoft.DotNet.Cli.Commands.Project.Convert;

internal sealed class ProjectConvertCommand : CommandBase<ProjectConvertCommandDefinition>
{
    private readonly string _file;
    private readonly string? _outputDirectory;
    private readonly bool _force;
    private readonly bool _dryRun;
    private readonly bool _deleteSource;
    private readonly bool _interactive;

    public ProjectConvertCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _file = parseResult.GetValue(Definition.FileArgument) ?? string.Empty;
        _outputDirectory = parseResult.GetValue(Definition.OutputOption)?.FullName;
        _force = parseResult.GetValue(Definition.ForceOption);
        _dryRun = parseResult.GetValue(Definition.DryRunOption);
        _deleteSource = parseResult.GetValue(Definition.DeleteSourceOption);
        _interactive = parseResult.GetValue(Definition.InteractiveOption);
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

        var builder = new VirtualProjectBuilder(file, VirtualProjectBuildingCommand.TargetFramework);

        builder.CreateProjectInstance(
            projectCollection,
            VirtualProjectBuildingCommand.ThrowingReporter,
            out var projectInstance,
            projectRootElement: out _,
            out var evaluatedDirectives,
            validateAllDirectives: !_force);

        // When the entry point has #:ref directives, place all converted projects in subfolders.
        bool hasRefs = evaluatedDirectives.Any(static d => d is CSharpDirective.Ref);
        string entryPointName = Path.GetFileNameWithoutExtension(file);
        string entryPointOutputDir = hasRefs ? Path.Combine(targetDirectory, entryPointName) : targetDirectory;

        // Pre-validate ref target directories (check for duplicates and existing dirs).
        if (hasRefs)
        {
            var usedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entryPointName };
            ValidateRefTargetDirectories(evaluatedDirectives, Path.GetDirectoryName(file)!,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), usedFolderNames);
        }

        var (_, _, _, includeItems) = ConvertFile(file, entryPointOutputDir, isEntryPointFile: true);

        // Convert referenced files (#:ref directives) into library projects.
        var convertedRefFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refIncludeItems = new List<(string ItemType, string FullPath, string RelativePath)>();
        ConvertReferencedFiles(evaluatedDirectives, Path.GetDirectoryName(file)!);

        // Handle deletion of source files if requested.
        bool shouldDelete = _deleteSource || TryAskForDeleteSource();
        if (shouldDelete)
        {
            // Delete the entry point file
            DeleteFile(file);

            // Delete all included items (e.g., via #:include directives and default items)
            foreach (var item in includeItems)
            {
                DeleteFile(item.FullPath);
            }

            // Delete converted referenced files and their included items
            foreach (var refFile in convertedRefFiles)
            {
                DeleteFile(refFile);
            }

            foreach (var item in refIncludeItems)
            {
                DeleteFile(item.FullPath);
            }
        }

        return 0;

        (VirtualProjectBuilder builder, ProjectInstance projectInstance, ImmutableArray<CSharpDirective> evaluatedDirectives, List<(string ItemType, string FullPath, string RelativePath)> includeItems)
            ConvertFile(string sourceFile, string outputDirectory, bool isEntryPointFile)
        {
            var sourceDirectory = Path.GetDirectoryName(sourceFile)!;

            VirtualProjectBuilder fileBuilder;
            ProjectInstance fileProjectInstance;
            ImmutableArray<CSharpDirective> fileDirectives;

            if (isEntryPointFile)
            {
                fileBuilder = builder;
                fileProjectInstance = projectInstance;
                fileDirectives = evaluatedDirectives;
            }
            else
            {
                fileBuilder = new VirtualProjectBuilder(sourceFile, VirtualProjectBuildingCommand.TargetFramework);

                fileBuilder.CreateProjectInstance(
                    projectCollection,
                    VirtualProjectBuildingCommand.ThrowingReporter,
                    out fileProjectInstance,
                    projectRootElement: out _,
                    out fileDirectives,
                    validateAllDirectives: !_force);
            }

            // Find other items to copy over, e.g., default Content items like JSON files in Web apps.
            var includeItems = FindIncludedItems(fileBuilder, fileProjectInstance, sourceFile).ToList();

            CreateDirectory(outputDirectory);

            // Copy the .cs file with directives removed.
            var targetFile = Path.Join(outputDirectory, Path.GetFileName(sourceFile));
            if (_dryRun)
            {
                Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCopyFile, sourceFile, targetFile);
                Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldConvertFile, targetFile);
            }
            else
            {
                VirtualProjectBuildingCommand.RemoveDirectivesFromFile(fileBuilder.EntryPointSourceFile, targetFile);
            }

            CopyIncludedItems(includeItems, outputDirectory);

            // Create project file.
            var projectFile = Path.Join(outputDirectory, Path.GetFileNameWithoutExtension(sourceFile) + ".csproj");
            if (_dryRun)
            {
                Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCreateFile, projectFile);
            }
            else
            {
                var projectDirectives = UpdateDirectives(fileDirectives, sourceDirectory, outputDirectory);
                WriteProjectFile(projectFile, projectDirectives);

                var explicitProjectItemDirectives = FindExplicitProjectItemDirectives(
                    projectFile,
                    sourceDirectory,
                    outputDirectory,
                    includeItems,
                    fileDirectives).ToImmutableArray();
                if (!explicitProjectItemDirectives.IsEmpty)
                {
                    WriteProjectFile(projectFile, projectDirectives, explicitProjectItemDirectives);
                }
            }

            return (fileBuilder, fileProjectInstance, fileDirectives, includeItems);

            void WriteProjectFile(
                string projectFile,
                ImmutableArray<CSharpDirective> projectDirectives,
                ImmutableArray<CSharpDirective.IncludeOrExclude> explicitProjectItemDirectives = default)
            {
                using var stream = File.Open(projectFile, FileMode.Create, FileAccess.Write);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                VirtualProjectBuilder.WriteProjectFile(
                    writer,
                    projectDirectives,
                    GetDefaultProperties(fileProjectInstance),
                    isVirtualProject: false,
                    userSecretsId: isEntryPointFile ? DetermineUserSecretsId(fileProjectInstance) : null,
                    explicitProjectItemDirectives: explicitProjectItemDirectives);
            }
        }

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

        void DeleteFile(string path)
        {
            if (_dryRun)
            {
                Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldDeleteSourceFile, path);
            }
            else
            {
                File.Delete(path);
                Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertDeletedSourceFile, path);
            }
        }

        void CopyIncludedItems(List<(string ItemType, string FullPath, string RelativePath)> items, string outputDirectory)
        {
            foreach (var item in items)
            {
                string targetItemFullPath = Path.Combine(outputDirectory, item.RelativePath);

                // Ignore already-copied files.
                if (File.Exists(targetItemFullPath))
                {
                    continue;
                }

                string targetItemDirectory = Path.GetDirectoryName(targetItemFullPath)!;
                CreateDirectory(targetItemDirectory);

                if (item.ItemType == "Compile")
                {
                    if (_dryRun)
                    {
                        Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldCopyFile, item.FullPath, targetItemFullPath);
                        Reporter.Output.WriteLine(CliCommandStrings.ProjectConvertWouldConvertFile, targetItemFullPath);
                    }
                    else
                    {
                        var sourceFile = SourceFile.Load(item.FullPath);
                        VirtualProjectBuildingCommand.RemoveDirectivesFromFile(sourceFile, targetItemFullPath);
                    }
                }
                else
                {
                    CopyFile(item.FullPath, targetItemFullPath);
                }
            }
        }

        void ConvertReferencedFiles(ImmutableArray<CSharpDirective> directives, string sourceDirectory)
        {
            foreach (var directive in directives)
            {
                if (directive is not CSharpDirective.Ref refDirective)
                {
                    continue;
                }

                var refPath = refDirective.ResolvedPath ?? Path.GetFullPath(Path.Combine(sourceDirectory, refDirective.Name.Replace('\\', '/')));

                if (!convertedRefFiles.Add(refPath))
                {
                    continue;
                }

                var refName = Path.GetFileNameWithoutExtension(refPath);
                var refDir = Path.GetDirectoryName(refPath)!;
                var refTargetDirectory = Path.Combine(targetDirectory, refName);

                var (_, _, refEvaluatedDirectives, items) = ConvertFile(refPath, refTargetDirectory, isEntryPointFile: false);

                refIncludeItems.AddRange(items);

                // Recursively convert referenced files in the referenced file.
                ConvertReferencedFiles(refEvaluatedDirectives, refDir);
            }
        }

        void ValidateRefTargetDirectories(ImmutableArray<CSharpDirective> directives, string sourceDirectory, HashSet<string> visited, HashSet<string> usedFolderNames)
        {
            foreach (var directive in directives)
            {
                if (directive is not CSharpDirective.Ref refDirective)
                {
                    continue;
                }

                var refPath = refDirective.ResolvedPath ?? Path.GetFullPath(Path.Combine(sourceDirectory, refDirective.Name.Replace('\\', '/')));

                if (!visited.Add(refPath))
                {
                    continue;
                }

                var refName = Path.GetFileNameWithoutExtension(refPath);
                var refDir = Path.GetDirectoryName(refPath)!;
                var refTargetDirectory = Path.Combine(targetDirectory, refName);

                if (!usedFolderNames.Add(refName))
                {
                    throw new GracefulException(CliCommandStrings.ProjectConvertDuplicateRefFolderName, refTargetDirectory);
                }

                if (Directory.Exists(refTargetDirectory))
                {
                    throw new GracefulException(CliCommandStrings.DirectoryAlreadyExists, refTargetDirectory);
                }

                // Recursively validate transitive refs.
                var refBuilder = new VirtualProjectBuilder(refPath, VirtualProjectBuildingCommand.TargetFramework);
                refBuilder.CreateProjectInstance(
                    projectCollection,
                    VirtualProjectBuildingCommand.ThrowingReporter,
                    project: out _,
                    projectRootElement: out _,
                    out var refDirectives,
                    validateAllDirectives: !_force);
                ValidateRefTargetDirectories(refDirectives, refDir, visited, usedFolderNames);
            }
        }

        IEnumerable<(string ItemType, string FullPath, string RelativePath)> FindIncludedItems(
            VirtualProjectBuilder fileBuilder, ProjectInstance fileProjectInstance, string sourceFile)
        {
            string sourceFileDirectory = PathUtilities.EnsureTrailingSlash(Path.GetDirectoryName(sourceFile)!);

            // Include only items we know are files.
            var mapping = fileBuilder.GetItemMapping(fileProjectInstance, VirtualProjectBuildingCommand.ThrowingReporter);

            var items = mapping.SelectMany(e => fileProjectInstance.GetItems(e.ItemType));

            var topLevelFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                // Escape hatch - exclude items that have metadata `ExcludeFromFileBasedAppConversion` set to `true`.
                string include = item.GetMetadataValue("ExcludeFromFileBasedAppConversion");
                if (string.Equals(include, bool.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string itemFullPath = Path.GetFullPath(path: item.GetMetadataValue("FullPath"), basePath: sourceFileDirectory);

                // Exclude items that do not exist.
                if (!File.Exists(itemFullPath))
                {
                    continue;
                }

                string itemRelativePath = Path.GetRelativePath(relativeTo: sourceFileDirectory, path: itemFullPath);

                // Files outside the source directory should be copied into the target directory at the top level.
                // Possibly with a number suffix to avoid conflicts.
                // For C# files, this is needed so we can remove directives from them.
                // For others, this is consistent but also we can omit the item groups from the converted project file and keep it simple.
                if (itemRelativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    itemRelativePath = Path.GetFileName(itemFullPath);
                    string fileNameWithoutExtension;
                    string extension;
                    if (!topLevelFileNames.Add(itemRelativePath))
                    {
                        fileNameWithoutExtension = Path.GetFileNameWithoutExtension(itemRelativePath);
                        extension = Path.GetExtension(itemRelativePath);

                        var counter = 1;
                        do
                        {
                            counter++;
                            itemRelativePath = $"{fileNameWithoutExtension}_{counter}{extension}";
                        }
                        while (!topLevelFileNames.Add(itemRelativePath));
                    }
                }

                yield return (item.ItemType, FullPath: itemFullPath, RelativePath: itemRelativePath);
            }
        }

        IEnumerable<CSharpDirective.IncludeOrExclude> FindExplicitProjectItemDirectives(
            string projectFile,
            string sourceDirectory,
            string outputDirectory,
            List<(string ItemType, string FullPath, string RelativePath)> includeItems,
            ImmutableArray<CSharpDirective> directives)
        {
            // The converted project is evaluated after files are copied so SDK defaults can pick up
            // items such as Compile/None/Content naturally. Keep only the #:include directives whose
            // copied items are missing from that evaluation so the project writer doesn't infer item
            // types from mapping again.
            var candidateItems = includeItems
                .Select(item => (item.ItemType, item.RelativePath, SourceFullPath: item.FullPath, OutputFullPath: Path.GetFullPath(Path.Combine(outputDirectory, item.RelativePath))))
                .Distinct()
                .ToArray();

            if (candidateItems.Length == 0)
            {
                yield break;
            }

            using var outputProjectCollection = new ProjectCollection();
            var outputProject = ProjectInstance.FromFile(projectFile, new ProjectOptions
            {
                ProjectCollection = outputProjectCollection,
            });

            var itemComparer = new ProjectItemComparer();
            var automaticallyIncludedItems = new HashSet<(string ItemType, string FullPath)>(itemComparer);
            foreach (var itemType in candidateItems.Select(static item => item.ItemType).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var item in outputProject.GetItems(itemType))
                {
                    var fullPath = Path.GetFullPath(item.GetMetadataValue("FullPath"), outputDirectory);
                    automaticallyIncludedItems.Add((itemType, fullPath));
                }
            }

            var addedExplicitItems = new HashSet<(string ItemType, string FullPath)>(itemComparer);
            foreach (var directive in directives.OfType<CSharpDirective.IncludeOrExclude>())
            {
                if (directive.Kind != CSharpDirective.IncludeOrExcludeKind.Include || directive.ItemType is null)
                {
                    continue;
                }

                var missingItems = candidateItems
                    .Where(item => string.Equals(item.ItemType, directive.ItemType, StringComparison.OrdinalIgnoreCase) &&
                        DirectiveIncludesItem(directive, sourceDirectory, item.SourceFullPath))
                    .Where(item =>
                    {
                        var itemKey = (item.ItemType, item.OutputFullPath);
                        return !automaticallyIncludedItems.Contains(itemKey) && addedExplicitItems.Add(itemKey);
                    })
                    .ToArray();

                if (missingItems.Length == 0)
                {
                    continue;
                }

                if (TryGetOutputDirectiveName(directive, sourceDirectory, outputDirectory, missingItems, out var directiveName))
                {
                    yield return directive.WithName(directiveName);
                    continue;
                }

                foreach (var item in missingItems)
                {
                    yield return directive.WithName(item.RelativePath);
                }
            }

            static bool DirectiveIncludesItem(CSharpDirective.IncludeOrExclude directive, string sourceDirectory, string itemFullPath)
            {
                if (!HasWildcards(directive.Name))
                {
                    return string.Equals(Path.GetFullPath(directive.Name), itemFullPath, StringComparison.OrdinalIgnoreCase);
                }

                if (!IsInDirectory(directive.Name, sourceDirectory))
                {
                    return false;
                }

                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(NormalizePath(Path.GetRelativePath(sourceDirectory, directive.Name)));
                return matcher.Match(NormalizePath(Path.GetRelativePath(sourceDirectory, itemFullPath))).HasMatches;
            }

            static bool TryGetOutputDirectiveName(
                CSharpDirective.IncludeOrExclude directive,
                string sourceDirectory,
                string outputDirectory,
                (string ItemType, string RelativePath, string SourceFullPath, string OutputFullPath)[] missingItems,
                [NotNullWhen(true)] out string? directiveName)
            {
                // Like project/ref conversion, rewrite paths to be relative to the converted output shape.
                // Files under the source directory keep the same relative path after being copied; files
                // copied from outside the source directory are placed at the output root.
                if (IsInDirectory(directive.Name, sourceDirectory))
                {
                    var outputPath = Path.Combine(outputDirectory, Path.GetRelativePath(sourceDirectory, directive.Name));
                    directiveName = Path.GetRelativePath(outputDirectory, outputPath);
                    return true;
                }

                if (missingItems.Length == 1)
                {
                    directiveName = Path.GetRelativePath(outputDirectory, missingItems[0].OutputFullPath);
                    return true;
                }

                directiveName = null;
                return false;
            }

            static bool HasWildcards(string path) => path.Contains('*') || path.Contains('?');

            static bool IsInDirectory(string path, string directory)
            {
                var fullPath = Path.GetFullPath(path);
                var fullDirectory = PathUtilities.EnsureTrailingSlash(Path.GetFullPath(directory));
                return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
            }

            static string NormalizePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        string? DetermineUserSecretsId(ProjectInstance projectInstance)
        {
            var implicitValue = projectInstance.GetPropertyValue("_ImplicitFileBasedProgramUserSecretsId");
            var actualValue = projectInstance.GetPropertyValue("UserSecretsId");
            return implicitValue == actualValue ? actualValue : null;
        }

        ImmutableArray<CSharpDirective> UpdateDirectives(ImmutableArray<CSharpDirective> directives, string? sourceDirectory = null, string? outputDirectory = null)
        {
            sourceDirectory ??= Path.GetDirectoryName(file)!;
            outputDirectory ??= targetDirectory;

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
                            project = project.WithName(Path.GetRelativePath(relativeTo: outputDirectory, path: project.Name), CSharpDirective.Project.NameKind.Final);
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

                    project = project.WithName(Path.GetRelativePath(relativeTo: outputDirectory, path: Path.Combine(sourceDirectory, project.Name)), CSharpDirective.Project.NameKind.Final);
                    result.Add(project);
                    continue;
                }

                // Convert #:ref directives to #:project directives pointing to the referenced file's
                // expected converted project location (i.e., subfolder of the output directory named after the .cs file).
                if (directive is CSharpDirective.Ref refDirective)
                {
                    var refPath = refDirective.ResolvedPath ?? Path.GetFullPath(Path.Combine(sourceDirectory, refDirective.Name.Replace('\\', '/')));
                    var refName = Path.GetFileNameWithoutExtension(refPath);

                    // The referenced file's converted project is expected at: <targetDirectory>/<refName>/<refName>.csproj
                    var convertedProjectPath = Path.Combine(targetDirectory, refName, refName + ".csproj");
                    var relativePath = Path.GetRelativePath(relativeTo: outputDirectory, path: convertedProjectPath);

                    result.Add(new CSharpDirective.Project(refDirective.Info, relativePath)
                    {
                        OriginalName = refDirective.OriginalName,
                    });
                    continue;
                }

                if (directive is CSharpDirective.IncludeOrExclude)
                {
                    continue;
                }

                result.Add(directive);
            }

            return result.DrainToImmutable();
        }

        IEnumerable<(string name, string value)> GetDefaultProperties(ProjectInstance projectInstance)
        {
            foreach (var (name, defaultValue) in VirtualProjectBuilder.GetDefaultProperties(VirtualProjectBuildingCommand.TargetFramework))
            {
                string projectValue = projectInstance.GetPropertyValue(name);
                if (string.Equals(projectValue, defaultValue, StringComparison.OrdinalIgnoreCase))
                {
                    yield return (name, defaultValue);
                }
            }
        }
    }

    private sealed class ProjectItemComparer : IEqualityComparer<(string ItemType, string FullPath)>
    {
        public bool Equals((string ItemType, string FullPath) x, (string ItemType, string FullPath) y)
        {
            return string.Equals(x.ItemType, y.ItemType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.FullPath, y.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string ItemType, string FullPath) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ItemType),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.FullPath));
        }
    }

    private string DetermineOutputDirectory(string file)
    {
        string defaultValue = Path.ChangeExtension(file, null);
        string defaultValueRelative = Path.GetRelativePath(relativeTo: Environment.CurrentDirectory, defaultValue);

        string targetDirectory;

        // Use CLI-provided output directory if specified
        if (_outputDirectory != null)
        {
            targetDirectory = _outputDirectory;
        }
        // In interactive mode, prompt for output directory
        else if (_interactive)
        {
            try
            {
                var prompt = new TextPrompt<string>(string.Format(CliCommandStrings.ProjectConvertAskForOutputDirectory, defaultValueRelative))
                    .AllowEmpty()
                    .Validate(path =>
                    {
                        // Determine the actual path to validate
                        string pathToValidate = string.IsNullOrWhiteSpace(path) ? defaultValue : Path.GetFullPath(path);

                        if (Directory.Exists(pathToValidate))
                        {
                            return ValidationResult.Error(string.Format(CliCommandStrings.DirectoryAlreadyExists, pathToValidate));
                        }

                        return ValidationResult.Success();
                    });

                var answer = Spectre.Console.AnsiConsole.Prompt(prompt);
                targetDirectory = string.IsNullOrWhiteSpace(answer) ? defaultValue : Path.GetFullPath(answer);
            }
            catch (Exception)
            {
                targetDirectory = defaultValue;
            }
        }
        // Non-interactive mode, use default
        else
        {
            targetDirectory = defaultValue;
        }

        // Validate that directory doesn't exist
        if (Directory.Exists(targetDirectory))
        {
            throw new GracefulException(CliCommandStrings.DirectoryAlreadyExists, targetDirectory);
        }

        return targetDirectory;
    }

    private bool TryAskForDeleteSource()
    {
        if (!_interactive)
        {
            return false;
        }

        try
        {
            var choice = Spectre.Console.AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[cyan]{Markup.Escape(CliCommandStrings.ProjectConvertAskDeleteSource)}[/]")
                    .AddChoices([CliCommandStrings.ProjectConvertDeleteSourceChoiceYes, CliCommandStrings.ProjectConvertDeleteSourceChoiceNo])
            );

            return choice == CliCommandStrings.ProjectConvertDeleteSourceChoiceYes;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
