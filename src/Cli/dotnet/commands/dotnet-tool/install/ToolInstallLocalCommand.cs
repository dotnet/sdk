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

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallLocalCommand : CommandBase
    {
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly IToolManifestEditor _toolManifestEditor;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
        private readonly IReporter _reporter;
        private readonly PackageId _packageId;
        private readonly bool _allowPackageDowngrade;

        private readonly string _explicitManifestFile;
        private readonly bool _createManifestIfNeeded;

        public ToolInstallLocalCommand(
            ParseResult parseResult,
            IToolPackageDownloader toolPackageDownloader = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IReporter reporter = null
            )
            : base(parseResult)
        {
            _explicitManifestFile = parseResult.GetValue(ToolAppliedOption.ToolManifestOption);

            _createManifestIfNeeded = parseResult.GetValue(ToolInstallCommandParser.CreateManifestIfNeededOption);

            _reporter = (reporter ?? Reporter.Output);

            _toolManifestFinder = toolManifestFinder ??
                                  new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
            _toolLocalPackageInstaller = new ToolInstallLocalInstaller(parseResult, toolPackageDownloader);
            _packageId = new PackageId(parseResult.GetValue(ToolUpdateCommandParser.PackageIdArgument));
            _allowPackageDowngrade = parseResult.GetValue(ToolInstallCommandParser.AllowPackageDowngradeOption);
        }
        

        public override int Execute()
        {
            (FilePath? manifestFileOptional, string warningMessage) =
                _toolManifestFinder.ExplicitManifestOrFindManifestContainPackageId(_explicitManifestFile, _packageId);

            if (warningMessage != null)
            {
                _reporter.WriteLine(warningMessage.Yellow());
            }

            FilePath manifestFile = manifestFileOptional ?? GetManifestFilePath();
            var existingPackageWithPackageId = _toolManifestFinder.Find(manifestFile).Where(p => p.PackageId.Equals(_packageId));

            if (!existingPackageWithPackageId.Any())
            {
                return InstallNewTool(manifestFile);
            }

            var existingPackage = existingPackageWithPackageId.Single();
            var toolDownloadedPackage = _toolLocalPackageInstaller.Install(manifestFile);

            InstallToolUpdate(existingPackage, toolDownloadedPackage, manifestFile);

            _localToolsResolverCache.SaveToolPackage(
                toolDownloadedPackage,
                _toolLocalPackageInstaller.TargetFrameworkToInstall);

            return 0;
        }

        public int InstallToolUpdate(ToolManifestPackage existingPackage, IToolPackage toolDownloadedPackage, FilePath manifestFile)
        {
            if (existingPackage.Version > toolDownloadedPackage.Version && !_allowPackageDowngrade)
            {
                throw new GracefulException(new[]
                    {
                        string.Format(
                            Update.LocalizableStrings.UpdateLocalToolToLowerVersion,
                            toolDownloadedPackage.Version.ToNormalizedString(),
                            existingPackage.Version.ToNormalizedString(),
                            manifestFile.Value)
                    },
                    isUserError: false);
            }
            else if (existingPackage.Version == toolDownloadedPackage.Version)
            {
                _reporter.WriteLine(
                    string.Format(
                        Update.LocalizableStrings.UpdateLocaToolSucceededVersionNoChange,
                        toolDownloadedPackage.Id,
                        existingPackage.Version.ToNormalizedString(),
                        manifestFile.Value));
            }
            else
            {
                _toolManifestEditor.Edit(
                    manifestFile,
                    _packageId,
                    toolDownloadedPackage.Version,
                    toolDownloadedPackage.Commands.Select(c => c.Name).ToArray());
                _reporter.WriteLine(
                    string.Format(
                        Update.LocalizableStrings.UpdateLocalToolSucceeded,
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
        public int InstallNewTool(FilePath manifestFile)
        {
            IToolPackage toolDownloadedPackage =
                _toolLocalPackageInstaller.Install(manifestFile);

            _toolManifestEditor.Add(
                manifestFile,
                toolDownloadedPackage.Id,
                toolDownloadedPackage.Version,
                toolDownloadedPackage.Commands.Select(c => c.Name).ToArray());

            _localToolsResolverCache.SaveToolPackage(
                toolDownloadedPackage,
                _toolLocalPackageInstaller.TargetFrameworkToInstall);

            _reporter.WriteLine(
                string.Format(
                    LocalizableStrings.LocalToolInstallationSucceeded,
                    string.Join(", ", toolDownloadedPackage.Commands.Select(c => c.Name)),
                    toolDownloadedPackage.Id,
                    toolDownloadedPackage.Version.ToNormalizedString(),
                    manifestFile.Value).Green());

            return 0;
        }

        private FilePath GetManifestFilePath()
        {
            try
            {
                return string.IsNullOrWhiteSpace(_explicitManifestFile)
                    ? _toolManifestFinder.FindFirst(_createManifestIfNeeded)
                    : new FilePath(_explicitManifestFile);
            }
            catch (ToolManifestCannotBeFoundException e)
            {
                throw new GracefulException(new[]
                    {
                        e.Message,
                        LocalizableStrings.NoManifestGuide
                    },
                    verboseMessages: new[] {e.VerboseMessage},
                    isUserError: false);
            }
        }
    }
}
