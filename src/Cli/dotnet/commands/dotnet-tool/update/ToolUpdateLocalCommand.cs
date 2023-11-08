// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateLocalCommand : CommandBase
    {
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly IToolManifestEditor _toolManifestEditor;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly IToolPackageDownloader _toolPackageDownloader;
        private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
        private readonly Lazy<ToolInstallLocalCommand> _toolInstallLocalCommand;
        private readonly IReporter _reporter;

        private readonly PackageId _packageId;
        private readonly string _explicitManifestFile;

        public ToolUpdateLocalCommand(
            ParseResult parseResult,
            IToolPackageDownloader toolPackageDownloader = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            _packageId = new PackageId(parseResult.GetValue(ToolUpdateCommandParser.PackageIdArgument));
            _explicitManifestFile = parseResult.GetValue(ToolUpdateCommandParser.ToolManifestOption);

            _reporter = (reporter ?? Reporter.Output);

            if (toolPackageDownloader == null)
            {
                (IToolPackageStore,
                    IToolPackageStoreQuery,
                    IToolPackageDownloader downloader) toolPackageStoresAndDownloader
                        = ToolPackageFactory.CreateToolPackageStoresAndDownloader(
                            additionalRestoreArguments: parseResult.OptionValuesToBeForwarded(ToolUpdateCommandParser.GetCommand()));
                _toolPackageDownloader = toolPackageStoresAndDownloader.downloader;
            }
            else
            {
                _toolPackageDownloader = toolPackageDownloader;
            }

            _toolManifestFinder = toolManifestFinder ??
                                  new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();

            _toolLocalPackageInstaller = new ToolInstallLocalInstaller(parseResult, toolPackageDownloader);
            _toolInstallLocalCommand = new Lazy<ToolInstallLocalCommand>(
                () => new ToolInstallLocalCommand(
                    parseResult,
                    _toolPackageDownloader,
                    _toolManifestFinder,
                    _toolManifestEditor,
                    _localToolsResolverCache,
                    _reporter));
        }

        public override int Execute()
        {
            _toolInstallLocalCommand.Value.Execute();

            return 0;
        }
    }
}

