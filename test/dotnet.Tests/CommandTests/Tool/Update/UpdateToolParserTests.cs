// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Update;
using Microsoft.DotNet.Cli.Utils;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class UpdateInstallToolParserTests
    {

        [TestMethod]
        [DataRow("console.test.app --version 1.0.0", "1.0.0")]
        [DataRow("console.test.app --version 1.*", "1.*")]
        [DataRow("console.test.app@1.0.0", "1.0.0")]
        [DataRow("console.test.app@1.*", "1.*")]
        public void UpdateGlobalToolParserCanGetPackageIdentityWithVersion(string arguments, string expectedVersion)
        {
            var result = Parser.Parse($"dotnet tool update -g {arguments}");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            var packageIdentity = result.GetValue(definition.PackageIdentityArgument);
            var packageId = packageIdentity?.Id;
            var packageVersion = packageIdentity?.VersionRange?.OriginalString ?? result.GetValue(definition.VersionOption);
            packageId.Should().Be("console.test.app");
            packageVersion.Should().Be(expectedVersion);
        }


        [TestMethod]
        public void UpdateGlobaltoolParserCanGetPackageId()
        {
            var result = Parser.Parse("dotnet tool update -g console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            var packageId = result.GetValue(definition.PackageIdentityArgument)?.Id;

            packageId.Should().Be("console.test.app");
        }

        [TestMethod]
        public void UpdateToolParserCanGetGlobalOption()
        {
            var result = Parser.Parse("dotnet tool update -g console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.GlobalOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanGetFollowingArguments()
        {
            var result =
                Parser.Parse(
                    $@"dotnet tool update -g console.test.app --version 1.0.1 --framework {ToolsetInfo.CurrentTargetFramework} --configfile C:\TestAssetLocalNugetFeed");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
            result.GetValue(definition.FrameworkOption).Should().Be(ToolsetInfo.CurrentTargetFramework);
        }

        [TestMethod]
        public void UpdateToolParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Parse($"dotnet tool update -g --add-source {expectedSourceValue} console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [TestMethod]
        public void UpdateToolParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Parse(
                    $"dotnet tool update -g " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2} console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.AddSourceOption).Should().BeEquivalentTo([expectedSourceValue1, expectedSourceValue2]);
        }

        [TestMethod]
        public void UpdateToolParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result =
                Parser.Parse($"dotnet tool update -g --verbosity:{expectedVerbosityLevel} console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            Enum.GetName(result.GetValue(definition.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [TestMethod]
        public void UpdateToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update --tool-path C:\TestAssetLocalNugetFeed console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.ToolPathOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [TestMethod]
        public void UpdateToolParserCanParseNoCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --no-cache");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.NoCacheOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanParseNoHttpCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --no-http-cache");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.NoHttpCacheOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --ignore-failed-sources");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.IgnoreFailedSourcesOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --disable-parallel");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.DisableParallelOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --interactive");
            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.RestoreOptions.InteractiveOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanParseVersionOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update -g console.test.app --version 1.2");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.VersionOption).Should().Be("1.2");
        }

        [TestMethod]
        public void UpdateToolParserCanParseLocalOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update --local console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.LocalOption).Should().Be(true);
        }

        [TestMethod]
        public void UpdateToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Parse(@"dotnet tool update --tool-manifest folder/my-manifest.format console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUpdateCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.ToolManifestOption).Should().Be(@"folder/my-manifest.format");
        }

        [TestMethod]
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
