// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Tool.Common;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using CreateShellShimRepository = Microsoft.DotNet.Cli.Commands.Tool.Install.CreateShellShimRepository;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

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
        List<string> options = [];
        if (parseResult.HasOption(ToolAppliedOption.UpdateAllOption))
        {
            options.Add(ToolAppliedOption.UpdateAllOption.Name);
        }

        if (parseResult.GetResult(ToolUpdateCommandParser.PackageIdentityArgument) is not null)
        {
            options.Add(ToolUpdateCommandParser.PackageIdentityArgument.Name);
        }

        if (options.Count != 1)
        {
            throw new GracefulException(message);
        }
    }

    internal static void EnsureNoConflictPackageIdentityVersionOption(ParseResult parseResult)
    {
        if (!string.IsNullOrEmpty(parseResult.GetValue(ToolUpdateCommandParser.PackageIdentityArgument)?.VersionRange?.OriginalString) &&
            !string.IsNullOrEmpty(parseResult.GetValue(ToolAppliedOption.VersionOption)))
        {
            throw new GracefulException(CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }
    }

    public override int Execute()
    {
        ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            CliCommandStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath);

        ToolAppliedOption.EnsureToolManifestAndOnlyLocalFlagCombination(_parseResult);

        ToolAppliedOption.EnsureNoConflictUpdateAllVersionOption(
            _parseResult,
            CliCommandStrings.UpdateToolCommandInvalidAllAndVersion);

        EnsureEitherUpdateAllOrUpdateOption(
            _parseResult,
            CliCommandStrings.UpdateToolCommandInvalidAllAndPackageId);

        EnsureNoConflictPackageIdentityVersionOption(_parseResult);

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
