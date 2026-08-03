// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Tool.Uninstall;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    [TestClass]
    public class UninstallToolParserTests
    {

        [TestMethod]
        public void UninstallToolParserCanGetPackageId()
        {
            var result = Parser.Parse("dotnet tool uninstall -g console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUninstallCommandDefinition>(result.CommandResult.Command);
            var packageId = result.GetValue(definition.PackageIdArgument);

            packageId.Should().Be("console.test.app");
        }

        [TestMethod]
        public void UninstallToolParserCanGetGlobalOption()
        {
            var result = Parser.Parse("dotnet tool uninstall -g console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUninstallCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.GlobalOption).Should().Be(true);
        }

        [TestMethod]
        public void UninstallToolParserCanParseToolPathOption()
        {
            var result =
                Parser.Parse(@"dotnet tool uninstall --tool-path C:\Tools console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUninstallCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.ToolPathOption).Should().Be(@"C:\Tools");
        }

        [TestMethod]
        public void UninstallToolParserCanParseLocalOption()
        {
            var result =
                Parser.Parse(@"dotnet tool uninstall --local console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUninstallCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.LocationOptions.LocalOption).Should().Be(true);
        }

        [TestMethod]
        public void UninstallToolParserCanParseToolManifestOption()
        {
            var result =
                Parser.Parse(@"dotnet tool uninstall --tool-manifest folder/my-manifest.format console.test.app");

            var definition = Assert.IsExactInstanceOfType<ToolUninstallCommandDefinition>(result.CommandResult.Command);
            result.GetValue(definition.ToolManifestOption).Should().Be(@"folder/my-manifest.format");
        }
    }
}
