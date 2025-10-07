// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using CreateShellShimRepository = Microsoft.DotNet.Cli.Commands.Tool.Install.CreateShellShimRepository;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader, IToolPackageUninstaller) CreateToolPackageStoresAndDownloaderAndUninstaller(
    DirectoryPath? nonGlobalLocation = null,
    IEnumerable<string> additionalRestoreArguments = null,
    string currentWorkingDirectory = null);

internal class ToolUpdateGlobalOrToolPathCommand : CommandBase
{
    private readonly CreateShellShimRepository _createShellShimRepository;
    private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
    internal readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;

    public ToolUpdateGlobalOrToolPathCommand(ParseResult parseResult,
        CreateToolPackageStoresAndDownloaderAndUninstaller createToolPackageStoreDownloaderUninstaller = null,
        CreateShellShimRepository createShellShimRepository = null,
        IReporter reporter = null,
        IToolPackageStoreQuery _store = null)
        : base(parseResult)
    {
        _createToolPackageStoreDownloaderUninstaller = createToolPackageStoreDownloaderUninstaller ??
                                              ToolPackageFactory.CreateToolPackageStoresAndDownloaderAndUninstaller;

        _createShellShimRepository =
            createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;

        PackageId? packageId = null;
        if (parseResult.GetValue(ToolUpdateCommandParser.PackageIdentityArgument)?.Id is string s)
        {
            packageId = new PackageId(s);
        }

        _toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                parseResult,
                packageId,
                _createToolPackageStoreDownloaderUninstaller,
                _createShellShimRepository,
                reporter: reporter,
                store: _store);
    }

    public override int Execute()
    {
        _toolInstallGlobalOrToolPathCommand.Execute();
        return 0;
    }
}
