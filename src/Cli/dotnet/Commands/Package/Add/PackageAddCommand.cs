// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Package.Add;

/// <param name="parseResult"></param>
/// <param name="fileOrDirectory">
/// Since this command is invoked via both 'package add' and 'add package', different symbols will control what the project path to search is.
/// It's cleaner for the separate callsites to know this instead of pushing that logic here.
/// </param>
internal class PackageAddCommand(ParseResult parseResult, string fileOrDirectory) : CommandBase(parseResult)
{
    private readonly PackageIdentityWithRange _packageId = parseResult.GetValue(PackageAddCommandParser.CmdPackageArgument);

    public override int Execute()
    {
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
            "-nologo"
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
            args.Add(packageId.VersionRange.OriginalString);
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
}
