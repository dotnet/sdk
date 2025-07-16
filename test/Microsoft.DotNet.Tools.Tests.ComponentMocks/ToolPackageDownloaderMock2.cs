// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;
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

        public Action? DownloadCallback { get; set; }

        public string? MockFeedWithNoPackages { get; set; }

        List<MockFeedPackage>? _packages = null;

        public ToolPackageDownloaderMock2(IToolPackageStore store, string runtimeJsonPathForTests, string? currentWorkingDirectory, IFileSystem fileSystem) : base(store, runtimeJsonPathForTests, currentWorkingDirectory, fileSystem)
        {
        }

        public void AddMockPackage(MockFeedPackage package)
        {
            if (_packages == null)
            {
                _packages = new List<MockFeedPackage>();
            };
            _packages.Add(package);
        }

        MockFeedPackage? GetPackage(PackageId packageId, NuGetVersion version)
        {
            if (_packages == null)
            {
                return new MockFeedPackage()
                {
                    PackageId = packageId.ToString(),
                    Version = version.ToString(),
                    ToolCommandName = DefaultToolCommandName
                };
            }

            var matchingPackages = _packages.Where(p => p.PackageId == packageId.ToString() && new NuGetVersion(p.Version) >= version);
            if (!matchingPackages.Any())
            {
                return null;
            }
            return matchingPackages.MaxBy(p => new NuGetVersion(p.Version));
        }

        protected override void CreateAssetFile(PackageId packageId, NuGetVersion version, DirectoryPath packagesRootPath, string assetFilePath, string runtimeJsonGraph, Cli.Utils.VerbosityOptions verbosity, string? targetFramework = null)
        {
            var mockPackage = GetPackage(packageId, version);
            if (mockPackage == null)
            {
                throw new InvalidOperationException("Mock feed package not found");
            }

            var entryPointSimple = Path.GetFileNameWithoutExtension(FakeEntrypointName);

            var extraFiles = string.Join(Environment.NewLine, mockPackage.AdditionalFiles.Select(f => $"\"{f.Key}\":{{}},"));

            var assetFileContents = $$"""
                {
                  "version": 3,
                  "targets": {
                    "{{DefaultTargetFramework}}/{{RuntimeInformation.RuntimeIdentifier}}": {
                      "{{packageId}}/{{version}}": {
                        "type": "package",
                        "tools": {
                          {{extraFiles}}
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
        protected override INuGetPackageDownloader CreateNuGetPackageDownloader(bool verifySignatures, Cli.Utils.VerbosityOptions verbosity, RestoreActionConfig? restoreActionConfig)
        {
            List<NuGetVersion> packageVersions;
            if (_packages == null)
            {
                packageVersions = [new NuGetVersion(DefaultPackageVersion)];
            }
            else
            {
                packageVersions = _packages.Select(p => new NuGetVersion(p.Version)).ToList();
            }

            return new MockNuGetPackageDownloader(packageVersions: packageVersions)
            {
                MockFeedWithNoPackages = MockFeedWithNoPackages
            };
        }

        protected override NuGetVersion DownloadAndExtractPackage(PackageId packageId, INuGetPackageDownloader nugetPackageDownloader, string packagesRootPath,
            NuGetVersion packageVersion, PackageSourceLocation packageSourceLocation, Cli.Utils.VerbosityOptions verbosity, bool includeUnlisted = false)
        {

            var package = GetPackage(packageId, packageVersion);
            if (package == null)
            {
                throw new NuGetPackageNotFoundException(string.Format(CliStrings.IsNotFoundInNuGetFeeds, $"Version {packageVersion} of {packageId}", MockNuGetPackageDownloader.MOCK_FEEDS_TEXT));
            }

            NuGetVersion resolvedVersion = new NuGetVersion(packageVersion);

            var versionFolderPathResolver = new VersionFolderPathResolver(packagesRootPath);
            var nupkgDir = versionFolderPathResolver.GetInstallPath(packageId.ToString(), resolvedVersion);

            var fakeExecutableSubDirectory = Path.Combine(
                       nupkgDir,
                       "tools",
                       DefaultTargetFramework,
                       "any");

            var fakeExecutablePath = Path.Combine(fakeExecutableSubDirectory, FakeEntrypointName);

            TransactionalAction.Run(() =>
            {
                _fileSystem.Directory.CreateDirectory(fakeExecutableSubDirectory);
                _fileSystem.File.CreateEmptyFile(fakeExecutablePath);
                _fileSystem.File.WriteAllText(Path.Combine(fakeExecutableSubDirectory, "DotnetToolSettings.xml"),
                    $"""
                <DotNetCliTool Version="{package.ToolFormatVersion}">
                  <Commands>
                    <Command Name="{package.ToolCommandName}" EntryPoint="{FakeEntrypointName}" Runner="dotnet" />
                  </Commands>
                </DotNetCliTool>
                """);

                foreach (var additionalFile in package.AdditionalFiles)
                {
                    var resolvedPath = Path.Combine(nupkgDir, additionalFile.Key);
                    if (!_fileSystem.Directory.Exists(Path.GetDirectoryName(resolvedPath)!))
                    {
                        _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)!);
                    }
                    _fileSystem.File.WriteAllText(resolvedPath, additionalFile.Value);
                }

                if (DownloadCallback != null)
                {
                    DownloadCallback();
                }

            }, rollback: () =>
            {
                if (_fileSystem.Directory.Exists(nupkgDir))
                {
                    _fileSystem.Directory.Delete(nupkgDir, true);
                }
            });

            return resolvedVersion;
        }
        protected override ToolConfiguration GetToolConfiguration(PackageId id, DirectoryPath packageDirectory, DirectoryPath assetsJsonParentDirectory)
        {
            return new ToolConfiguration(DefaultToolCommandName, FakeEntrypointName, "dotnet");

        }
        protected override bool IsPackageInstalled(PackageId packageId, NuGetVersion packageVersion, string packagesRootPath)
        {
            var versionFolderPathResolver = new VersionFolderPathResolver(packagesRootPath);
            var nupkgDir = versionFolderPathResolver.GetInstallPath(packageId.ToString(), packageVersion);

            return _fileSystem.Directory.Exists(nupkgDir);
        }
    }
}
