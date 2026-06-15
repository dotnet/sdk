// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Add;

internal sealed class SdkAddCommand : CommandBase<SdkAddCommandDefinitionBase>
{
    private readonly SdkReferenceIdentity _sdkId;

    public SdkAddCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _sdkId = parseResult.GetValue(Definition.SdkIdArgument);
    }

    public override int Execute()
    {
        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(
            Definition.FileOption,
            Definition.ProjectOption,
            Definition.GetProjectOrFileArgument(),
            _parseResult);

        if (allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory))
        {
            return ExecuteForFileBasedApp(fileOrDirectory);
        }

        Debug.Assert(allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFilePath = File.Exists(fileOrDirectory)
            ? fileOrDirectory
            : MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory);

        return ExecuteForProject(projectFilePath);
    }

    private int ExecuteForProject(string projectFilePath)
    {
        bool userVersionSpecified = IsVersionSpecified();
        string? userVersion = GetSpecifiedVersion();
        var startDirectory = Path.GetDirectoryName(projectFilePath) ?? Environment.CurrentDirectory;

        bool interactive = _parseResult.GetValue(Definition.InteractiveOption);

        using var projects = new ProjectCollection();
        var msbuildProject = MsbuildProject.FromFile(projects, projectFilePath, interactive);

        if (!userVersionSpecified && ProjectSdkReferenceHelper.ContainsSdk(msbuildProject.ProjectRootElement, _sdkId.Id))
        {
            WriteAddResultMessage(SdkAddResult.Unchanged, version: null, projectFilePath, forFile: false);
            return 0;
        }

        var (version, leaveExistingUnchanged) = SdkAddVersionHelper.ResolveVersion(
            _sdkId.Id,
            userVersion,
            userVersionSpecified,
            startDirectory);

        SdkAddResult result = ProjectSdkReferenceHelper.AddOrUpdateSdk(
            msbuildProject.ProjectRootElement,
            _sdkId.Id,
            version,
            leaveExistingUnchanged);

        if (result == SdkAddResult.Unchanged)
        {
            WriteAddResultMessage(result, version, projectFilePath, forFile: false);
            return 0;
        }

        byte[] snapshot = File.ReadAllBytes(projectFilePath);
        msbuildProject.ProjectRootElement.Save();

        if (_parseResult.GetValue(Definition.NoRestoreOption))
        {
            WriteAddResultMessage(result, version, projectFilePath, forFile: false);
            return 0;
        }

        string[] restoreArgs = interactive
            ? [projectFilePath, "--nologo", "-v:q", "--interactive"]
            : [projectFilePath, "--nologo", "-v:q"];
        int exitCode = RestoreCommand.FromArgs(restoreArgs).Execute();
        if (exitCode != 0)
        {
            File.WriteAllBytes(projectFilePath, snapshot);
            return exitCode;
        }

        WriteAddResultMessage(result, version, projectFilePath, forFile: false);
        return 0;
    }

    private int ExecuteForFileBasedApp(string path)
    {
        bool userVersionSpecified = IsVersionSpecified();
        string? userVersion = GetSpecifiedVersion();
        var fullPath = Path.GetFullPath(path);
        var startDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;

        var file = SourceFile.Load(fullPath);
        var editor = FileBasedAppSourceEditor.Load(file);

        var existing = editor.Directives
            .OfType<CSharpDirective.Sdk>()
            .FirstOrDefault(s => string.Equals(s.Name, _sdkId.Id, StringComparison.OrdinalIgnoreCase));

        if (!userVersionSpecified && existing != null)
        {
            WriteAddResultMessage(SdkAddResult.Unchanged, version: null, fullPath, forFile: true);
            return 0;
        }

        var (version, _) = SdkAddVersionHelper.ResolveVersion(
            _sdkId.Id,
            userVersion,
            userVersionSpecified,
            startDirectory);

        SdkAddResult result;
        string? effectiveVersion = userVersionSpecified ? userVersion : version ?? existing?.Version;

        var command = new VirtualProjectBuildingCommand(
            entryPointFileFullPath: fullPath,
            msbuildArgs: MSBuildArgs.FromProperties(new Dictionary<string, string>(1)
            {
                ["NuGetInteractive"] = _parseResult.GetValue(Definition.InteractiveOption).ToString(),
            }.AsReadOnly()))
        {
            NoCache = true,
            NoBuild = true,
        };

        editor.Add(new CSharpDirective.Sdk(default) { Name = _sdkId.Id, Version = effectiveVersion });
        command.Directives = editor.Directives;
        result = existing != null ? SdkAddResult.Updated : SdkAddResult.Added;

        if (_parseResult.GetValue(Definition.NoRestoreOption))
        {
            editor.SourceFile.Save();
            WriteAddResultMessage(result, effectiveVersion, fullPath, forFile: true);
            return 0;
        }

        int exitCode = command.Execute();
        if (exitCode != 0)
        {
            return exitCode;
        }

        editor.SourceFile.Save();
        WriteAddResultMessage(result, effectiveVersion, fullPath, forFile: true);
        return 0;
    }

    private void WriteAddResultMessage(SdkAddResult result, string? version, string path, bool forFile)
    {
        switch (result)
        {
            case SdkAddResult.Added:
                Reporter.Output.WriteLine(
                    forFile ? CliCommandStrings.SdkReferenceAddedToFile : CliCommandStrings.SdkReferenceAddedToProject,
                    _sdkId.Id,
                    FormatVersionSuffix(version),
                    path);
                break;
            case SdkAddResult.Updated:
                Reporter.Output.WriteLine(
                    forFile ? CliCommandStrings.SdkReferenceUpdatedInFile : CliCommandStrings.SdkReferenceUpdatedInProject,
                    _sdkId.Id,
                    FormatVersionSuffix(version),
                    path);
                break;
            case SdkAddResult.Unchanged:
                Reporter.Output.WriteLine(
                    forFile ? CliCommandStrings.SdkReferenceUnchangedInFile : CliCommandStrings.SdkReferenceUnchangedInProject,
                    _sdkId.Id,
                    path);
                break;
        }
    }

    private string? GetSpecifiedVersion()
        => _sdkId.HasVersion ? _sdkId.Version : _parseResult.GetValue(Definition.VersionOption);

    private bool IsVersionSpecified()
        => _sdkId.HasVersion || _parseResult.HasOption(Definition.VersionOption);

    private static string FormatVersionSuffix(string? version)
        => string.IsNullOrEmpty(version) ? string.Empty : $" version '{version}'";
}
