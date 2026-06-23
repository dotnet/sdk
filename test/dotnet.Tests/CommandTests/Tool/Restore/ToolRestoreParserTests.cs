// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands.Tool;
using Microsoft.DotNet.Cli.Commands.Tool.Restore;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class ToolRestoreParserTests
    {

        [TestMethod]
        public void ToolRestoreParserCanGetManifestFilePath()
        {
            var result = Parser.Parse("dotnet tool restore --tool-manifest folder/my-manifest.format");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.ToolManifestOption).Should().Be("folder/my-manifest.format");
        }

        [TestMethod]
        public void ToolRestoreParserCanGetFollowingArguments()
        {
            var result =
                Parser.Parse(
                    @"dotnet tool restore --configfile C:\TestAssetLocalNugetFeed");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.ConfigOption).Should().Be(@"C:\TestAssetLocalNugetFeed");
        }

        [TestMethod]
        public void ToolRestoreParserCanParseSourceOption()
        {
            const string expectedSourceValue = "TestSourceValue";

            var result =
                Parser.Parse($"dotnet tool restore --add-source {expectedSourceValue}");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption).First().Should().Be(expectedSourceValue);
        }

        [TestMethod]
        public void ToolRestoreParserCanParseMultipleSourceOption()
        {
            const string expectedSourceValue1 = "TestSourceValue1";
            const string expectedSourceValue2 = "TestSourceValue2";

            var result =
                Parser.Parse(
                    $"dotnet tool restore " +
                    $"--add-source {expectedSourceValue1} " +
                    $"--add-source {expectedSourceValue2}");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.AddSourceOption)[0].Should().Be(expectedSourceValue1);
            result.GetRequiredValue(definition.AddSourceOption)[1].Should().Be(expectedSourceValue2);
        }

        [TestMethod]
        public void ToolRestoreParserCanParseVerbosityOption()
        {
            const string expectedVerbosityLevel = "diag";

            var result = Parser.Parse($"dotnet tool restore --verbosity {expectedVerbosityLevel}");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            Enum.GetName(result.GetRequiredValue(definition.VerbosityOption)).Should().Be(expectedVerbosityLevel);
        }

        [TestMethod]
        public void ToolRestoreParserCanParseNoCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool restore --no-cache");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.NoCacheOption).Should().BeTrue();
        }

        [TestMethod]
        public void ToolRestoreParserCanParseNoHttpCacheOption()
        {
            var result =
                Parser.Parse(@"dotnet tool restore --no-http-cache");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.NoHttpCacheOption).Should().BeTrue();
        }

        [TestMethod]
        public void ToolRestoreParserCanParseIgnoreFailedSourcesOption()
        {
            var result =
                Parser.Parse(@"dotnet tool restore --ignore-failed-sources");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.IgnoreFailedSourcesOption).Should().BeTrue();
        }

        [TestMethod]
        public void ToolRestoreParserCanParseDisableParallelOption()
        {
            var result =
                Parser.Parse(@"dotnet tool restore --disable-parallel");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.DisableParallelOption).Should().BeTrue();
        }

        [TestMethod]
        public void ToolRestoreParserCanParseInteractiveRestoreOption()
        {
            var result =
                Parser.Parse(@"dotnet tool restore --interactive");

            var definition = Assert.IsExactInstanceOfType<ToolRestoreCommandDefinition>(result.CommandResult.Command);
            result.GetRequiredValue(definition.RestoreOptions.InteractiveOption).Should().BeTrue();
        }
    }
}
