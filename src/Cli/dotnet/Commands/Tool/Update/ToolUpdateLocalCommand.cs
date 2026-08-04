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

internal sealed class ToolUpdateLocalCommand : CommandBase<ToolUpdateCommandDefinition>
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

        _toolPackageDownloader = toolPackageDownloader ?? ToolPackageFactory.CreateToolPackageStoresAndDownloader().downloader;
        _toolManifestFinder = toolManifestFinder ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
        _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
        _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();

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

