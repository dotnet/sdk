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

    public override int Execute()
    {
        PackageLocation packageLocation = new PackageLocation();
        PackageId packageId = new PackageId(_toolCommandName);

        var tempDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());

        // Acquire package

        ToolPackageStoreAndQuery toolPackageStoreAndQuery = ToolPackageFactory.CreateConcreteToolPackageStore(tempDir);
        ToolPackageDownloader toolPackageDownloader = new ToolPackageDownloader(toolPackageStoreAndQuery);
        ToolPackageUninstaller toolPackageUninstaller = new ToolPackageUninstaller(toolPackageStoreAndQuery);

        IToolPackage toolPackage = toolPackageStoreAndQuery.EnumeratePackageVersions(packageId).FirstOrDefault()
            ?? toolPackageDownloader.InstallPackage(packageLocation, packageId);

        // Run package

        DotnetToolsCommandResolver dotnetToolsCommandResolver = new DotnetToolsCommandResolver(toolPackageStoreAndQuery.Root.ToString());
        CommandSpec commandSpec = dotnetToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                // since LocalToolsCommandResolver is a resolver, and all resolver input have dotnet-
                CommandName = $"dotnet-{_toolCommandName}",
                CommandArguments = new string[] { },
            });

        var result = CommandFactoryUsingResolver.Create(commandSpec).Execute();

        return result.ExitCode;
    }
}
