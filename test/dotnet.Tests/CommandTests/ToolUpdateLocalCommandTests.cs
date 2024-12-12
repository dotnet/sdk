// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.Restore;
using Microsoft.DotNet.Tools.Tool.Update;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;
using Parser = Microsoft.DotNet.Cli.Parser;


namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolUpdateLocalCommandTests
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _temporaryDirectoryParent;
        private readonly ParseResult _parseResult;
        private readonly ParseResult _parseResultUpdateAll;
        private readonly BufferedReporter _reporter;
        private readonly string _temporaryDirectory;
        private readonly string _manifestFilePath;
        private readonly MockFeed _mockFeed;
        private readonly ToolManifestFinder _toolManifestFinder;
        private readonly ToolManifestEditor _toolManifestEditor;
        private readonly ToolUpdateLocalCommand _defaultToolUpdateLocalCommand;
        private readonly ToolUpdateLocalCommand _toolUpdateAllLocalCommand;
        private readonly string _pathToPlacePackages;
        private readonly IToolPackageStore _toolPackageStore;
        private readonly ToolPackageDownloaderMock _toolPackageDownloaderMock;
        private readonly NuGetVersion _packageOriginalVersionA;
        private readonly NuGetVersion _packageNewVersionA;
        private readonly PackageId _packageIdA = new("local.tool.console.a");
        private readonly PackageId _packageIdB = new("local.tool.console.b");
        private readonly ToolCommandName _toolCommandNameA = new("a");
        private readonly ToolCommandName _toolCommandNameB = new("b");
        private readonly ILocalToolsResolverCache _localToolsResolverCache;
        private readonly ToolRestoreCommand _toolRestoreCommand;

        public ToolUpdateLocalCommandTests()
        {
            _reporter = new BufferedReporter();
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();

            _temporaryDirectoryParent = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _temporaryDirectory = Path.Combine(_temporaryDirectoryParent, "sub");
            _fileSystem.Directory.CreateDirectory(_temporaryDirectory);
            _pathToPlacePackages = Path.Combine(_temporaryDirectory, "pathToPlacePackage");

            _packageOriginalVersionA = NuGetVersion.Parse("1.0.0");
            _packageNewVersionA = NuGetVersion.Parse("2.0.0");

            ToolPackageStoreMock toolPackageStoreMock =
                new(new DirectoryPath(_pathToPlacePackages), _fileSystem);
            _toolPackageStore = toolPackageStoreMock;
            _mockFeed = new MockFeed
            {
                Type = MockFeedType.ImplicitAdditionalFeed,
                Packages = new List<MockFeedPackage>
                {
                    new MockFeedPackage
                    {
                        PackageId = _packageIdA.ToString(),
                        Version = _packageOriginalVersionA.ToNormalizedString(),
                        ToolCommandName = _toolCommandNameA.ToString()
                    },
                    new MockFeedPackage
                    {
                        PackageId = _packageIdB.ToString(),
                        Version = _packageOriginalVersionA.ToNormalizedString(),
                        ToolCommandName = _toolCommandNameB.ToString()
                    },
                }
            };

            _toolPackageDownloaderMock = new ToolPackageDownloaderMock(
                store: _toolPackageStore,
                fileSystem: _fileSystem,
                reporter: _reporter,       
                new List<MockFeed>
                {
                    _mockFeed
                });

            _localToolsResolverCache
                = new LocalToolsResolverCache(
                    _fileSystem,
                    new DirectoryPath(Path.Combine(_temporaryDirectory, "cache")),
                    1);

            _manifestFilePath = Path.Combine(_temporaryDirectory, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonContent);
            _toolManifestFinder = new ToolManifestFinder(new DirectoryPath(_temporaryDirectory), _fileSystem,
                new FakeDangerousFileDetector());
            _toolManifestEditor = new ToolManifestEditor(_fileSystem, new FakeDangerousFileDetector());

            _parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()}");
            _parseResultUpdateAll = Parser.Instance.Parse($"dotnet tool update --all --local");

            _toolRestoreCommand = new ToolRestoreCommand(
                _parseResult,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            );

            _defaultToolUpdateLocalCommand = new ToolUpdateLocalCommand(
                _parseResult,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            _toolUpdateAllLocalCommand = new ToolUpdateLocalCommand(
                _parseResultUpdateAll,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);
        }

        [Fact]
        public void WhenPassingRestoreActionConfigOptions()
        {
            var parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()} --ignore-failed-sources");
            var command = new ToolUpdateLocalCommand(parseResult);
            command._toolInstallLocalCommand.Value.restoreActionConfig.IgnoreFailedSources.Should().BeTrue();
        }

        [Fact]
        public void WhenPassingIgnoreFailedSourcesItShouldNotThrow()
        {
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, "nuget.config"), _nugetConfigWithInvalidSources);
            var parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()} --ignore-failed-sources");
            var updateLocalCommand = new ToolUpdateLocalCommand(
                parseResult,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            updateLocalCommand.Execute().Should().Be(0);

            _fileSystem.File.Delete(Path.Combine(_temporaryDirectory, "nuget.config"));
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldUpdateFromManifestFile()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void WhenRunWithUpdateAllItShouldUpdateFromManifestFile()
        {
            _toolRestoreCommand.Execute();
            new ToolRestoreCommand(
                Parser.Instance.Parse($"dotnet tool restore"),
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _localToolsResolverCache,
                _fileSystem,
                _reporter
            ).Execute();

            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();
            _mockFeed.Packages[1].Version = _packageNewVersionA.ToNormalizedString();

            _toolUpdateAllLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess(packageIdExpected: _packageIdA.ToString());
            AssertUpdateSuccess(packageIdExpected: _packageIdB.ToString());
        }

        [Fact]
        public void WhenRunFromDirectorWithPackageIdItShouldUpdateFromManifestFile()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            var toolUpdateCommand = new ToolUpdateCommand(
                _parseResult,
                _reporter,
                new ToolUpdateGlobalOrToolPathCommand(_parseResult),
                _defaultToolUpdateLocalCommand);

            toolUpdateCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void GivenNoRestoredManifestWhenRunWithPackageIdItShouldUpdateFromManifestFile()
        {
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void GivenManifestDoesNotHavePackageWhenRunWithPackageIdItShouldUpdate()
        {
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.File.WriteAllText(Path.Combine(_temporaryDirectory, _manifestFilePath), _jsonEmptyContent);

            _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            AssertUpdateSuccess();
        }

        [Fact]
        public void GivenNoManifestFileItShouldThrow()
        {
            _fileSystem.File.Delete(_manifestFilePath);
            Action a = () => _defaultToolUpdateLocalCommand.Execute().Should().Be(0);

            a.Should().Throw<GracefulException>()
                .And.Message.Should()
                .Contain(ToolManifest.LocalizableStrings.CannotFindAManifestFile);

            a.Should().Throw<GracefulException>()
                .And.VerboseMessage.Should().Contain(string.Format(ToolManifest.LocalizableStrings.ListOfSearched, ""));
        }

        [Fact]
        public void WhenRunWithExplicitManifestFileItShouldUpdateFromExplicitManifestFile()
        {
            string explicitManifestFilePath = Path.Combine(_temporaryDirectory, "subdirectory", "dotnet-tools.json");
            _fileSystem.File.Delete(_manifestFilePath);
            _fileSystem.Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "subdirectory"));
            _fileSystem.File.WriteAllText(explicitManifestFilePath, _jsonContent);

            ParseResult parseResult
                = Parser.Instance.Parse(
                    $"dotnet tool update {_packageIdA.ToString()} --tool-manifest {explicitManifestFilePath}");

            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            ToolUpdateLocalCommand toolUpdateLocalCommand = new(
                parseResult,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            toolUpdateLocalCommand.Execute().Should().Be(0);
            AssertUpdateSuccess(new FilePath(explicitManifestFilePath));
        }

        [Fact]
        public void WhenRunFromToolUpdateRedirectCommandWithPackageIdItShouldUpdateFromManifestFile()
        {
            ParseResult parseResult = Parser.Instance.Parse($"dotnet tool update {_packageIdA.ToString()}");

            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            ToolUpdateLocalCommand toolUpdateLocalCommand = new(
                parseResult,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);
            ToolUpdateCommand toolUpdateCommand = new(
                parseResult,
                toolUpdateLocalCommand: toolUpdateLocalCommand);

            toolUpdateCommand.Execute().Should().Be(0);
        }

        [Fact]
        public void WhenRunWithPackageIdItShouldShowSuccessMessage()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            _reporter.Clear();
            _defaultToolUpdateLocalCommand.Execute();

            _reporter.Lines.Single()
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.UpdateLocalToolSucceeded,
                        _packageIdA,
                        _packageOriginalVersionA.ToNormalizedString(),
                        _packageNewVersionA.ToNormalizedString(),
                        _manifestFilePath).Green());
        }

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldOnlyChangTheClosestOne()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();

            _reporter.Clear();
            _defaultToolUpdateLocalCommand.Execute();

            AssertUpdateSuccess();

            _fileSystem.File.ReadAllText(parentManifestFilePath).Should().Be(_jsonContent, "no change");
        }

        [Fact]
        public void GivenParentDirHasManifestWithSamePackageIdWhenRunWithPackageIdItShouldWarningTheOtherManifests()
        {
            var parentManifestFilePath = Path.Combine(_temporaryDirectoryParent, "dotnet-tools.json");
            _fileSystem.File.WriteAllText(parentManifestFilePath, _jsonContent);

            _toolRestoreCommand.Execute();

            _reporter.Clear();
            _mockFeed.Packages[0].Version = _packageNewVersionA.ToNormalizedString();
            _defaultToolUpdateLocalCommand.Execute();

            _reporter.Lines[0].Should().Contain(parentManifestFilePath);
            _reporter.Lines[0].Should().NotContain(_manifestFilePath);
        }

        [Fact]
        public void GivenFeedVersionIsTheSameWhenRunWithPackageIdItShouldShowDifferentSuccessMessage()
        {
            _toolRestoreCommand.Execute();

            _reporter.Clear();
            _defaultToolUpdateLocalCommand.Execute();

            AssertUpdateSuccess(packageVersion: _packageOriginalVersionA);

            _reporter.Lines.Single()
                .Should().Contain(
                    string.Format(
                        LocalizableStrings.UpdateLocaToolSucceededVersionNoChange,
                        _packageIdA,
                        _packageOriginalVersionA.ToNormalizedString(),
                        _manifestFilePath));
        }

        [Fact]
        public void GivenFeedVersionIsLowerRunPackageIdItShouldThrow()
        {
            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = "0.9.0";

            _reporter.Clear();
            Action a = () => _defaultToolUpdateLocalCommand.Execute();
            a.Should().Throw<GracefulException>().And.Message.Should().Contain(string.Format(
                LocalizableStrings.UpdateLocalToolToLowerVersion,
                "0.9.0",
                _packageOriginalVersionA.ToNormalizedString(),
                _manifestFilePath));
        }

        [Fact]
        public void GivenFeedVersionIsLowerWithDowngradeFlagRunPackageIdItShouldSucceeds()
        {
            _reporter.Clear();

            ParseResult parseResult
                = Parser.Instance.Parse(
                    $"dotnet tool update {_packageIdA.ToString()} --version 0.9.0 --allow-downgrade");

            _toolRestoreCommand.Execute();
            _mockFeed.Packages[0].Version = "0.9.0";

            ToolUpdateLocalCommand toolUpdateLocalCommand = new ToolUpdateLocalCommand(
                parseResult,
                _toolPackageDownloaderMock,
                _toolManifestFinder,
                _toolManifestEditor,
                _localToolsResolverCache,
                _reporter);

            toolUpdateLocalCommand.Execute().Should().Be(0);
        }

        private void AssertUpdateSuccess(
            FilePath? manifestFile = null,
            NuGetVersion packageVersion = null,
            string packageIdExpected = "local.tool.console.a")
        {
            packageVersion ??= _packageNewVersionA;
            IReadOnlyCollection<ToolManifestPackage> manifestPackages = _toolManifestFinder.Find(manifestFile);

            manifestPackages.Should().Contain(
                pkg => pkg.PackageId.ToString() == packageIdExpected && pkg.Version == packageVersion,
                $"expected package {packageIdExpected} to be updated to version {packageVersion}");

            ToolManifestPackage updatedPackage = manifestPackages.First(
                pkg => packageIdExpected == null || pkg.PackageId.ToString() == packageIdExpected);
            _localToolsResolverCache.TryLoad(new RestoredCommandIdentifier(
                    updatedPackage.PackageId,
                    updatedPackage.Version,
                    NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                    Constants.AnyRid,
                    updatedPackage.CommandNames.Single()),
                out RestoredCommand restoredCommand
            ).Should().BeTrue();

            _fileSystem.File.Exists(restoredCommand.Executable.Value).Should().BeTrue();
        }

        private readonly string _jsonContent =
            @"{
  ""version"": 1,
  ""isRoot"": false,
  ""tools"": {
    ""local.tool.console.a"": {
      ""version"": ""1.0.0"",
      ""commands"": [
        ""a""
      ]
    },
    ""local.tool.console.b"": {
      ""version"": ""1.0.0"",
      ""commands"": [
        ""a""
      ]
    }
  }
}";

        private readonly string _jsonEmptyContent =
            @"{
  ""version"": 1,
  ""isRoot"": false,
  ""tools"": {}
}";

        private string _nugetConfigWithInvalidSources = @"{
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""invalid_source"" value=""https://api.nuget.org/v3/invalid.json"" />
  </packageSources>
</configuration>
}";
    }
}

