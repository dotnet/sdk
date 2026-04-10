// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.CommandLineParserTests
{
    public class RestoreCommandLineParserTests
    {
        private readonly ITestOutputHelper output;

        public RestoreCommandLineParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsSpecified()
        {
            var result = Parser.Parse(["dotnet", "restore", @".\some.csproj", "--packages", @"c:\.nuget\packages", "/p:SkipInvalidConfigurations=true"]);

            result.GetValue(RestoreCommandParser.SlnOrProjectOrFileArgument).Should().BeEquivalentTo(@".\some.csproj");
            result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()).Should().Contain(@"--property:SkipInvalidConfigurations=true");
        }

        [Fact]
        public void RestoreCapturesArgumentsToForwardToMSBuildWhenTargetIsNotSpecified()
        {
            var result = Parser.Parse(["dotnet", "restore", "--packages", @"c:\.nuget\packages", "/p:SkipInvalidConfigurations=true"]);

            result.OptionValuesToBeForwarded(RestoreCommandParser.GetCommand()).Should().Contain(@"--property:SkipInvalidConfigurations=true");
        }

        [Fact]
        public void RestoreDistinguishesRepeatSourceArgsFromCommandArgs()
        {
            var restore =
                Parser.Parse(["dotnet", "restore", "--no-cache", "--packages", @"D:\OSS\corefx\packages", "--source", "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json", "--source", "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json", "--source", "https://api.nuget.org/v3/index.json", @"D:\OSS\corefx\external\runtime\runtime.depproj"]);

            restore.GetValue(RestoreCommandParser.SlnOrProjectOrFileArgument).Should().BeEquivalentTo(@"D:\OSS\corefx\external\runtime\runtime.depproj");

            restore.GetValue(RestoreCommandParser.SourceOption)
                .Should()
                .BeEquivalentTo(
                    "https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json",
                    "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json",
                    "https://api.nuget.org/v3/index.json");
        }
    }
}
