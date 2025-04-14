// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Cli.ToolPackage;
namespace Microsoft.DotNet.Cli.Commands.Tool.Runx;

internal class ToolRunxCommand(ParseResult result) : CommandBase(result)
{
    private readonly string _toolCommandName = result.GetValue(ToolRunxCommandParser.CommandNameArgument);
    private readonly IEnumerable<string> _toolArguments = result.GetValue(ToolRunxCommandParser.CommandArgument);

    public override int Execute()
    {
        PackageId packageId = new PackageId(_toolCommandName);

        var tempDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());

        // Acquire package

        ToolPackageStoreAndQuery toolPackageStoreAndQuery = ToolPackageFactory.CreateConcreteToolPackageStore(tempDir);
        ToolPackageDownloader toolPackageDownloader = new ToolPackageDownloader(toolPackageStoreAndQuery);
        ToolPackageUninstaller toolPackageUninstaller = new ToolPackageUninstaller(toolPackageStoreAndQuery);

        PackageLocation packageLocation = new PackageLocation(rootConfigDirectory: toolPackageStoreAndQuery.Root);

        IToolPackage toolPackage = toolPackageStoreAndQuery.EnumeratePackageVersions(packageId).FirstOrDefault()
            ?? toolPackageDownloader.InstallPackage(packageLocation, packageId, isGlobalTool: true);

        // Run package

        DotnetToolsCommandResolver dotnetToolsCommandResolver = new DotnetToolsCommandResolver(toolPackage.PackageDirectory.Value);
        CommandSpec commandSpec = dotnetToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                // since LocalToolsCommandResolver is a resolver, and all resolver input have dotnet-
                CommandName = _toolCommandName.StartsWith("dotnet-") ? _toolCommandName : $"dotnet-{_toolCommandName}",
                CommandArguments = _toolArguments,
            });

        var result = CommandFactoryUsingResolver.Create(commandSpec).Execute();

        return result.ExitCode;
    }
}
