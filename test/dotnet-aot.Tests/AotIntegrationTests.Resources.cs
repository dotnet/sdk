// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Tests;

public partial class AotIntegrationTests
{
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
