// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Remove;

internal sealed class SdkRemoveCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly SdkRemoveCommandDefinitionBase _definition = (SdkRemoveCommandDefinitionBase)parseResult.CommandResult.Command;

    public override int Execute()
    {
        var sdkNames = DeduplicateSdkNames(_parseResult.GetValue(_definition.SdkIdArgument) ?? []);

        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(
            _definition.FileOption,
            _definition.ProjectOption,
            _definition.GetProjectOrFileArgument(),
            _parseResult);

        if (allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory))
        {
            return ExecuteForFileBasedApp(fileOrDirectory, sdkNames);
        }

        Debug.Assert(allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFilePath = File.Exists(fileOrDirectory)
            ? fileOrDirectory
            : MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory);

        bool interactive = _parseResult.GetValue(_definition.InteractiveOption);
        return ExecuteForProject(projectFilePath, sdkNames, interactive);
    }

    private static int ExecuteForProject(string projectFilePath, IEnumerable<string> sdkNames, bool interactive)
    {
        using var projects = new ProjectCollection();
        var msbuildProject = MsbuildProject.FromFile(projects, projectFilePath, interactive);
        var project = msbuildProject.ProjectRootElement;
        var sdkNameList = sdkNames as IList<string> ?? [.. sdkNames];

        foreach (var sdkName in sdkNameList)
        {
            if (ProjectSdkReferenceHelper.IsPrimarySdkReference(project, sdkName))
            {
                throw new GracefulException(CliCommandStrings.CannotRemovePrimarySdkReference, sdkName);
            }
        }

        var removed = new List<string>();
        var notFound = new List<string>();

        foreach (var sdkName in sdkNameList)
        {
            if (ProjectSdkReferenceHelper.TryRemoveSdk(project, sdkName))
            {
                removed.Add(sdkName);
            }
            else
            {
                notFound.Add(sdkName);
            }
        }

        if (removed.Count > 0)
        {
            msbuildProject.ProjectRootElement.Save();
        }

        foreach (var sdkName in removed)
        {
            Reporter.Output.WriteLine(CliCommandStrings.SdkReferenceRemovedFromProject, sdkName, projectFilePath);
        }

        foreach (var sdkName in notFound)
        {
            Reporter.Output.WriteLine(CliCommandStrings.SdkReferenceNotFoundInProject, sdkName, projectFilePath);
        }

        return removed.Count > 0 ? 0 : 1;
    }

    private static int ExecuteForFileBasedApp(string path, IEnumerable<string> sdkNames)
    {
        var fullPath = Path.GetFullPath(path);
        var editor = FileBasedAppSourceEditor.Load(SourceFile.Load(fullPath));

        var primarySdk = editor.Directives.OfType<CSharpDirective.Sdk>().FirstOrDefault();
        var sdkNameList = sdkNames as IList<string> ?? [.. sdkNames];

        foreach (var sdkName in sdkNameList)
        {
            if (primarySdk != null &&
                string.Equals(primarySdk.Name, sdkName, StringComparison.OrdinalIgnoreCase))
            {
                throw new GracefulException(CliCommandStrings.CannotRemovePrimarySdkReferenceInFile, sdkName);
            }
        }

        var removedCounts = new List<(string SdkName, int Count)>();
        var notFound = new List<string>();

        foreach (var sdkName in sdkNameList)
        {
            int count = 0;
            var directives = editor.Directives;
            for (int i = directives.Length - 1; i >= 0; i--)
            {
                var directive = directives[i];
                if (directive is CSharpDirective.Sdk sdk &&
                    string.Equals(sdk.Name, sdkName, StringComparison.OrdinalIgnoreCase))
                {
                    editor.Remove(directive);
                    count++;
                }
            }

            if (count > 0)
            {
                removedCounts.Add((sdkName, count));
            }
            else
            {
                notFound.Add(sdkName);
            }
        }

        if (removedCounts.Count > 0)
        {
            editor.SourceFile.Save();
        }

        foreach (var (sdkName, count) in removedCounts)
        {
            Reporter.Output.WriteLine(CliCommandStrings.DirectivesRemoved, "#:sdk", count, sdkName, fullPath);
        }

        foreach (var sdkName in notFound)
        {
            Reporter.Output.WriteLine(CliCommandStrings.SdkReferenceNotFoundInFile, sdkName, fullPath);
        }

        return removedCounts.Count > 0 ? 0 : 1;
    }

    private static List<string> DeduplicateSdkNames(IEnumerable<string> sdkNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (string sdkName in sdkNames)
        {
            if (seen.Add(sdkName))
            {
                result.Add(sdkName);
            }
        }

        return result;
    }
}
