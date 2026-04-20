// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using FluentAssertions;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Restore;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Restore.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;
using Microsoft.DotNet.Cli.ToolPackage;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolRestoreCommandTests: SdkTest
    {
        private readonly IFileSystem _fileSystem;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly ToolPackageDownloaderMock _toolPackageDownloaderMock;
        private readonly ToolPackageDownloader _toolPackageDownloader;
        private readonly ParseResult _parseResult;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _pathToPlacePackages;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly PackageId _packageIdA = new("local.tool.console.a");

        private readonly PackageId _packageIdWithCommandNameCollisionWithA =
            new("command.name.collision.with.package.a");

        private readonly NuGetVersion _packageVersionWithCommandNameCollisionWithA;
        private readonly NuGetVersion _packageVersionA;
        private readonly ToolCommandName _toolCommandNameA = new("a");

        private readonly PackageId _packageIdB = new("local.tool.console.B");
        private readonly NuGetVersion _packageVersionB;
        private readonly ToolCommandName _toolCommandNameB = new("b");
        private readonly DirectoryPath _nugetGlobalPackagesFolder;

        private int _installCalledCount = 0;

        public ToolRestoreCommandTests(ITestOutputHelper log): base(log)
        {
            _packageVersionA = NuGetVersion.Parse("1.0.4");
            _packageVersionWithCommandNameCollisionWithA = NuGetVersion.Parse("1.0.9");
            _packageVersionB = NuGetVersion.Parse("1.0.4");

            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _nugetGlobalPackagesFolder = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
            _temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _pathToPlacePackages = Path.Combine(_temporaryDirectory, "pathToPlacePackage");
            ToolPackageStoreMock toolPackageStoreMock =
                new(new DirectoryPath(_pathToPlacePackages), _fileSystem);
            _toolPackageStore = toolPackageStoreMock;

            _toolPackageDownloader = new ToolPackageDownloader(toolPackageStoreMock);

            _toolPackageDownloaderMock = new ToolPackageDownloaderMock(
                _toolPackageStore,
                _fileSystem,    
                _reporter,
                new List<MockFeed>
                {
                    new MockFeed
                    {
                        Type = MockFeedType.ImplicitAdditionalFeed,
                        Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = _packageIdA.ToString(),
                                Version = _packageVersionA.ToNormalizedString(),
                                ToolCommandName = _toolCommandNameA.ToString()
                            },
                            new MockFeedPackage
                            {
                                PackageId = _packageIdB.ToString(),
                                Version = _packageVersionB.ToNormalizedString(),
                                ToolCommandName = _toolCommandNameB.ToString()
                            },
                            new MockFeedPackage
                            {
                                PackageId = _packageIdWithCommandNameCollisionWithA.ToString(),
                                Version = _packageVersionWithCommandNameCollisionWithA.ToNormalizedString(),
                                ToolCommandName = "A"
                            }
                        }
                    }
                },
                downloadCallback: () => _installCalledCount++);

            _parseResult = Parser.Instance.Parse("dotnet tool restore");

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(_temporaryDirectory, "cache")),
                    1);
        }

        [Fact]
        public void WhenRunItCanSaveCommandsToCache()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false),
                    new ToolManifestPackage(_packageIdB, _packageVersionB,
                        new[] {_toolCommandNameB},
                        new DirectoryPath(_temporaryDirectory),
                        false)
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _localToolsResolverCache.TryLoad(
                    new RestoredCommandIdentifier(
                        _packageIdA,
                        _packageVersionA,
                        NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                        Constants.AnyRid,
                        _toolCommandNameA), out RestoredCommand restoredCommand)
                .Should().BeTrue();

            _fileSystem.File.Exists(restoredCommand.Executable.Value)
                .Should().BeTrue($"Cached command should be found at {restoredCommand.Executable.Value}");
        }

        [Fact]
        public void WhenRunItCanSaveCommandsToCacheAndShowSuccessMessage()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false),
                    new ToolManifestPackage(_packageIdB, _packageVersionB,
                        new[] {_toolCommandNameB},
                        new DirectoryPath(_temporaryDirectory),
                        false)
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Contain(l => l.Contains(string.Format(
                LocalizableStrings.RestoreSuccessful, _packageIdA,
                _packageVersionA.ToNormalizedString(), _toolCommandNameA)));
            _reporter.Lines.Should().Contain(l => l.Contains(string.Format(
                LocalizableStrings.RestoreSuccessful, _packageIdB,
                _packageVersionB.ToNormalizedString(), _toolCommandNameB)));

            _reporter.Lines.Should().Contain(l => l.Contains("\x1B[32m"),
                "ansicolor code for green, message should be green");
        }

        [Fact]
        public void WhenRestoredCommandHasTheSameCommandNameItThrows()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false),
                    new ToolManifestPackage(_packageIdWithCommandNameCollisionWithA,
                        _packageVersionWithCommandNameCollisionWithA, new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false)
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            var allPossibleErrorMessage = new[]
            {
                string.Format(LocalizableStrings.PackagesCommandNameCollisionConclusion,
                    string.Join(Environment.NewLine,
                        new[]
                        {
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                _toolCommandNameA.Value,
                                _packageIdA.ToString()),
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                "A",
                                _packageIdWithCommandNameCollisionWithA.ToString())
                        })),

                string.Format(LocalizableStrings.PackagesCommandNameCollisionConclusion,
                    string.Join(Environment.NewLine,
                        new[]
                        {
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                "A",
                                _packageIdWithCommandNameCollisionWithA.ToString()),
                            "\t" + string.Format(LocalizableStrings.PackagesCommandNameCollisionForOnePackage,
                                _toolCommandNameA.Value,
                                _packageIdA.ToString()),
                        })),
            };

            Action a = () => toolRestoreCommand.Execute();
            a.Should().Throw<ToolPackageException>()
                .And.Message
                .Should().BeOneOf(allPossibleErrorMessage, "Run in parallel, no order guarantee");
        }

        [Fact]
        public void WhenSomePackageFailedToRestoreItCanRestorePartiallySuccessful()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false),
                    new ToolManifestPackage(new PackageId("non-exists"), NuGetVersion.Parse("1.0.0"),
                        new[] {new ToolCommandName("non-exists")},
                        new DirectoryPath(_temporaryDirectory),
                        false)
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            int executeResult = toolRestoreCommand.Execute();
            _reporter.Lines.Should()
                .Contain(l => l.Contains(string.Format(LocalizableStrings.PackageFailedToRestore,
                    "non-exists", "")));

            _reporter.Lines.Should().Contain(l => l.Contains(LocalizableStrings.RestorePartiallyFailed));

            executeResult.Should().Be(1);

            _localToolsResolverCache.TryLoad(
                    new RestoredCommandIdentifier(
                        _packageIdA,
                        _packageVersionA,
                        NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                        Constants.AnyRid,
                        _toolCommandNameA), out _)
                .Should().BeTrue("Existing package will succeed despite other package failed");
        }

        [Fact]
        public void ItShouldFailWhenPackageCommandNameDoesNotMatchManifestCommands()
        {
            ToolCommandName differentCommandNameA = new("different-command-nameA");
            ToolCommandName differentCommandNameB = new("different-command-nameB");
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {differentCommandNameA, differentCommandNameB},
                        new DirectoryPath(_temporaryDirectory),
                        false),
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(1);
            _reporter.Lines.Should()
                .Contain(l =>
                    l.Contains(
                        string.Format(LocalizableStrings.CommandsMismatch,
                            "\"different-command-nameA\" \"different-command-nameB\"", _packageIdA, "\"a\"")));
        }

        [Fact]
        public void ItRestoresMultipleTools()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            string configContents = """
                {
                  "version": 1,
                  "isRoot": true,
                  "tools": {
                    "cake.tool": {
                      "version": "2.3.0",
                      "commands": [
                        "dotnet-cake"
                      ]
                    },
                    "powershell": {
                      "version": "7.3.7",
                      "commands": [
                        "pwsh"
                      ]
                    },
                    "api-tools": {
                      "version": "1.3.5",
                      "commands": [
                        "api-tools"
                      ]
                    },
                    "dotnet-ef": {
                      "version": "8.0.0-rc.1.23419.6",
                      "commands": [
                        "dotnet-ef"
                      ]
                    }
                  }
                }
                """;

            File.WriteAllText(Path.Combine(testDir, "dotnet-tools.json"), configContents);

            string CliHome = Path.Combine(testDir, ".home");
            Directory.CreateDirectory(CliHome);

            var toolRestoreCommand = new DotnetCommand(Log, "tool", "restore")
                .WithEnvironmentVariable("DOTNET_CLI_HOME", CliHome)
                .WithEnvironmentVariable("DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK", "true")
                .WithWorkingDirectory(testDir);

            toolRestoreCommand
                .Execute()
                .Should()
                .Pass();

            //  Delete tool resolver cache and then run command again.  NuGet packages will still be downloaded to packages folder, making it more likely to hit concurrency issues
            //  in the tool code
            Directory.Delete(CliHome, true);

            toolRestoreCommand
                .Execute()
                .Should()
                .Pass();
        }

        private class CacheRow
        {
            public string Version { get; set; }
            public string TargetFramework { get; set; }
            public string RuntimeIdentifier { get; set; }
            public string Name { get; set; }
            public string Runner { get; set; }
            public string PathToExecutable { get; set; }
        }

        [Fact]
        public void ItRestoresCorrectToolVersion()
        {
            var testDir = _testAssetsManager.CreateTestDirectory().Path;

            string configContents = """
                {
                  "version": 1,
                  "isRoot": true,
                  "tools": {
                    "dotnet-ef": {
                      "version": "8.0.0-rc.1.23419.6",
                      "commands": [
                        "dotnet-ef"
                      ]
                    }
                  }
                }
                """;

            File.WriteAllText(Path.Combine(testDir, "dotnet-tools.json"), configContents);

            string CliHome = Path.Combine(testDir, ".home");
            Directory.CreateDirectory(CliHome);

            var toolRestoreCommand = new DotnetCommand(Log, "tool", "restore")
                .WithEnvironmentVariable("DOTNET_CLI_HOME", CliHome)
                .WithEnvironmentVariable("DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK", "true")
                .WithWorkingDirectory(testDir);

            toolRestoreCommand
                .Execute()
                .Should()
                .Pass();

            var cacheFilePath = Path.Combine(CliHome, ".dotnet", "toolResolverCache", "1", "dotnet-ef");

            string json = File.ReadAllText(cacheFilePath);

            var rows = JsonSerializer.Deserialize<List<CacheRow>>(json);

            rows.Count.Should().Be(1);

            rows[0].Name.Should().Be("dotnet-ef");
            rows[0].Version.Should().Be("8.0.0-rc.1.23419.6");
        }

        [Fact]
        public void WhenCannotFindManifestFileItPrintsWarning()
        {
            IToolManifestFinder realManifestFinderImplementationWithMockFinderSystem =
                new ToolManifestFinder(new DirectoryPath(Path.GetTempPath()), _fileSystem, new FakeDangerousFileDetector());

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                realManifestFinderImplementationWithMockFinderSystem,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _reporter.Lines.Should()
                .Contain(l =>
                    l.Contains(ToolManifest.LocalizableStrings.CannotFindAManifestFile));
        }

        [Fact]
        public void WhenPackageIsRestoredAlreadyItWillNotRestoreItAgain()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false)
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute();
            var installCallCountBeforeTheSecondRestore = _installCalledCount;
            toolRestoreCommand.Execute();

            installCallCountBeforeTheSecondRestore.Should().BeGreaterThan(0);
            _installCalledCount.Should().Be(installCallCountBeforeTheSecondRestore);
        }

        [Fact]
        public void WhenPackageIsRestoredAlreadyButDllIsRemovedItRestoresAgain()
        {
            IToolManifestFinder manifestFinder =
                new MockManifestFinder(new[]
                {
                    new ToolManifestPackage(_packageIdA, _packageVersionA,
                        new[] {_toolCommandNameA},
                        new DirectoryPath(_temporaryDirectory),
                        false)
                });

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute();
            _fileSystem.Directory.Delete(_nugetGlobalPackagesFolder.Value, true);
            var installCallCountBeforeTheSecondRestore = _installCalledCount;
            toolRestoreCommand.Execute();

            installCallCountBeforeTheSecondRestore.Should().BeGreaterThan(0);
            _installCalledCount.Should().Be(installCallCountBeforeTheSecondRestore + 1);
        }

        [Fact]
        public void WhenRunWithoutManifestFileItShouldPrintSpecificRestoreErrorMessage()
        {
            IToolManifestFinder manifestFinder =
                new CannotFindManifestFinder();

            ToolRestoreCommand toolRestoreCommand = new(_parseResult,
                _toolPackageDownloaderMock,
                manifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            toolRestoreCommand.Execute().Should().Be(0);

            _reporter.Lines.Should().Contain(l =>
                l.Contains(AnsiExtensions.Yellow(LocalizableStrings.NoToolsWereRestored)));
        }

        private class MockManifestFinder : IToolManifestFinder
        {
            private readonly IReadOnlyCollection<ToolManifestPackage> _toReturn;

            public MockManifestFinder(IReadOnlyCollection<ToolManifestPackage> toReturn)
            {
                _toReturn = toReturn;
            }

            public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
            {
                return _toReturn;
            }

            public FilePath FindFirst(bool createManifestFileOption = false)
            {
                throw new NotImplementedException();
            }

            public IReadOnlyList<FilePath> FindByPackageId(PackageId packageId)
            {
                throw new NotImplementedException();
            }
        }

        private class CannotFindManifestFinder : IToolManifestFinder
        {
            public IReadOnlyCollection<ToolManifestPackage> Find(FilePath? filePath = null)
            {
                throw new ToolManifestCannotBeFoundException("In test cannot find manifest");
            }

            public FilePath FindFirst(bool createManifestFileOption = false)
            {
                throw new NotImplementedException();
            }

            public IReadOnlyList<FilePath> FindByPackageId(PackageId packageId)
            {
                throw new NotImplementedException();
            }
        }
    }
}

