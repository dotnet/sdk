// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Run;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tests.Commands.Tool
{
    public class ToolRunCommandTests 
    {
        private readonly IFileSystem _fileSystem;
        private const string ManifestFilename = "dotnet-tools.json";
        private readonly string _testDirectoryRoot;
        private DirectoryPath _nugetGlobalPackagesFolder;
        private readonly LocalToolsResolverCache _localToolsResolverCache;

        public ToolRunCommandTests() 
        {
            _fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            _nugetGlobalPackagesFolder = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
            string temporaryDirectory = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            _localToolsResolverCache = new LocalToolsResolverCache(
                _fileSystem,
                new DirectoryPath(Path.Combine(temporaryDirectory, "cache")));
            _testDirectoryRoot = _fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;

        }

        [Fact]
        public void WhenRunWithRollForwardOptionItShouldIncludeRollForwardInNativeHost()
        {
            var parseResult = Parser.Instance.Parse($"dotnet tool run $TOOLCOMMAND$ --roll-forward Major");

            var toolRunCommand = new ToolRunCommand(
            parseResult);

            (FilePath fakeExecutable, LocalToolsCommandResolver localToolsCommandResolver) = DefaultSetup("dotnet-a");
            IEnumerable<string> testForwardArgument = Enumerable.Empty<string>();

            var result = localToolsCommandResolver.Resolve(new CommandResolverArguments()
            {
                CommandName = "dotnet-a",
                CommandArguments = (toolRunCommand._rollForward != null ? new List<string> { "--roll-forward", toolRunCommand._rollForward } : Enumerable.Empty<string>()).Concat(testForwardArgument)
            });

            result.Should().NotBeNull();
            result.Args.Should().Contain("--roll-forward", toolRunCommand._rollForward, fakeExecutable.Value);
        }

        private (FilePath, LocalToolsCommandResolver) DefaultSetup(string toolCommand)
        {
            NuGetVersion packageVersionA = NuGetVersion.Parse("1.0.4");
            _fileSystem.File.WriteAllText(Path.Combine(_testDirectoryRoot, ManifestFilename),
                _jsonContent.Replace("$TOOLCOMMAND$", toolCommand));
            ToolManifestFinder toolManifest =
                new ToolManifestFinder(new DirectoryPath(_testDirectoryRoot), _fileSystem, new FakeDangerousFileDetector());
            ToolCommandName toolCommandNameA = new ToolCommandName(toolCommand);
            FilePath fakeExecutable = _nugetGlobalPackagesFolder.WithFile("fakeExecutable.dll");
            _fileSystem.Directory.CreateDirectory(_nugetGlobalPackagesFolder.Value);
            _fileSystem.File.CreateEmptyFile(fakeExecutable.Value);
            _localToolsResolverCache.Save(
                new Dictionary<RestoredCommandIdentifier, RestoredCommand>
                {
                    [new RestoredCommandIdentifier(
                            new PackageId("local.tool.console.a"),
                            packageVersionA,
                            NuGetFramework.Parse(BundledTargetFramework.GetTargetFrameworkMoniker()),
                            Constants.AnyRid,
                            toolCommandNameA)]
                        = new RestoredCommand(toolCommandNameA, "dotnet", fakeExecutable)
                });

            var localToolsCommandResolver = new LocalToolsCommandResolver(
                toolManifest,
                _localToolsResolverCache,
                _fileSystem);

            return (fakeExecutable, localToolsCommandResolver);
        }

        private string _jsonContent =
            @"{
   ""version"":1,
   ""isRoot"":true,
   ""tools"":{
      ""local.tool.console.a"":{
         ""version"":""1.0.4"",
         ""commands"":[
            ""$TOOLCOMMAND$""
         ]
      }
   }
}";

    }
}
