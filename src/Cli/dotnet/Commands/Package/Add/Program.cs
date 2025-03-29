// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Tools.Package.Add;

internal class AddPackageReferenceCommand(ParseResult parseResult) : CommandBase(parseResult)
{
    private readonly string _packageId = parseResult.GetValue(PackageAddCommandParser.CmdPackageArgument);
    private readonly string _fileOrDirectory = parseResult.HasOption(PackageCommandParser.ProjectOption) ?
            parseResult.GetValue(PackageCommandParser.ProjectOption) :
            parseResult.GetValue(AddCommandParser.ProjectArgument);

    public override int Execute()
    {
        var projectFilePath = string.Empty;

        if (!File.Exists(_fileOrDirectory))
        {
            projectFilePath = MsbuildProject.GetProjectFileFromDirectory(_fileOrDirectory).FullName;
        }
        else
        {
            projectFilePath = _fileOrDirectory;
        }

        var tempDgFilePath = string.Empty;

        if (_parseResult.GetResult(PackageAddCommandParser.NoRestoreOption) is null)
        {

            try
            {
                // Create a Dependency Graph file for the project
                tempDgFilePath = Path.GetTempFileName();
            }
            catch (IOException ioex)
            {
                // Catch IOException from Path.GetTempFileName() and throw a graceful exception to the user.
                throw new GracefulException(string.Format(LocalizableStrings.CmdDGFileIOException, projectFilePath), ioex);
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

    private void GetProjectDependencyGraph(string projectFilePath, string dgFilePath)
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
            "-nologo"
        ];

        var result = new MSBuildForwardingApp(args).Execute();

        if (result != 0)
        {
            throw new GracefulException(string.Format(LocalizableStrings.CmdDGFileException, projectFilePath));
        }
    }

    private void DisposeTemporaryFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    private string[] TransformArgs(string packageId, string tempDgFilePath, string projectFilePath)
    {
        var args = new List<string>
        {
            "package",
            "add",
            "--package",
            packageId,
            "--project",
            projectFilePath
        };

        args.AddRange(_parseResult
            .OptionValuesToBeForwarded(PackageAddCommandParser.GetCommand())
            .SelectMany(a => a.Split(' ', 2)));

        if (_parseResult.GetResult(PackageAddCommandParser.NoRestoreOption) is not null)
        {
            args.Add("--no-restore");
        }
        else
        {
            args.Add("--dg-file");
            args.Add(tempDgFilePath);
        }

        return args.ToArray();
    }
}
