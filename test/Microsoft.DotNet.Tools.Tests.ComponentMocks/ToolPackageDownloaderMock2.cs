// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Commands.Restore;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    internal class ToolPackageDownloaderMock2 : ToolPackageDownloaderBase
    {
        public const string DefaultToolCommandName = "SimulatorCommand";
        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string DefaultPackageVersion = "1.0.4";
        public const string DefaultTargetFramework = "net6.0";


        public ToolPackageDownloaderMock2(IToolPackageStore store, string runtimeJsonPathForTests, string currentWorkingDirectory, IFileSystem fileSystem) : base(store, runtimeJsonPathForTests, currentWorkingDirectory, fileSystem)
        {
        }

        protected override void CreateAssetFile(PackageId packageId, NuGetVersion version, DirectoryPath packagesRootPath, string assetFilePath, string runtimeJsonGraph, string? targetFramework = null)
        {
            var entryPointSimple = Path.GetFileNameWithoutExtension(FakeEntrypointName);

            var assetFileContents = $$"""
                {
                  "version": 3,
                  "targets": {
                    "{{DefaultTargetFramework}}/{{RuntimeInformation.RuntimeIdentifier}}": {
                      "{{packageId}}/{{version}}": {
                        "type": "package",
                        "tools": {
                          "tools/{{DefaultTargetFramework}}/any/DotnetToolSettings.xml": {},
                          "tools/{{DefaultTargetFramework}}/any/{{entryPointSimple}}.deps.json": {},
                          "tools/{{DefaultTargetFramework}}/any/{{entryPointSimple}}.dll": {},
                          "tools/{{DefaultTargetFramework}}/any/{{entryPointSimple}}.pdb": {},
                          "tools/{{DefaultTargetFramework}}/any/{{entryPointSimple}}.runtimeconfig.json": {}
                        }
                      }
                    }
                  },
                  "libraries": {},
                  "projectFileDependencyGroups": {}
                }
                """;

            _fileSystem.File.WriteAllText(assetFilePath, assetFileContents);
        }
        protected override INuGetPackageDownloader CreateNuGetPackageDownloader(bool verifySignatures, VerbosityOptions verbosity, RestoreActionConfig restoreActionConfig)
        {
            return new MockNuGetPackageDownloader(packageVersions: [new NuGetVersion(DefaultPackageVersion)]);
        }

        protected override NuGetVersion DownloadAndExtractPackage(PackageId packageId, INuGetPackageDownloader nugetPackageDownloader, string packagesRootPath,
            NuGetVersion packageVersion, PackageSourceLocation packageSourceLocation, bool includeUnlisted = false)
        {
            NuGetVersion resolvedVersion = new NuGetVersion(DefaultPackageVersion);

            var versionFolderPathResolver = new VersionFolderPathResolver(packagesRootPath);
            var nupkgDir = versionFolderPathResolver.GetInstallPath(packageId.ToString(), resolvedVersion);

            var fakeExecutableSubDirectory = Path.Combine(
                       nupkgDir,
                       "tools",
                       DefaultTargetFramework,
                       "any");

            var fakeExecutablePath = Path.Combine(fakeExecutableSubDirectory, FakeEntrypointName);

            _fileSystem.Directory.CreateDirectory(fakeExecutableSubDirectory);
            _fileSystem.File.CreateEmptyFile(fakeExecutablePath);
            _fileSystem.File.WriteAllText(Path.Combine(fakeExecutableSubDirectory, "DotnetToolSettings.xml"),
                $"""
                <DotNetCliTool Version="1">
                  <Commands>
                    <Command Name="{DefaultToolCommandName}" EntryPoint="{FakeEntrypointName}" Runner="dotnet" />
                  </Commands>
                </DotNetCliTool>
                """);

            return resolvedVersion;
        }
        protected override ToolConfiguration GetToolConfiguration(PackageId id, DirectoryPath packageDirectory, DirectoryPath assetsJsonParentDirectory)
        {
            return new ToolConfiguration(DefaultToolCommandName, FakeEntrypointName, "dotnet");
            
        }
        protected override bool IsPackageInstalled(PackageId packageId, NuGetVersion packageVersion, string packagesRootPath)
        {
            return false;
        }
    }
}
