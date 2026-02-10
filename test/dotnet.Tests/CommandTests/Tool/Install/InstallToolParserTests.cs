// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class InstallToolParserTests
    {
        private readonly ITestOutputHelper output;

        public InstallToolParserTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Theory]
        [InlineData("console.test.app --version 1.0.0", "1.0.0")]
        [InlineData("console.test.app --version 1.*", "1.*")]
        [InlineData("console.test.app@1.0.0", "1.0.0")]
        [InlineData("console.test.app@1.*", "1.*")]
        public void InstallGlobalToolParserCanGetPackageIdentityWithVersion(string arguments, string expectedVersion)
        {
            var result = Parser.Parse($"dotnet tool install -g {arguments}");
            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            var packageIdentity = result.GetValue(definition.PackageIdentityArgument);
            var packageId = packageIdentity.Id;
            var packageVersion = packageIdentity.VersionRange?.OriginalString ?? result.GetValue(definition.VersionOption);
            packageId.Should().Be("console.test.app");
            packageVersion.Should().Be(expectedVersion);
        }

        [Fact]
        public void InstallGlobaltoolParserCanGetFollowingArguments()
        {
            var result =
                Parser.Parse(
                    $@"dotnet tool install -g console.test.app --version 1.0.1 --framework {ToolsetInfo.CurrentTargetFramework} --configfile C:\TestAssetLocalNugetFeed");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
            result.GetRequiredValue(definition.FrameworkOption).Should().Be(ToolsetInfo.CurrentTargetFramework);
        }

        [Fact]
        public void InstallToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Parse($"dotnet tool install -g --add-source {expectedSourceValue} console.test.app");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [Fact]
        public void InstallToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Parse(
                    $"dotnet tool install -g " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2} console.test.app");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);

            result.GetRequiredValue(definition.AddSourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetRequiredValue(definition.AddSourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [Fact]
        public void InstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Parse("dotnet tool install -g console.test.app");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.LocationOptions.GlobalOption).Should().Be(true);
        }

        [Fact]
        public void InstallToolParserCanGetLocalOption()
        {
            var result = Parser.Parse("dotnet tool install --local console.test.app");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.LocationOptions.LocalOption).Should().Be(true);
        }

        [Fact]
        public void InstallToolParserCanGetManifestOption()
        {
            var result =
                Parser.Parse(
                    "dotnet tool install --local console.test.app --tool-manifest folder/my-manifest.format");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.ToolManifestOption).Should().Be("folder/my-manifest.format");
        }

        [Fact]
        public void InstallToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Parse($"dotnet tool install -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            Enum.GetName(result.GetRequiredValue(definition.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [Fact]
        public void InstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Parse(@"dotnet tool install --tool-path C:\Tools console.test.app");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.LocationOptions.ToolPathOption).Should().Be(@"C:\Tools");
        }

        [Fact]
        public void InstallToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool install -g console.test.app --no-cache");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.NoCacheOption).Should().BeTrue();
        }

        [Fact]
        public void InstallToolParserCanParseNoHttpCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool install -g console.test.app --no-http-cache");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.NoHttpCacheOption).Should().BeTrue();
        }

        [Fact]
        public void InstallToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Parse(@"dotnet tool install -g console.test.app --ignore-failed-sources");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.IgnoreFailedSourcesOption).Should().BeTrue();
        }

        [Fact]
        public void InstallToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Parse(@"dotnet tool install -g console.test.app --disable-parallel");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.DisableParallelOption).Should().BeTrue();
        }

        [Fact]
        public void InstallToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Parse(@"dotnet tool install -g console.test.app --interactive");

            var definition = Assert.IsType<ToolInstallCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.InteractiveOption).Should().BeTrue();
        }

        [Fact]
        public void InstallToolWithBothAtVersionAndVersionOptionThrowsError()
        {
            var result = Parser.Parse("dotnet tool install -g console.test.app@1.0.0 --version 2.0.0");

            var toolInstallCommand = new Cli.Commands.Tool.Install.ToolInstallCommand(result);

            Action a = () => toolInstallCommand.Execute();

            a.Should().Throw<GracefulException>().And.Message
                .Should().Contain(Cli.CliStrings.PackageIdentityArgumentVersionOptionConflict);
        }
    }
}
