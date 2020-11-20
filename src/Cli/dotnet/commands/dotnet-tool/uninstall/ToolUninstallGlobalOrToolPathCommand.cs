// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ShellShim;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.Uninstall
{
    internal delegate IShellShimRepository CreateShellShimRepository(DirectoryPath? nonGlobalLocation = null);
    internal delegate (IToolPackageStore, IToolPackageStoreQuery, IToolPackageUninstaller) CreateToolPackageStoresAndUninstaller(DirectoryPath? nonGlobalLocation = null);
    internal class ToolUninstallGlobalOrToolPathCommand : CommandBase
    {
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private CreateShellShimRepository _createShellShimRepository;
        private CreateToolPackageStoresAndUninstaller _createToolPackageStoresAndUninstaller;

        public ToolUninstallGlobalOrToolPathCommand(
            ParseResult result,
            CreateToolPackageStoresAndUninstaller createToolPackageStoreAndUninstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IReporter reporter = null)
            : base(result)
        {
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;

            _createShellShimRepository = createShellShimRepository ?? ShellShimRepositoryFactory.CreateShellShimRepository;
            _createToolPackageStoresAndUninstaller = createToolPackageStoreAndUninstaller ??
                                                    ToolPackageFactory.CreateToolPackageStoresAndUninstaller;
        }

        public override int Execute()
        {
            var global = _parseResult.ValueForOption<bool>(ToolAppliedOption.GlobalOptionAliases.First());
            var toolPath = _parseResult.ValueForOption<string>(ToolAppliedOption.ToolPathOptionAlias);

            DirectoryPath? toolDirectoryPath = null;
            if (!string.IsNullOrWhiteSpace(toolPath))
            {
                if (!Directory.Exists(toolPath))
                {
                    throw new GracefulException(
                        string.Format(
                            LocalizableStrings.InvalidToolPathOption,
                            toolPath));
                }

                toolDirectoryPath = new DirectoryPath(toolPath);
            }

            (IToolPackageStore toolPackageStore, IToolPackageStoreQuery toolPackageStoreQuery, IToolPackageUninstaller toolPackageUninstaller)
                = _createToolPackageStoresAndUninstaller(toolDirectoryPath);
            IShellShimRepository shellShimRepository = _createShellShimRepository(toolDirectoryPath);

            var packageId = new PackageId(_parseResult.ValueForArgument<string>(ToolInstallCommandParser.PackageIdArgument));
            IToolPackage package = null;
            try
            {
                package = toolPackageStoreQuery.EnumeratePackageVersions(packageId).SingleOrDefault();
                if (package == null)
                {
                    throw new GracefulException(
                        messages: new[]
                        {
                            string.Format(
                                LocalizableStrings.ToolNotInstalled,
                                packageId),
                        },
                    isUserError: false);
                }
            }
            catch (InvalidOperationException)
            {
                throw new GracefulException(
                        messages: new[]
                        {
                            string.Format(
                        LocalizableStrings.ToolHasMultipleVersionsInstalled,
                        packageId),
                        },
                    isUserError: false);
            }

            try
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    foreach (var command in package.Commands)
                    {
                        shellShimRepository.RemoveShim(command.Name);
                    }

                    toolPackageUninstaller.Uninstall(package.PackageDirectory);

                    scope.Complete();
                }

                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.UninstallSucceeded,
                        package.Id,
                        package.Version.ToNormalizedString()).Green());
                return 0;
            }
            catch (Exception ex) when (ToolUninstallCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
            {
                throw new GracefulException(
                    messages: ToolUninstallCommandLowLevelErrorConverter.GetUserFacingMessages(ex, packageId),
                    verboseMessages: new[] {ex.ToString()},
                    isUserError: false);
            }
        }
    }
}
