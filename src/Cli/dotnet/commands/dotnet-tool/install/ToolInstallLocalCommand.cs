// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.Extensions.EnvironmentAbstractions;
using System.Collections.Generic;
using NuGet.Packaging;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallLocalCommand : CommandBase
    {
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly IToolManifestEditor _toolManifestEditor;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly ToolInstallLocalInstaller _toolLocalPackageInstaller;
        private readonly IReporter _reporter;

        private readonly string _explicitManifestFile;
        private readonly PackageId _packageId;
        private readonly bool _allowPackageDowngrade;
        private readonly bool _createManifestIfNeeded;

        public ToolInstallLocalCommand(
            ParseResult parseResult,
            IToolPackageInstaller toolPackageInstaller = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IReporter reporter = null
            )
            : base(parseResult)
        {
            _explicitManifestFile = parseResult.GetValue(ToolAppliedOption.ToolManifestOption);
            _packageId = new PackageId(parseResult.GetValue(ToolUpdateCommandParser.PackageIdArgument));

            _createManifestIfNeeded = parseResult.GetValue(ToolInstallCommandParser.CreateManifestIfNeededOption);

            _reporter = (reporter ?? Reporter.Output);

            _toolManifestFinder = toolManifestFinder ??
                                  new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
            _toolManifestEditor = toolManifestEditor ?? new ToolManifestEditor();
            _localToolsResolverCache = localToolsResolverCache ?? new LocalToolsResolverCache();
            _toolLocalPackageInstaller = new ToolInstallLocalInstaller(parseResult, toolPackageInstaller);
            _allowPackageDowngrade = parseResult.GetValue(ToolInstallCommandParser.AllowPackageDowngradeOption);
        }

        public override int Execute()
        {
            FilePath manifestFile = GetManifestFilePath();
            var existingPackageWithPackageId = _toolManifestFinder.Find(manifestFile).Where(p => p.PackageId.Equals(_packageId));

            if (!existingPackageWithPackageId.Any())
            {
                return Install(manifestFile);
            }

            var existingPackage = existingPackageWithPackageId.Single();
            var toolDownloadedPackage = _toolLocalPackageInstaller.Install(manifestFile);

            InstallLogic(existingPackage, toolDownloadedPackage, manifestFile);
            return 0;
        }

        public int InstallLogic(ToolManifestPackage existingPackage, IToolPackage toolDownloadedPackage, FilePath manifestFile)
        {
            if (existingPackage.Version > toolDownloadedPackage.Version && !_allowPackageDowngrade)
            {
                throw new GracefulException(new[]
                    {
                        string.Format(
                            LocalizableStrings.UpdateToLowerVersion,
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
                        LocalizableStrings.UpdateLocaToolSucceededVersionNoChange,
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
                        LocalizableStrings.UpdateLocalToolSucceeded,
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

        public int Install(FilePath manifestFile)
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
