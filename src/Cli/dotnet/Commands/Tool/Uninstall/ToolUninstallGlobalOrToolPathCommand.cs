// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Transactions;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Uninstall;

internal delegate IShellShimRepository CreateShellShimRepository(string appHostSourceDirectory, DirectoryPath? nonGlobalLocation = null);
internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageUninstaller) CreateToolPackageStoresAndUninstaller(DirectoryPath? nonGlobalLocation = null);
internal class ToolUninstallGlobalOrToolPathCommand(
    ParseResult result,
    CreateToolPackageStoresAndUninstaller createToolPackageStoreAndUninstaller = null,
    CreateShellShimRepository createShellShimRepository = null,
    IReporter reporter = null) : CommandBase(result)
{
    private readonly IReporter _reporter = reporter ?? Reporter.Output;
    private readonly IReporter _errorReporter = reporter ?? Reporter.Error;
    private readonly CreateShellShimRepository _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;
    private readonly CreateToolPackageStoresAndUninstaller _createToolPackageStoresAndUninstaller = createToolPackageStoreAndUninstaller ??
                                                ToolPackageFactory.CreateToolPackageStoresAndUninstaller;

    public override int Execute()
    {
        var global = _parseResult.GetValue(ToolUninstallCommandParser.GlobalOption);
        var toolPath = _parseResult.GetValue(ToolUninstallCommandParser.ToolPathOption);

        DirectoryPath? toolDirectoryPath = null;
        if (!string.IsNullOrWhiteSpace(toolPath))
        {
            if (!Directory.Exists(toolPath))
            {
                throw new GracefulException(
                    string.Format(
                        CliCommandStrings.ToolUninstallInvalidToolPathOption,
                        toolPath));
            }

            toolDirectoryPath = new DirectoryPath(toolPath);
        }

        (IToolPackageStore toolPackageStore, IToolPackageStoreQuery toolPackageStoreQuery, IToolPackageUninstaller toolPackageUninstaller)
            = _createToolPackageStoresAndUninstaller(toolDirectoryPath);
        var appHostSourceDirectory = ShellShimTemplateFinder.GetDefaultAppHostSourceDirectory();
        IShellShimRepository shellShimRepository = _createShellShimRepository(appHostSourceDirectory, toolDirectoryPath);

        var packageId = new PackageId(_parseResult.GetValue(ToolUninstallCommandParser.PackageIdArgument));
        IToolPackage package = null;
        try
        {
            package = toolPackageStoreQuery.EnumeratePackageVersions(packageId).SingleOrDefault();
            if (package == null)
            {
                throw new GracefulException(messages: [string.Format(CliCommandStrings.ToolUninstallToolNotInstalled, packageId)], isUserError: false);
            }
        }
        catch (InvalidOperationException)
        {
            throw new GracefulException(messages: [string.Format(CliCommandStrings.ToolUninstallToolHasMultipleVersionsInstalled, packageId)], isUserError: false);
        }

        try
        {
            TransactionalAction.Run(() =>
            {
                shellShimRepository.RemoveShim(package.Command);

                toolPackageUninstaller.Uninstall(package.PackageDirectory);
            });

            _reporter.WriteLine(
                string.Format(
                    CliCommandStrings.ToolUninstallUninstallSucceeded,
                    package.Id,
                    package.Version.ToNormalizedString()).Green());
            return 0;
        }
        catch (Exception ex) when (ToolUninstallCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
        {
            throw new GracefulException(
                messages: ToolUninstallCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId),
                verboseMessages: [ex.ToString()],
                isUserError: false);
        }
    }
}
