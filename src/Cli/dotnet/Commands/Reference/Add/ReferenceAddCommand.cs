// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Commands.Reference.Add;

internal sealed class ReferenceAddCommand : CommandBase<ReferenceAddCommandDefinitionBase>
{
    private readonly string _fileOrDirectory;
    private readonly AppKinds _allowedAppKinds;

    public ReferenceAddCommand(ParseResult parseResult)
        : base(parseResult)
    {
        (_fileOrDirectory, _allowedAppKinds) = PackageCommandParser.ProcessPathOptions(
            Definition.GetFileOption(),
            Definition.GetProjectOption(),
            Definition.GetProjectOrFileArgument(),
            parseResult);
    }

    public override int Execute()
    {
        using var projects = new ProjectCollection();
        bool interactive = _parseResult.GetValue(Definition.InteractiveOption);

        if (_allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(_fileOrDirectory))
        {
            return ExecuteForFileBasedApp(projects, interactive);
        }

        if (!_allowedAppKinds.HasFlag(AppKinds.ProjectBased))
        {
            throw new GracefulException(CliCommandStrings.InvalidFilePath, _fileOrDirectory);
        }

        MsbuildProject msbuildProj = MsbuildProject.FromFileOrDirectory(
            projects,
            _fileOrDirectory,
            interactive);

        var frameworkString = _parseResult.GetValue(Definition.FrameworkOption);

        var arguments = _parseResult.GetValue(Definition.ProjectPathArgument).ToList().AsReadOnly();
        PathUtility.EnsureAllPathsExist(arguments,
            CliStrings.CouldNotFindProjectOrDirectory, true);
        List<MsbuildProject> refs = [.. arguments.Select((r) => MsbuildProject.FromFileOrDirectory(projects, r, interactive))];

        if (string.IsNullOrEmpty(frameworkString))
        {
            foreach (var tfm in msbuildProj.GetTargetFrameworks())
            {
                foreach (var @ref in refs)
                {
                    if (!@ref.CanWorkOnFramework(tfm))
                    {
                        Reporter.Error.Write(GetProjectNotCompatibleWithFrameworksDisplayString(
                                                 @ref,
                                                 msbuildProj.GetTargetFrameworks().Select((fx) => fx.GetShortFolderName())));
                        return 1;
                    }
                }
            }
        }
        else
        {
            var framework = NuGetFramework.Parse(frameworkString);
            if (!msbuildProj.IsTargetingFramework(framework))
            {
                Reporter.Error.WriteLine(string.Format(
                                             CliStrings.ProjectDoesNotTargetFramework,
                                             msbuildProj.ProjectRootElement.FullPath,
                                             frameworkString));
                return 1;
            }

            foreach (var @ref in refs)
            {
                if (!@ref.CanWorkOnFramework(framework))
                {
                    Reporter.Error.Write(GetProjectNotCompatibleWithFrameworksDisplayString(@ref, [frameworkString]));
                    return 1;
                }
            }
        }

        var relativePathReferences = refs.Select((r) =>
                                                    Path.GetRelativePath(
                                                        msbuildProj.ProjectDirectory,
                                                        r.ProjectRootElement.FullPath)).ToList();

        int numberOfAddedReferences = msbuildProj.AddProjectToProjectReferences(
            frameworkString,
            relativePathReferences);

        if (numberOfAddedReferences != 0)
        {
            msbuildProj.ProjectRootElement.Save();
        }

        return 0;
    }

    private int ExecuteForFileBasedApp(ProjectCollection projects, bool interactive)
    {
        if (!string.IsNullOrEmpty(_parseResult.GetValue(Definition.FrameworkOption)))
        {
            throw new GracefulException(CliCommandStrings.InvalidOptionForFileBasedApp, Definition.FrameworkOption.Name);
        }

        var arguments = _parseResult.GetValue(Definition.ProjectPathArgument).ToList().AsReadOnly();
        PathUtility.EnsureAllPathsExist(arguments, CliStrings.CouldNotFindProjectOrDirectory, true);

        var editor = new FileBasedAppReferenceEditor(_fileOrDirectory);
        var projectReferenceArguments = new List<string>();
        var fileBasedAppReferenceArguments = new List<string>();
        foreach (var argument in arguments)
        {
            if (VirtualProjectBuilder.IsValidEntryPointPath(argument))
            {
                fileBasedAppReferenceArguments.Add(argument);
            }
            else
            {
                projectReferenceArguments.Add(argument);
            }
        }

        List<MsbuildProject> refs = [.. projectReferenceArguments.Select((r) => MsbuildProject.FromFileOrDirectory(projects, r, interactive))];
        var targetFramework = NuGetFramework.Parse(VirtualProjectBuildingCommand.TargetFramework);
        foreach (var @ref in refs)
        {
            if (!@ref.CanWorkOnFramework(targetFramework))
            {
                Reporter.Error.Write(GetProjectNotCompatibleWithFrameworksDisplayString(@ref, [VirtualProjectBuildingCommand.TargetFramework]));
                return 1;
            }
        }

        int numberOfAddedReferences = editor.AddProjectReferences(
            refs.Zip(projectReferenceArguments, (reference, argument) =>
                (ProjectFilePath: reference.ProjectRootElement.FullPath,
                 DirectiveInclude: GetDirectiveInclude(argument, editor.EntryPointFileDirectory))));

        numberOfAddedReferences += editor.AddFileBasedAppReferences(fileBasedAppReferenceArguments.Select(argument =>
            (FilePath: Path.GetFullPath(argument),
             DirectiveInclude: GetDirectiveInclude(argument, editor.EntryPointFileDirectory))));

        if (numberOfAddedReferences != 0)
        {
            editor.Save();
        }

        return 0;

        static string GetDirectiveInclude(string argument, string projectDirectory)
        {
            return Path.GetRelativePath(projectDirectory, Path.GetFullPath(argument)).Replace('\\', '/');
        }
    }

    private static string GetProjectNotCompatibleWithFrameworksDisplayString(MsbuildProject project, IEnumerable<string> frameworksDisplayStrings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format(CliStrings.ProjectNotCompatibleWithFrameworks, project.ProjectRootElement.FullPath));
        foreach (var tfm in frameworksDisplayStrings)
        {
            sb.AppendLine($"    - {tfm}");
        }

        return sb.ToString();
    }
}
