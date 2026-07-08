// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.CommandLine;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.CommandLineParserTests
{
    [TestClass]
    public class RestoreCommandLineParserTests
    {

        [TestMethod]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsSpecified()
        {
            var result = Parser.Parse(["dotnet", "restore", @".\some.csproj", "--packages", @"c:\.nuget\packages", "/p:SkipInvalidConfigurations=true"]);
            var definition = Assert.IsExactInstanceOfType<RestoreCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.SlnOrProjectOrFileArgument).Should().BeEquivalentTo(@".\some.csproj");
            result.OptionValuesToBeForwarded(definition).Should().Contain(@"--property:SkipInvalidConfigurations=true");
        }

        [TestMethod]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsNotSpecified()
        {
            var result = Parser.Parse(["dotnet", "restore", "--packages", @"c:\.nuget\packages", "/p:SkipInvalidConfigurations=true"]);
            var definition = Assert.IsExactInstanceOfType<RestoreCommandDefinition>(result.CommandResult.Command);

            result.OptionValuesToBeForwarded(definition).Should().Contain(@"--property:SkipInvalidConfigurations=true");
        }

        [TestMethod]
        public void RestoreDistinguishesRepeatSourceArgsFromCommandArgs()
        {
            var restore =
                Parser.Parse(["dotnet", "restore", "--no-cache", "--packages", @"D:\OSS\corefx\packages", "--source", "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json", "--source", "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json", "--source", "https://api.nuget.org/v3/index.json", @"D:\OSS\corefx\external\runtime\runtime.depproj"]);

            var definition = Assert.IsExactInstanceOfType<RestoreCommandDefinition>(restore.CommandResult.Command);

            restore.GetValue(definition.SlnOrProjectOrFileArgument).Should().BeEquivalentTo(@"D:\OSS\corefx\external\runtime\runtime.depproj");

            restore.GetValue(definition.ImplicitRestoreOptions.SourceOption)
                .Should()
                .BeEquivalentTo(
                    "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json",
                    "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
                    "https://api.nuget.org/v3/index.json");
        }
    }
}
