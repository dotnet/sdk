// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Tests;

public partial class AotIntegrationTests
{
    [TestMethod]
    [DataRow("zh-CN", "build", ".NET \u751f\u6210\u5668")]
    [DataRow("fr-CA", "sln", "Commande .NET Modifier un fichier solution")]
    public void LocalizedHelp_MatchesManagedCli(string language, string command, string translatedDescription)
    {
        SkipIfDnUnavailable();
        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = language,
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0",
        };
        var native = RunDn([command, "--help"], enableAot: true, extraEnv: environment);
        var managed = RunDn([command, "--help"], enableAot: false, extraEnv: environment);

        Assert.AreEqual(0, native.exitCode, native.stderr);
        Assert.AreEqual(managed.exitCode, native.exitCode);
        native.stdout.Should().Contain(translatedDescription).And.Be(managed.stdout);
        native.stderr.Should().Be(managed.stderr);
    }

    [TestMethod]
    public void LocalizedSlnHelp_UsesManagedSatelliteResources()
    {
        SkipIfDnUnavailable();

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "fr",
        };
        var (exitCode, stdout, stderr) = RunDn(
            ["sln", "--help"],
            enableAot: true,
            extraEnv: environment);

        Assert.AreEqual(0, exitCode, stderr);
        stdout.Should().Contain("Commande .NET Modifier un fichier solution");
        stdout.Should().Contain("Ajoutez un ou plusieurs projets");
    }

    [TestMethod]
    public void ParentCultureSlnHelp_UsesManagedParentSatellite()
    {
        SkipIfDnUnavailable();

        var environment = new Dictionary<string, string>
        {
            ["DOTNET_CLI_UI_LANGUAGE"] = "fr-CA",
        };
        var (exitCode, stdout, stderr) = RunDn(
            ["sln", "--help"],
            enableAot: true,
            extraEnv: environment);

        Assert.AreEqual(0, exitCode, stderr);
        stdout.Should().Contain("Commande .NET Modifier un fichier solution");
    }

    [TestMethod]
    public void MalformedExactCultureSatellite_FallsBackToParent()
    {
        SkipIfDnUnavailable();
        string? resourceMode = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_RESOURCE_MODE");
        if (string.Equals(resourceMode, "Embedded", StringComparison.Ordinal))
        {
            Assert.Inconclusive("Malformed external satellite probing applies only to external resource modes.");
        }

        string? sdkDirectory = Environment.GetEnvironmentVariable("DOTNET_AOT_TEST_SDK_DIRECTORY");
        Assert.IsFalse(string.IsNullOrEmpty(sdkDirectory));

        string cultureDirectory = Path.Combine(sdkDirectory, "fr-CA");
        string malformedSatellite = Path.Combine(
            cultureDirectory,
            "Microsoft.DotNet.Cli.Definitions.resources.dll");
        Directory.CreateDirectory(cultureDirectory);
        byte[]? originalSatellite = File.Exists(malformedSatellite)
            ? File.ReadAllBytes(malformedSatellite)
            : null;
        File.WriteAllText(malformedSatellite, "not a managed satellite assembly");

        try
        {
            var environment = new Dictionary<string, string>
            {
                ["DOTNET_CLI_UI_LANGUAGE"] = "fr-CA",
            };
            var (exitCode, stdout, stderr) = RunDn(
                ["sln", "--help"],
                enableAot: true,
                extraEnv: environment);

            Assert.AreEqual(0, exitCode, stderr);
            stdout.Should().Contain("Commande .NET Modifier un fichier solution");
        }
        finally
        {
            if (originalSatellite is null)
            {
                File.Delete(malformedSatellite);
            }
            else
            {
                File.WriteAllBytes(malformedSatellite, originalSatellite);
            }
        }
    }
}
