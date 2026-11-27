// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Format.Tests
{
    [TestClass]
    public class GivenDotnetFormatExecutesAndGeneratesHelpText : SdkTest
    {
        public GivenDotnetFormatExecutesAndGeneratesHelpText()
        {
        }

        [TestMethod]
        public void ItRuns()
        {
            new DotnetCommand(Log, "format")
                .Execute("--help")
                .Should().Pass();
        }

        [TestMethod]
        public void ItRespectsDotnetCliUiLanguage()
        {
            new DotnetCommand(Log, "format")
                .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "cs-CZ")
                .Execute("--help")
                .Should().Pass()
                .And.HaveStdOutContaining("Formátuje kód, aby odpovídal nastavení editorconfig.");

            if (!OperatingSystem.IsWindows())
            {
                new DotnetCommand(Log, "format")
                    .WithEnvironmentVariable("LANG", "cs_CZ.UTF-8")
                    .WithEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                    .Execute("--help")
                    .Should().Pass()
                    .And.HaveStdOutContaining("Formats code to match editorconfig settings.")
                    .And.NotHaveStdOutContaining("Formátuje kód, aby odpovídal nastavení editorconfig.");
            }
        }
    }
}
