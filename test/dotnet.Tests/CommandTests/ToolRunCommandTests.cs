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
    public class ToolRunCommandTests: SdkTest
    {
        private const string ManifestFilename = "dotnet-tools.json";
        private DirectoryPath _nugetGlobalPackagesFolder;

        public ToolRunCommandTests(ITestOutputHelper log) : base(log)
        {
            _nugetGlobalPackagesFolder = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation());
        }

        [Fact]
        public void WhenRunWithRollForwardOptionItShouldIncludeRollForwardInNativeHost()
        {
            var parseResult = Parser.Instance.Parse($"dotnet tool run dotnet-a --allow-roll-forward");

            var toolRunCommand = new ToolRunCommand(parseResult);

            (FilePath fakeExecutable, LocalToolsCommandResolver localToolsCommandResolver) = DefaultSetup("a");
            IEnumerable<string> testForwardArgument = Enumerable.Empty<string>();

            var result = localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
            {
                CommandName = "dotnet-a",
                CommandArguments = testForwardArgument
            }, toolRunCommand._allowRollForward); 

            result.Should().NotBeNull();
            result.Args.Should().Contain("--roll-forward", "Major", fakeExecutable.Value);
        }

        [Fact]
        public void WhenRunWithoutRollForwardOptionItShouldNotIncludeRollForwardInNativeHost()
        {
            var parseResult = Parser.Instance.Parse($"dotnet tool run dotnet-a");

            var toolRunCommand = new ToolRunCommand(parseResult);

            (FilePath fakeExecutable, LocalToolsCommandResolver localToolsCommandResolver) = DefaultSetup("a");
            IEnumerable<string> testForwardArgument = Enumerable.Empty<string>();

            var result = localToolsCommandResolver.ResolveStrict(new CommandResolverArguments()
            {
                CommandName = "dotnet-a",
                CommandArguments = testForwardArgument
            }, toolRunCommand._allowRollForward);

            result.Should().NotBeNull();
            result.Args.Should().Contain(fakeExecutable.Value);
            result.Args.Should().NotContain("--roll-forward", "Major");
        }

        private (FilePath, LocalToolsCommandResolver) DefaultSetup(string toolCommand)
        {
            var testDirectoryRoot = _testAssetsManager.CreateTestDirectory();
            var fileSystem = new FileSystemWrapper();
            NuGetVersion packageVersionA = NuGetVersion.Parse("1.0.4");

            fileSystem.File.WriteAllText(Path.Combine(testDirectoryRoot.Path, ManifestFilename),
                _jsonContent.Replace("$TOOLCOMMAND$", toolCommand));
            ToolManifestFinder toolManifest =
                new ToolManifestFinder(new DirectoryPath(testDirectoryRoot.Path), fileSystem, new FakeDangerousFileDetector());
            ToolCommandName toolCommandNameA = new ToolCommandName(toolCommand);
            FilePath fakeExecutable = _nugetGlobalPackagesFolder.WithFile("fakeExecutable.dll");

            fileSystem.Directory.CreateDirectory(_nugetGlobalPackagesFolder.Value);
            fileSystem.File.CreateEmptyFile(fakeExecutable.Value);

            string temporaryDirectory = fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath;
            var localToolsResolverCache = new LocalToolsResolverCache(
                fileSystem,
                new DirectoryPath(Path.Combine(temporaryDirectory, "cache")));

            localToolsResolverCache.Save(
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
                localToolsResolverCache,
                fileSystem);

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
