// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.IO;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tool.Install
{
    internal class ToolInstallLocalInstaller
    {
        public string TargetFrameworkToInstall { get; private set; }

        private readonly IToolPackageInstaller _toolPackageInstaller;
        private readonly PackageId _packageId;
        private readonly string _packageVersion;
        private readonly string _configFilePath;
        private readonly string[] _sources;
        private readonly string _verbosity;

        public ToolInstallLocalInstaller(
            ParseResult parseResult,
            IToolPackageInstaller toolPackageInstaller = null)
        {
            _packageId = new PackageId(parseResult.ValueForArgument<string>(ToolInstallCommandParser.PackageIdArgument));
            _packageVersion = parseResult.ValueForOption<string>(ToolInstallCommandParser.VersionOption);
            _configFilePath = parseResult.ValueForOption<string>(ToolInstallCommandParser.ConfigOption);
            _sources = parseResult.ValueForOption<string[]>(ToolInstallCommandParser.AddSourceOption);
            _verbosity = Enum.GetName(parseResult.ValueForOption<VerbosityOptions>(ToolInstallCommandParser.VerbosityOption));

            if (toolPackageInstaller == null)
            {
                (IToolPackageStore,
                    IToolPackageStoreQuery,
                    IToolPackageInstaller installer) toolPackageStoresAndInstaller
                        = ToolPackageFactory.CreateToolPackageStoresAndInstaller(
                            additionalRestoreArguments: parseResult.OptionValuesToBeForwarded(ToolInstallCommandParser.GetCommand()));
                _toolPackageInstaller = toolPackageStoresAndInstaller.installer;
            }
            else
            {
                _toolPackageInstaller = toolPackageInstaller;
            }

            TargetFrameworkToInstall = BundledTargetFramework.GetTargetFrameworkMoniker();
        }

        public IToolPackage Install(FilePath manifestFile)
        {
            if (!string.IsNullOrEmpty(_configFilePath) && !File.Exists(_configFilePath))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.NuGetConfigurationFileDoesNotExist,
                        Path.GetFullPath(_configFilePath)));
            }

            VersionRange versionRange = null;
            if (!string.IsNullOrEmpty(_packageVersion) && !VersionRange.TryParse(_packageVersion, out versionRange))
            {
                throw new GracefulException(
                    string.Format(
                        LocalizableStrings.InvalidNuGetVersionRange,
                        _packageVersion));
            }

            FilePath? configFile = null;
            if (!string.IsNullOrEmpty(_configFilePath))
            {
                configFile = new FilePath(_configFilePath);
            }

            try
            {
                IToolPackage toolDownloadedPackage =
                    _toolPackageInstaller.InstallPackageToExternalManagedLocation(
                        new PackageLocation(
                            nugetConfig: configFile,
                            additionalFeeds: _sources,
                            rootConfigDirectory: manifestFile.GetDirectoryPath()),
                        _packageId,
                        versionRange,
                        TargetFrameworkToInstall,
                        verbosity: _verbosity);

                return toolDownloadedPackage;
            }
            catch (Exception ex) when (InstallToolCommandLowLevelErrorConverter.ShouldConvertToUserFacingError(ex))
            {
                throw new GracefulException(
                    messages: InstallToolCommandLowLevelErrorConverter.GetUserFacingMessages(ex, _packageId),
                    verboseMessages: new[] {ex.ToString()},
                    isUserError: false);
            }
        }
    }
}
