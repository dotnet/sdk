// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

internal class ToolUpdateLocalCommand : CommandBase
{
    private readonly IToolManifestFinder _toolManifestFinder;
    private readonly IToolManifestEditor _toolManifestEditor;
    private readonly ILocalToolsResolverCache _localToolsResolverCache;
    private readonly IToolPackageDownloader _toolPackageDownloader;
    internal readonly Lazy<ToolInstallLocalCommand> _toolInstallLocalCommand;
    private readonly IReporter _reporter;

    public ToolUpdateLocalCommand(
        ParseResult parseResult,
        IToolPackageDownloader toolPackageDownloader = null,
        IToolManifestFinder toolManifestFinder = null,
        IToolManifestEditor toolManifestEditor = null,
        ILocalToolsResolverCache localToolsResolverCache = null,
        IReporter reporter = null)
        : base(parseResult)
    {
        _reporter = reporter ?? Reporter.Output;

        if (toolPackageDownloader == null)
        {
            (IToolPackageStore,
                IToolPackageStoreQuery,
                IToolPackageDownloader downloader) toolPackageStoresAndDownloader
                    = ToolPackageFactory.CreateToolPackageStoresAndDownloader();
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

        PackageId? packageId = null;
        if (parseResult.GetValue(ToolUpdateCommandParser.PackageIdentityArgument)?.Id is string s)
        {
            packageId = new PackageId(s);
        }

        _toolInstallLocalCommand = new Lazy<ToolInstallLocalCommand>(
            () => new ToolInstallLocalCommand(
                parseResult,
                packageId,
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

