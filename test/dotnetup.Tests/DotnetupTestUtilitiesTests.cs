// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Verifies test-only artifact discovery. Assumes dotnetup has been built in the selected artifacts directory.
/// </summary>
[TestClass]
public class DotnetupTestUtilitiesTests
{
    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void GetDotnetupExecutablePath_WithoutEnvironmentOverride_UsesTestAssemblyMetadata()
    {
        var original = Environment.GetEnvironmentVariable("ArtifactsDir");
        try
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", null);
            var artifactsDir = typeof(DotnetupTestUtilitiesTests).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "ArtifactsDir").Value;
            if (string.IsNullOrWhiteSpace(artifactsDir))
            {
                throw new AssertFailedException("The test assembly must record its ArtifactsDir build property.");
            }

            var executable = DotnetupTestUtilities.GetDotnetupExecutablePath();

            executable.Should().StartWith(Path.GetFullPath(artifactsDir));
            File.Exists(executable).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", original);
        }
    }

    [TestMethod]
    [ResourceLock(WellKnownResources.EnvironmentVariables)]
    public void GetDotnetupExecutablePath_MissingExplicitArtifactsDirectory_DoesNotUseOtherBuilds()
    {
        using var environment = new TestEnvironment();
        var original = Environment.GetEnvironmentVariable("ArtifactsDir");
        try
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", environment.TempRoot);

            Action locate = () => DotnetupTestUtilities.GetDotnetupExecutablePath();

            locate.Should().Throw<FileNotFoundException>().Which.Message.Should().Contain(environment.TempRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ArtifactsDir", original);
        }
    }

    [TestMethod]
    public void ArtifactsDirMetadata_IsNotEmbeddedInProductAssembly()
    {
        typeof(Parser).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Should().NotContain(attribute => attribute.Key == "ArtifactsDir");
    }
}
