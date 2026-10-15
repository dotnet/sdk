// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using CreateShellShimRepository = Microsoft.DotNet.Tools.Tool.Install.CreateShellShimRepository;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateCommand : CommandBase
    {
        private readonly ToolUpdateLocalCommand _toolUpdateLocalCommand;
        private readonly ToolUpdateGlobalOrToolPathCommand _toolUpdateGlobalOrToolPathCommand;
        private readonly bool _global;
        private readonly string _toolPath;

        public ToolUpdateCommand(
            ParseResult result,
            IReporter reporter = null,
            ToolUpdateGlobalOrToolPathCommand toolUpdateGlobalOrToolPathCommand = null,
            ToolUpdateLocalCommand toolUpdateLocalCommand = null,
            CreateToolPackageStoresAndDownloaderAndUninstaller createToolPackageStoreDownloaderUninstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IToolPackageDownloader toolPackageDownloader = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null
            )
            : base(result)
        {
            _toolUpdateLocalCommand
                = toolUpdateLocalCommand ??
                  new ToolUpdateLocalCommand(
                        result,
                        toolPackageDownloader,
                        toolManifestFinder,
                        toolManifestEditor,
                        localToolsResolverCache,
                        reporter);

            _global = result.GetValue(ToolInstallCommandParser.GlobalOption);
            _toolPath = result.GetValue(ToolInstallCommandParser.ToolPathOption);
            DirectoryPath? location = string.IsNullOrWhiteSpace(_toolPath) ? null : new DirectoryPath(_toolPath);
            _toolUpdateGlobalOrToolPathCommand =
                toolUpdateGlobalOrToolPathCommand
                ?? new ToolUpdateGlobalOrToolPathCommand(
                    result,
                    createToolPackageStoreDownloaderUninstaller,
                    createShellShimRepository,
                    reporter,
                    ToolPackageFactory.CreateToolPackageStoreQuery(location));
        }


        internal static void EnsureEitherUpdateAllOrUpdateOption(
            ParseResult parseResult,
            string message)
        {
            List<string> options = new List<string>();
            if (parseResult.GetResult(ToolAppliedOption.UpdateAllOption) is not null)
            {
                options.Add(ToolAppliedOption.UpdateAllOption.Name);
            }

            if (parseResult.GetResult(ToolUpdateCommandParser.PackageIdArgument) is not null)
            {
                options.Add(ToolUpdateCommandParser.PackageIdArgument.Name);
            }

            if (options.Count != 1)
            {
                throw new GracefulException(message);
            }
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _parseResult,
                LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath);

            ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(_parseResult);

            ToolAppliedOption.EnsureNoConflictUpdateAllVersionOption(
                _parseResult,
                LocalizableStrings.UpdateToolCommandInvalidAllAndVersion);

            EnsureEitherUpdateAllOrUpdateOption(
                _parseResult,
                LocalizableStrings.UpdateToolCommandInvalidAllAndPackageId);

            if (_global || !string.IsNullOrWhiteSpace(_toolPath))
            {
                return _toolUpdateGlobalOrToolPathCommand.Execute();
            }
            else
            {
                return _toolUpdateLocalCommand.Execute();
            }
        }
    }
}
