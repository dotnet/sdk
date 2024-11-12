// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.Uninstall;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using CreateShellShimRepository = Microsoft.DotNet.Tools.Tool.Install.CreateShellShimRepository;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageDownloader, IToolPackageUninstaller) CreateToolPackageStoresAndDownloaderAndUninstaller(
        DirectoryPath? nonGlobalLocation = null,
        IEnumerable<string> additionalRestoreArguments = null);

    internal class ToolUpdateGlobalOrToolPathCommand : CommandBase
    {
        private readonly CreateShellShimRepository _createShellShimRepository;
        private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _createToolPackageStoreDownloaderUninstaller;
        internal readonly ToolInstallGlobalOrToolPathCommand _toolInstallGlobalOrToolPathCommand;

        public ToolUpdateGlobalOrToolPathCommand(ParseResult parseResult,
            CreateToolPackageStoresAndDownloaderAndUninstaller createToolPackageStoreDownloaderUninstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            _createToolPackageStoreDownloaderUninstaller = createToolPackageStoreDownloaderUninstaller ??
                                                  ToolPackageFactory.CreateToolPackageStoresAndDownloaderAndUninstaller;

            _createShellShimRepository =
                createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;
                
            _toolInstallGlobalOrToolPathCommand = new ToolInstallGlobalOrToolPathCommand(
                    parseResult,
                    _createToolPackageStoreDownloaderUninstaller,
                    _createShellShimRepository,
                    reporter: reporter);
        }

        public override int Execute()
        {
            _toolInstallGlobalOrToolPathCommand.Execute();
            return 0;
        }
    }
}
