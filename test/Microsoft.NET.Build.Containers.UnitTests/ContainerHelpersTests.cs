// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContainerHelpersTests
{
    private const string DefaultRegistry = "docker.io";

    [TestMethod]
    // Valid Tests
    [DataRow("mcr.microsoft.com", true)]
    [DataRow("mcr.microsoft.com:5001", true)] // Registries can have ports
    [DataRow("docker.io", true)] // default docker registry is considered valid

    // // Invalid tests
    [DataRow("mcr.mi-=crosoft.com", false)] // invalid url
    [DataRow("mcr.microsoft.com/", false)] // invalid url
    public void IsValidRegistry(string registry, bool expectedReturn)
    {
        Console.WriteLine($"Domain pattern is '{ReferenceParser.AnchoredDomainRegexp.ToString()}'");
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidRegistry(registry));
    }

    [TestMethod]
    [DataRow("mcr.microsoft.com/dotnet/runtime@sha256:6cec36412a215aad2a033cfe259890482be0a1dcb680e81fccc393b2d4069455", true, "mcr.microsoft.com", "dotnet/runtime", null, "sha256:6cec36412a215aad2a033cfe259890482be0a1dcb680e81fccc393b2d4069455", true)]
    // Handle both tag and digest
    [DataRow("mcr.microsoft.com/dotnet/runtime:6.0@sha256:6cec36412a215aad2a033cfe259890482be0a1dcb680e81fccc393b2d4069455", true, "mcr.microsoft.com", "dotnet/runtime", "6.0", "sha256:6cec36412a215aad2a033cfe259890482be0a1dcb680e81fccc393b2d4069455", true)]
    [DataRow("mcr.microsoft.com/dotnet/runtime:6.0", true, "mcr.microsoft.com", "dotnet/runtime", "6.0", null, true)]
    [DataRow("mcr.microsoft.com/dotnet/runtime", true, "mcr.microsoft.com", "dotnet/runtime", null, null, true)]
    [DataRow("mcr.microsoft.com/", false, null, null, null, null, false)] // no image = nothing resolves
    // Ports tag along
    [DataRow("mcr.microsoft.com:54/dotnet/runtime", true, "mcr.microsoft.com:54", "dotnet/runtime", null, null, true)]
    // Even if nonsensical
    [DataRow("mcr.microsoft.com:0/dotnet/runtime", true, "mcr.microsoft.com:0", "dotnet/runtime", null, null, true)]
    // We don't allow hosts with missing ports when a port is anticipated
    [DataRow("mcr.microsoft.com:/dotnet/runtime", false, null, null, null, null, false)]
    // Use default registry when no registry specified.
    [DataRow("ubuntu:jammy", true, DefaultRegistry, "library/ubuntu", "jammy", null, false)]
    [DataRow("ubuntu/runtime:jammy", true, DefaultRegistry, "ubuntu/runtime", "jammy", null, false)]
    // Alias 'docker.io' to Docker registry.
    [DataRow("docker.io/ubuntu:jammy", true, DefaultRegistry, "library/ubuntu", "jammy", null, true)]
    [DataRow("docker.io/ubuntu/runtime:jammy", true, DefaultRegistry, "ubuntu/runtime", "jammy", null, true)]
    // 'localhost' registry.
    [DataRow("localhost/ubuntu:jammy", true, "localhost", "ubuntu", "jammy", null, true)]
    public void TryParseFullyQualifiedContainerName(string fullyQualifiedName, bool expectedReturn, string? expectedRegistry, string? expectedImage, string? expectedTag, string? expectedDigest, bool expectedIsRegistrySpecified)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.TryParseFullyQualifiedContainerName(fullyQualifiedName, out string? containerReg, out string? containerName, out string? containerTag, out string? containerDigest, out bool isRegistrySpecified));
        Assert.AreEqual(expectedRegistry, containerReg);
        Assert.AreEqual(expectedImage, containerName);
        Assert.AreEqual(expectedTag, containerTag);
        Assert.AreEqual(expectedDigest, containerDigest);
        Assert.AreEqual(expectedIsRegistrySpecified, isRegistrySpecified);
    }

    [TestMethod]
    [DataRow("dotnet/runtime", true)]
    [DataRow("foo/bar", true)]
    [DataRow("registry", true)]
    [DataRow("-foo/bar", false)]
    [DataRow(".foo/bar", false)]
    [DataRow("_foo/bar", false)]
    [DataRow("foo/bar-", false)]
    [DataRow("foo/bar.", false)]
    [DataRow("foo/bar_", false)]
    [DataRow("--------", false)]
    public void IsValidImageName(string imageName, bool expectedReturn)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidImageName(imageName));
    }

    [TestMethod]
    [DataRow("0aa", "0aa", null, null)]
    [DataRow("9zz", "9zz", null, null)]
    [DataRow("aa0", "aa0", null, null)]
    [DataRow("zz9", "zz9", null, null)]
    [DataRow("runtime", "runtime", null, null)]
    [DataRow("dotnet_runtime", "dotnet_runtime", null, null)]
    [DataRow("dotnet-runtime", "dotnet-runtime", null, null)]
    [DataRow("dotnet/runtime", "dotnet/runtime", null, null)]
    [DataRow("dotnet runtime", "dotnet-runtime", "NormalizedContainerName", null)]
    [DataRow("Api", "api", "NormalizedContainerName", null)]
    [DataRow("API", "api", "NormalizedContainerName", null)]
    [DataRow("$runtime", null, null, "InvalidImageName_NonAlphanumericStartCharacter")]
    [DataRow("-%", null, null, "InvalidImageName_NonAlphanumericStartCharacter")]
    public void IsValidRepositoryName(string containerRepository, string? expectedNormalized, string? expectedWarning, string? expectedError)
    {
        var actual = ContainerHelpers.NormalizeRepository(containerRepository);
        Assert.AreEqual(expectedNormalized, actual.normalizedImageName);
        Assert.AreEqual(expectedWarning, actual.normalizationWarning?.Item1);
        Assert.AreEqual(expectedError, actual.normalizationError?.Item1);
    }

    [TestMethod]
    [DataRow("6.0", true)] // baseline
    [DataRow("5.2-asd123", true)] // with commit hash
    [DataRow(".6.0", false)] // starts with .
    [DataRow("-6.0", false)] // starts with -
    [DataRow("---", false)] // malformed
    public void IsValidImageTag(string imageTag, bool expectedReturn)
    {
        Assert.AreEqual(expectedReturn, ContainerHelpers.IsValidImageTag(imageTag));
    }

    [TestMethod]
    public void IsValidImageTag_InvalidLength()
    {
        Assert.IsFalse(ContainerHelpers.IsValidImageTag(new string('a', 129)));
    }

    [TestMethod]
    [DataRow("80/tcp", true, 80, PortType.tcp, null)]
    [DataRow("80", true, 80, PortType.tcp, null)]
    [DataRow("125/dup", false, 125, PortType.tcp, ContainerHelpers.ParsePortError.InvalidPortType)]
    [DataRow("invalidNumber", false, null, null, ContainerHelpers.ParsePortError.InvalidPortNumber)]
    [DataRow("welp/unknowntype", false, null, null, (ContainerHelpers.ParsePortError)6)]
    [DataRow("a/b/c", false, null, null, ContainerHelpers.ParsePortError.UnknownPortFormat)]
    [DataRow("/tcp", false, null, null, ContainerHelpers.ParsePortError.MissingPortNumber)]
    public void CanParsePort(string input, bool shouldParse, int? expectedPortNumber, PortType? expectedType, ContainerHelpers.ParsePortError? expectedError)
    {
        var parseSuccess = ContainerHelpers.TryParsePort(input, out var port, out var errors);
        Assert.AreEqual(shouldParse, parseSuccess);

        if (shouldParse)
        {
            Assert.IsNotNull(port);
            Assert.AreEqual(expectedPortNumber, port.Value.Number);
            Assert.AreEqual(expectedType, port.Value.Type);
        }
        else
        {
            Assert.IsNull(port);
            Assert.IsNotNull(errors);
            Assert.AreEqual(expectedError, errors);
        }
    }

    [TestMethod]
    [DataRow("FOO", true)]
    [DataRow("foo_bar", true)]
    [DataRow("foo-bar", false)]
    [DataRow("foo.bar", false)]
    [DataRow("foo bar", false)]
    [DataRow("1_NAME", false)]
    [DataRow("ASPNETCORE_URLS", true)]
    [DataRow("ASPNETCORE_URLS2", true)]
    public void CanRecognizeEnvironmentVariableNames(string envVarName, bool isValid)
    {
        var success = ContainerHelpers.IsValidEnvironmentVariable(envVarName);
        Assert.AreEqual(isValid, success);
    }
}
