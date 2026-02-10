// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolManifest;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Install;

internal sealed class ToolInstallLocalCommand : CommandBase<ToolUpdateInstallCommandDefinition>
{
    private readonly IToolManifestFinder _toolManifestFinder;
    private readonly IToolManifestEditor _toolManifestEditor;
    private readonly ILocalToolsResolverCache _localToolsResolverCache;
    private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
    private readonly IReporter _reporter;
    private readonly PackageIdentityWithRange? _packageIdentityWithRange;
    private readonly bool _allowPackageDowngrade;

    private readonly string? _explicitManifestFile;
    private readonly bool _createManifestIfNeeded;
    private readonly bool _allowRollForward;
    private readonly bool _updateAll;

    internal RestoreActionConfig restoreActionConfig;

    public ToolInstallLocalCommand(
        ParseResult parseResult,
        IToolPackageDownloader? toolPackageDownloader = null,
        IToolManifestFinder? toolManifestFinder = null,
        IToolManifestEditor? toolManifestEditor = null,
        ILocalToolsResolverCache? localToolsResolverCache = null,
        IReporter? reporter = null,
        string? runtimeJsonPathForTests = null)
        : base(parseResult)
    {
        if (Definition is ToolUpdateCommandDefinition updateDef)
        {
            _updateAll = parseResult.GetValue(updateDef.UpdateAllOption);
            _packageIdentityWithRange = parseResult.GetValue(updateDef.PackageIdentityArgument);
        }
        else
        {
            var installDef = (ToolInstallCommandDefinition)Definition;
            _packageIdentityWithRange = parseResult.GetValue(installDef.PackageIdentityArgument);
            _createManifestIfNeeded = parseResult.GetValue(installDef.CreateManifestIfNeededOption);
            _allowRollForward = parseResult.GetValue(installDef.RollForwardOption);
        }

        _explicitManifestFile = parseResult.GetValue(Definition.ToolManifestOption);

        _reporter = reporter ?? Reporter.Output;

        _toolManifestFinder = toolManifestFinder ?? new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
        _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
        _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();

        restoreActionConfig = Definition.RestoreOptions.ToRestoreActionConfig(parseResult);

        _toolLocalPackageInstaller = new ToolInstallLocalInstaller(
            configFilePath: parseResult.GetValue(Definition.ConfigOption),
            sources: parseResult.GetValue(Definition.AddSourceOption),
            verbosity: parseResult.GetValue(Definition.VerbosityOption),
            toolPackageDownloader,
            runtimeJsonPathForTests,
            restoreActionConfig);

        _allowPackageDowngrade = parseResult.GetValue(Definition.AllowPackageDowngradeOption);
    }

    public override int Execute()
    {
        if (_updateAll)
        {
            foreach (var (manifestPackage, _) in ((IToolManifestInspector)_toolManifestFinder).Inspect())
            {
                ExecuteInstallCommand(manifestPackage.PackageId, versionRange: null);
            }

            return 0;
        }

        // package id must be specified (UpdateToolCommandInvalidAllAndPackageId is reported otherwise):
        Debug.Assert(_packageIdentityWithRange != null);
        var packageId = new PackageId(_packageIdentityWithRange.Value.Id);

        var versionRange = VersionRangeUtilities.GetVersionRange(
            _packageIdentityWithRange.Value.VersionRange?.OriginalString,
            _parseResult.GetValue(Definition.VersionOption),
            _parseResult.GetValue(Definition.PrereleaseOption));

        return ExecuteInstallCommand(packageId, versionRange);
    }

    private int ExecuteInstallCommand(PackageId packageId, VersionRange? versionRange)
    {
        FilePath manifestFile = GetManifestFilePath();

        (FilePath? manifestFileOptional, string warningMessage) =
            _toolManifestFinder.ExplicitManifestOrFindManifestContainPackageId(_explicitManifestFile, packageId);

        if (warningMessage != null)
        {
            _reporter.WriteLine(warningMessage.Yellow());
        }

        manifestFile = manifestFileOptional ?? GetManifestFilePath();
        var existingPackageWithPackageId = _toolManifestFinder.Find(manifestFile).Where(p => p.PackageId.Equals(packageId));

        if (!existingPackageWithPackageId.Any())
        {
            return InstallNewTool(manifestFile, packageId, versionRange);
        }

        var existingPackage = existingPackageWithPackageId.Single();
        var toolDownloadedPackage = _toolLocalPackageInstaller.Install(manifestFile, packageId, versionRange);

        InstallToolUpdate(existingPackage, toolDownloadedPackage, manifestFile, packageId);

        _localToolsResolverCache.SaveToolPackage(
            toolDownloadedPackage,
            _toolLocalPackageInstaller.TargetFrameworkToInstall);

        return 0;
    }

    public int InstallToolUpdate(ToolManifestPackage existingPackage, IToolPackage toolDownloadedPackage, FilePath manifestFile, PackageId packageId)
    {
        if (existingPackage.Version > toolDownloadedPackage.Version && !_allowPackageDowngrade)
        {
            throw new GracefulException(
                [
                    string.Format(
                        CliCommandStrings.UpdateLocalToolToLowerVersion,
                        toolDownloadedPackage.Version.ToNormalizedString(),
                        existingPackage.Version.ToNormalizedString(),
                        manifestFile.Value)
                ],
                isUserError: false);
        }
        else if (existingPackage.Version == toolDownloadedPackage.Version)
        {
            _reporter.WriteLine(
                string.Format(
                    CliCommandStrings.UpdateLocaToolSucceededVersionNoChange,
                    toolDownloadedPackage.Id,
                    existingPackage.Version.ToNormalizedString(),
                    manifestFile.Value));
        }
        else
        {
            _toolManifestEditor.Edit(
                manifestFile,
                packageId,
                toolDownloadedPackage.Version,
                [toolDownloadedPackage.Command.Name]);
            _reporter.WriteLine(
                string.Format(
                    CliCommandStrings.UpdateLocalToolSucceeded,
                    toolDownloadedPackage.Id,
                    existingPackage.Version.ToNormalizedString(),
                    toolDownloadedPackage.Version.ToNormalizedString(),
                    manifestFile.Value).Green());
        }

        _localToolsResolverCache.SaveToolPackage(
            toolDownloadedPackage,
            _toolLocalPackageInstaller.TargetFrameworkToInstall);

        return 0;
    }

    public int InstallNewTool(FilePath manifestFile, PackageId packageId, VersionRange? versionRange)
    {
        IToolPackage toolDownloadedPackage =
            _toolLocalPackageInstaller.Install(manifestFile, packageId, versionRange);

        _toolManifestEditor.Add(
            manifestFile,
            toolDownloadedPackage.Id,
            toolDownloadedPackage.Version,
            [toolDownloadedPackage.Command.Name],
            _allowRollForward);

        _localToolsResolverCache.SaveToolPackage(
            toolDownloadedPackage,
            _toolLocalPackageInstaller.TargetFrameworkToInstall);

        _reporter.WriteLine(
            string.Format(
                CliCommandStrings.LocalToolInstallationSucceeded,
                toolDownloadedPackage.Command.Name,
                toolDownloadedPackage.Id,
                toolDownloadedPackage.Version.ToNormalizedString(),
                manifestFile.Value).Green());

        return 0;
    }

    public FilePath GetManifestFilePath()
    {
        return string.IsNullOrWhiteSpace(_explicitManifestFile)
            ? _toolManifestFinder.FindFirst(_createManifestIfNeeded)
            : new FilePath(_explicitManifestFile);
    }
}
