// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Commands.Package.Add;

internal class PackageAddCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly PackageIdentityWithRange _packageId = parseResult.GetValue(PackageAddCommandParser.CmdPackageArgument)!;

    public override int Execute()
    {
        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(_parseResult);

        if (allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuildingCommand.IsValidEntryPointPath(fileOrDirectory))
        {
            return ExecuteForFileBasedApp(fileOrDirectory);
        }

        Debug.Assert(allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFilePath;
        if (!File.Exists(fileOrDirectory))
        {
            projectFilePath = MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory).FullName;
        }
        else
        {
            projectFilePath = fileOrDirectory;
        }

        var tempDgFilePath = string.Empty;

        if (!_parseResult.GetValue(PackageAddCommandParser.NoRestoreOption))
        {

            try
            {
                // Create a Dependency Graph file for the project
                tempDgFilePath = Path.GetTempFileName();
            }
            catch (IOException ioex)
            {
                // Catch IOException from Path.GetTempFileName() and throw a graceful exception to the user.
                throw new GracefulException(string.Format(CliCommandStrings.CmdDGFileIOException, projectFilePath), ioex);
            }

            GetProjectDependencyGraph(projectFilePath, tempDgFilePath);
        }

        var result = NuGetCommand.Run(
            TransformArgs(
                _packageId,
                tempDgFilePath,
                projectFilePath));
        DisposeTemporaryFile(tempDgFilePath);

        return result;
    }

    private static void GetProjectDependencyGraph(string projectFilePath, string dgFilePath)
    {
        List<string> args =
        [
            // Pass the project file path
            projectFilePath,

            // Pass the task as generate restore Dependency Graph file
            "-target:GenerateRestoreGraphFile",

            // Pass Dependency Graph file output path
            $"-property:RestoreGraphOutputPath=\"{dgFilePath}\"",

            // Turn off recursive restore
            $"-property:RestoreRecursive=false",

            // Turn off restore for Dotnet cli tool references so that we do not generate extra dg specs
            $"-property:RestoreDotnetCliToolReferences=false",

            // Output should not include MSBuild version header
            "-nologo",

            // Set verbosity to quiet to avoid cluttering the output for this 'inner' build
            "-v:quiet"
        ];

        var result = new MSBuildForwardingApp(args).Execute();

        if (result != 0)
        {
            throw new GracefulException(string.Format(CliCommandStrings.CmdDGFileException, projectFilePath));
        }
    }

    private static void DisposeTemporaryFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private string[] TransformArgs(PackageIdentityWithRange packageId, string tempDgFilePath, string projectFilePath)
    {
        List<string> args = [
            "package",
            "add",
            "--package",
            packageId.Id,
            "--project",
            projectFilePath
        ];

        if (packageId.HasVersion)
        {
            args.Add("--version");
            args.Add(packageId.VersionRange.OriginalString ?? string.Empty);
        }

        args.AddRange(_parseResult
            .OptionValuesToBeForwarded()
            .SelectMany(a => a.Split(' ', 2)));

        if (_parseResult.GetValue(PackageAddCommandParser.NoRestoreOption))
        {
            args.Add("--no-restore");
        }
        else
        {
            args.Add("--dg-file");
            args.Add(tempDgFilePath);
        }

        return [.. args];
    }

    // More logic should live in NuGet: https://github.com/NuGet/Home/issues/14390
    private int ExecuteForFileBasedApp(string path)
    {
        // Check disallowed options.
        ReadOnlySpan<Option> disallowedOptions =
        [
            PackageAddCommandParser.FrameworkOption,
            PackageAddCommandParser.SourceOption,
            PackageAddCommandParser.PackageDirOption,
        ];
        foreach (var option in disallowedOptions)
        {
            if (_parseResult.HasOption(option))
            {
                throw new GracefulException(CliCommandStrings.InvalidOptionForFileBasedApp, option.Name);
            }
        }

        string? specifiedVersion = _packageId.HasVersion
            ? _packageId.VersionRange?.OriginalString ?? string.Empty
            : _parseResult.GetValue(PackageAddCommandParser.VersionOption);
        bool prerelease = _parseResult.GetValue(PackageAddCommandParser.PrereleaseOption);

        if (specifiedVersion != null && prerelease)
        {
            throw new GracefulException(CliCommandStrings.PrereleaseAndVersionAreNotSupportedAtTheSameTime);
        }

        var fullPath = Path.GetFullPath(path);

        // Create restore command, used also for obtaining MSBuild properties.
        bool interactive = _parseResult.GetValue(PackageAddCommandParser.InteractiveOption);
        var command = new VirtualProjectBuildingCommand(
            entryPointFileFullPath: fullPath,
            msbuildArgs: MSBuildArgs.FromProperties(new Dictionary<string, string>(2)
            {
                ["NuGetInteractive"] = interactive.ToString(),
                // Floating versions are needed if user did not specify a version
                // - then we restore with version '*' to determine the latest version.
                ["CentralPackageFloatingVersionsEnabled"] = bool.TrueString,
            }.AsReadOnly()))
        {
            NoCache = true,
            NoBuild = true,
        };
        var projectCollection = new ProjectCollection();
        var projectInstance = command.CreateProjectInstance(projectCollection);

        // Set initial version to Directory.Packages.props and/or C# file
        // (we always need to add the package reference to the C# file but when CPM is enabled, it's added without a version).
        string version = specifiedVersion ?? (prerelease ? "*-*" : "*");
        bool skipUpdate = false;
        var central = SetCentralVersion(version);
        var local = SetLocalVersion(central != null ? null : version);

        if (!_parseResult.GetValue(PackageAddCommandParser.NoRestoreOption))
        {
            // Restore.
            int exitCode = command.Execute();
            if (exitCode != 0)
            {
                // If restore fails, revert any changes made.
                central?.Revert();
                return exitCode;
            }

            // If no version was specified by the user, save the actually restored version.
            if (specifiedVersion == null && !skipUpdate)
            {
                var projectAssetsFile = projectInstance.GetProperty("ProjectAssetsFile")?.EvaluatedValue;
                if (!File.Exists(projectAssetsFile))
                {
                    Reporter.Verbose.WriteLine($"Assets file does not exist: {projectAssetsFile}");
                }
                else
                {
                    var lockFile = new LockFileFormat().Read(projectAssetsFile);
                    var library = lockFile.Libraries.FirstOrDefault(l => string.Equals(l.Name, _packageId.Id, StringComparison.OrdinalIgnoreCase));
                    if (library != null)
                    {
                        var restoredVersion = library.Version.ToString();
                        if (central is { } centralValue)
                        {
                            centralValue.Update(restoredVersion);
                            local.Save();
                        }
                        else
                        {
                            local.Update(restoredVersion);
                        }

                        return 0;
                    }
                }
            }
        }

        central?.Save();
        local.Save();
        return 0;

        (Action Save, Action<string> Update) SetLocalVersion(string? version)
        {
            // Add #:package directive to the C# file.
            var file = SourceFile.Load(fullPath);
            var editor = FileBasedAppSourceEditor.Load(file);
            editor.Add(new CSharpDirective.Package(default) { Name = _packageId.Id, Version = version });
            command.Directives = editor.Directives;
            return (Save, Update);

            void Save()
            {
                editor.SourceFile.Save();
            }

            void Update(string value)
            {
                // Update the C# file with the given version.
                editor.Add(new CSharpDirective.Package(default) { Name = _packageId.Id, Version = value });
                editor.SourceFile.Save();
            }
        }

        (Action Revert, Action<string> Update, Action Save)? SetCentralVersion(string version)
        {
            // Find out whether CPM is enabled.
            if (!MSBuildUtilities.ConvertStringToBool(projectInstance.GetProperty("ManagePackageVersionsCentrally")?.EvaluatedValue))
            {
                return null;
            }

            // Load the Directory.Packages.props project.
            var directoryPackagesPropsPath = projectInstance.GetProperty("DirectoryPackagesPropsPath")?.EvaluatedValue;
            if (!File.Exists(directoryPackagesPropsPath))
            {
                Reporter.Verbose.WriteLine($"Directory.Packages.props file does not exist: {directoryPackagesPropsPath}");
                return null;
            }

            var snapshot = File.ReadAllText(directoryPackagesPropsPath);
            var directoryPackagesPropsProject = projectCollection.LoadProject(directoryPackagesPropsPath);

            const string packageVersionItemType = "PackageVersion";
            const string versionAttributeName = "Version";

            // Update existing PackageVersion if it exists.
            var packageVersion = directoryPackagesPropsProject.GetItems(packageVersionItemType)
                .LastOrDefault(i => string.Equals(i.EvaluatedInclude, _packageId.Id, StringComparison.OrdinalIgnoreCase));
            if (packageVersion != null)
            {
                var packageVersionItemElement = packageVersion.Project.GetItemProvenance(packageVersion).LastOrDefault()?.ItemElement;
                var versionAttribute = packageVersionItemElement?.Metadata.FirstOrDefault(i => i.Name.Equals(versionAttributeName, StringComparison.OrdinalIgnoreCase));
                if (versionAttribute != null)
                {
                    versionAttribute.Value = version;
                    directoryPackagesPropsProject.Save();

                    // If user didn't specify a version and a version is already specified in Directory.Packages.props,
                    // don't update the Directory.Packages.props (that's how the project-based equivalent behaves as well).
                    if (specifiedVersion == null)
                    {
                        skipUpdate = true;
                        return (Revert: NoOp, Update: Unreachable, Save: Revert);

                        static void NoOp() { }
                        static void Unreachable(string value) => Debug.Fail("Unreachable.");
                    }

                    return (Revert, v => Update(versionAttribute, v), Save);
                }
            }

            {
                // Get the ItemGroup to add a PackageVersion to or create a new one.
                var itemGroup = directoryPackagesPropsProject.Xml.ItemGroups
                        .Where(e => e.Items.Any(i => string.Equals(i.ItemType, packageVersionItemType, StringComparison.OrdinalIgnoreCase)))
                        .FirstOrDefault()
                    ?? directoryPackagesPropsProject.Xml.AddItemGroup();

                // Add a PackageVersion item.
                var item = itemGroup.AddItem(packageVersionItemType, _packageId.Id);
                var metadata = item.AddMetadata(versionAttributeName, version, expressAsAttribute: true);
                directoryPackagesPropsProject.Save();

                return (Revert, v => Update(metadata, v), Save);
            }

            void Update(ProjectMetadataElement element, string value)
            {
                element.Value = value;
                directoryPackagesPropsProject.Save();
            }

            void Revert()
            {
                File.WriteAllText(path: directoryPackagesPropsPath, contents: snapshot);
            }

            static void Save() { /* No-op by default. */ }
        }
    }
}
