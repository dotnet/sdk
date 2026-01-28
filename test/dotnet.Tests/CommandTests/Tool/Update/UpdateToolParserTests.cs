// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class UpdateInstallToolParserTests
    {
        private readonly ITestOutputHelper _output;

        public UpdateInstallToolParserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("console.test.app --version 1.0.0", "1.0.0")]
        [InlineData("console.test.app --version 1.*", "1.*")]
        [InlineData("console.test.app@1.0.0", "1.0.0")]
        [InlineData("console.test.app@1.*", "1.*")]
        public void UpdateGlobalToolParserCanGetPackageIdentityWithVersion(string arguments, string expectedVersion)
        {
            var result = Parser.Parse($"dotnet tool update -g {arguments}");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            var packageIdentity = result.GetValue(definition.PackageIdentityArgument);
            var packageId = packageIdentity?.Id;
            var packageVersion = packageIdentity?.VersionRange?.OriginalString ?? result.GetValue(definition.VersionOption);
            packageId.Should().Be("console.test.app");
            packageVersion.Should().Be(expectedVersion);
        }


        [Fact]
        public void UpdateGlobaltoolParserCanGetPackageId()
        {
            var result = Parser.Parse("dotnet tool update -g console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            var packageId = result.GetValue(definition.PackageIdentityArgument)?.Id;

            packageId.Should().Be("console.test.app");
        }

        [Fact]
        public void UpdateToolParserCanGetGlobalOption()
        {
            var result = Parser.Parse("dotnet tool update -g console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanGetFollowingArguments()
        {
            var result =
                Parser.Parse(
                    $@"dotnet tool update -g console.test.app --version 1.0.1 --framework {ToolsetInfo.CurrentTargetFramework} --configfile C:\TestAssetLocalNugetFeed");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
            result.GetValue(definition.FrameworkOption).Should().Be(ToolsetInfo.CurrentTargetFramework);
        }

        [Fact]
        public void UpdateToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Parse($"dotnet tool update -g --add-source {expectedSourceValue} console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [Fact]
        public void UpdateToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Parse(
                    $"dotnet tool update -g " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2} console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.AddSourceOption).Should().BeEquivalentTo([expectedSourceValue1, expectedSourceValue2]);
        }

        [Fact]
        public void UpdateToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result =
                Parser.Parse($"dotnet tool update -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            Enum.GetName(result.GetValue(definition.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void UpdateToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update --tool-path C:\TestAssetLocalNugetFeed console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.ToolPathOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [Fact]
        public void UpdateToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --no-cache");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.NoCacheOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseNoHttpCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --no-http-cache");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.NoHttpCacheOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --ignore-failed-sources");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.IgnoreFailedSourcesOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --disable-parallel");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.DisableParallelOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --interactive");
            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.InteractiveOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseVersionOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --version 1.2");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.VersionOption).Should().Be("1.2");
        }

        [Fact]
        public void UpdateToolParserCanParseLocalOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update --local console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.LocalOption).Should().Be(true);
        }

        [Fact]
        public void UpdateToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update --tool-manifest folder/my-manifest.format console.test.app");

            var definition = Assert.IsType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.ToolManifestOption).Should().Be(@"folder/my-manifest.format");
        }

        [Fact]
        public void UpdateToolWithBothAtVersionAndVersionOptionThrowsError()
        {
            var result = Parser.Parse("dotnet tool update -g console.test.app@1.0.0 --version 2.0.0");

            var toolUpdateCommand = new Cli.Commands.Tool.Update.ToolUpdateCommand(result);

            Action a = () => toolUpdateCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Cli.CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }
    }
}
