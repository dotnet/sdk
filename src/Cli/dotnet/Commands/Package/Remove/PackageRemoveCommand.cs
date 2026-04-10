// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Package.Remove;

internal sealed class PackageRemoveCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly PackageRemoveCommandDefinitionBase _definition = (PackageRemoveCommandDefinitionBase)parseResult.CommandResult.Command;

    public override int Execute()
    {
        var arguments = _parseResult.GetValue(_definition.CmdPackageArgument) ?? [];

        if (arguments is not [{ } packageToRemove])
        {
            throw new GracefulException(CliCommandStrings.PackageRemoveSpecifyExactlyOnePackageReference);
        }

        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(_definition.FileOption, _definition.ProjectOption, projectOrFileArgument: null, _parseResult);

        if (allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory))
        {
            return ExecuteForFileBasedApp(path: fileOrDirectory, packageId: packageToRemove);
        }

        Debug.Assert(allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFilePath;
        if (!File.Exists(fileOrDirectory))
        {
            projectFilePath = MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory);
        }
        else
        {
            projectFilePath = fileOrDirectory;
        }

        var result = NuGetCommand.Run(TransformArgs(packageToRemove, projectFilePath));

        return result;
    }

    private string[] TransformArgs(string packageId, string projectFilePath)
    {
        var args = new List<string>()
        {
            "package",
            "remove",
            "--package",
            packageId,
            "--project",
            projectFilePath
        };

        args.AddRange(_parseResult
            .OptionValuesToBeForwarded(new PackageRemoveCommandDefinition())
            .SelectMany(a => a.Split(' ')));

        return [.. args];
    }

    private static int ExecuteForFileBasedApp(string path, string packageId)
    {
        var fullPath = Path.GetFullPath(path);

        // Remove #:package directive from the C# file.
        // We go through the directives in reverse order so removing one doesn't affect spans of the remaining ones.
        var editor = FileBasedAppSourceEditor.Load(SourceFile.Load(fullPath));
        var count = 0;
        var directives = editor.Directives;
        for (int i = directives.Length - 1; i >= 0; i--)
        {
            var directive = directives[i];
            if (directive is CSharpDirective.Package p &&
                string.Equals(p.Name, packageId, StringComparison.OrdinalIgnoreCase))
            {
                editor.Remove(directive);
                count++;
            }
        }
        editor.SourceFile.Save();

        Reporter.Output.WriteLine(CliCommandStrings.DirectivesRemoved, "#:package", count, packageId, fullPath);
        return count > 0 ? 0 : 1; // success if any directives were found and removed
    }
}
