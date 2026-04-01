// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Commands.NuGet;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectTools;

namespace Microsoft.DotNet.Cli.Commands.Package.Add;

internal sealed class PackageAddCommand : CommandBase<PackageAddCommandDefinitionBase>
{
    private readonly PackageIdentityWithRange _packageId;

    public PackageAddCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _packageId = parseResult.GetValue(Definition.PackageIdArgument);
    }

    public override int Execute()
    {
        var (fileOrDirectory, allowedAppKinds) = PackageCommandParser.ProcessPathOptions(Definition.FileOption, Definition.ProjectOption, Definition.GetProjectOrFileArgument(), _parseResult);

        bool isFileBasedApp = allowedAppKinds.HasFlag(AppKinds.FileBased) && VirtualProjectBuilder.IsValidEntryPointPath(fileOrDirectory);

        Debug.Assert(isFileBasedApp || allowedAppKinds.HasFlag(AppKinds.ProjectBased));

        string projectFilePath;
        if (!File.Exists(fileOrDirectory))
        {
            Debug.Assert(!isFileBasedApp);
            projectFilePath = MsbuildProject.GetProjectFileFromDirectory(fileOrDirectory);
        }
        else
        {
            projectFilePath = fileOrDirectory;
        }

        if (isFileBasedApp)
        {
            projectFilePath = Path.GetFullPath(projectFilePath);
        }

        var tempDgFilePath = string.Empty;

        if (!_parseResult.GetValue(Definition.NoRestoreOption))
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

            GetProjectDependencyGraph(projectFilePath, tempDgFilePath, isFileBasedApp);
        }

        var args = TransformArgs(
            _packageId,
            tempDgFilePath,
            projectFilePath);

        var result = NuGetCommand.Run(args, isFileBasedApp);

        DisposeTemporaryFile(tempDgFilePath);

        return result;
    }

    private static void GetProjectDependencyGraph(string projectFilePath, string dgFilePath, bool isFileBasedApp)
    {
        int result;
        if (isFileBasedApp)
        {
            result = new VirtualProjectBuildingCommand(
                projectFilePath,
                MSBuildArgs
                    .FromProperties(new Dictionary<string, string>
                    {
                        { "RestoreGraphOutputPath", dgFilePath },
                        { "RestoreRecursive", "false" },
                        { "RestoreDotnetCliToolReferences", "false" },
                    }.AsReadOnly())
                    .CloneWithVerbosity(VerbosityOptions.quiet)
                    .CloneWithAdditionalTargets("GenerateRestoreGraphFile"))
            {
                NoRestore = true,
                NoCache = true,
                NoWriteBuildMarkers = true,
            }.Execute();
        }
        else
        {
            result = new MSBuildForwardingApp(
                [
                    // Pass the project file path
                    projectFilePath,

                    // Pass the task as generate restore Dependency Graph file
                    "-target:GenerateRestoreGraphFile",

                    // Pass Dependency Graph file output path
                    $"-property:RestoreGraphOutputPath=\"{dgFilePath}\"",

                    // Turn off recursive restore
                    "-property:RestoreRecursive=false",

                    // Turn off restore for Dotnet cli tool references so that we do not generate extra dg specs
                    "-property:RestoreDotnetCliToolReferences=false",

                    // Output should not include MSBuild version header
                    "--nologo",

                    // Set verbosity to quiet to avoid cluttering the output for this 'inner' build
                    "-v:quiet"
                ]).Execute();
        }

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

        if (_parseResult.GetValue(Definition.NoRestoreOption))
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
