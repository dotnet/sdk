// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using CreateShellShimRepository = Microsoft.DotNet.Cli.Commands.Tool.Install.CreateShellShimRepository;

namespace Microsoft.DotNet.Cli.Commands.Tool.Update;

internal sealed class ToolUpdateCommand : CommandBase<ToolUpdateCommandDefinition>
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
        ILocalToolsResolverCache localToolsResolverCache = null)
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

        _global = result.GetValue(Definition.LocationOptions.GlobalOption);
        _toolPath = result.GetValue(Definition.LocationOptions.ToolPathOption);
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


    internal void EnsureEitherUpdateAllOrUpdateOption(
        ParseResult parseResult,
        string message)
    {
        List<string> options = [];
        if (parseResult.HasOption(Definition.UpdateAllOption))
        {
            options.Add(Definition.UpdateAllOption.Name);
        }

        if (parseResult.GetResult(Definition.PackageIdentityArgument) is not null)
        {
            options.Add(Definition.PackageIdentityArgument.Name);
        }

        if (options.Count != 1)
        {
            throw new GracefulException(message);
        }
    }

    internal void EnsureNoConflictPackageIdentityVersionOption(ParseResult parseResult)
    {
        if (!string.IsNullOrEmpty(parseResult.GetValue(Definition.PackageIdentityArgument)?.VersionRange?.OriginalString) &&
            !string.IsNullOrEmpty(parseResult.GetValue(Definition.VersionOption)))
        {
            throw new GracefulException(CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }
    }

    private void EnsureNoConflictUpdateAllVersionOption(
        ParseResult parseResult,
        string message)
    {
        List<string> options = [];
        if (parseResult.HasOption(Definition.UpdateAllOption))
        {
            options.Add(Definition.UpdateAllOption.Name);
        }

        if (parseResult.HasOption(Definition.VersionOption))
        {
            options.Add(Definition.VersionOption.Name);
        }

        if (options.Count > 1)
        {
            throw new GracefulException(
                string.Format(
                    message,
                    string.Join(" ", options)));
        }
    }

    public override int Execute()
    {
        Definition.LocationOptions.EnsureNoConflictGlobalLocalToolPathOption(
            _parseResult,
            CliCommandStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath);

        Definition.LocationOptions.EnsureToolManifestAndOnlyLocalFlagCombination(
            _parseResult,
            Definition.ToolManifestOption);

        EnsureNoConflictUpdateAllVersionOption(
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
