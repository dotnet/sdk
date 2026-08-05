// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DotnetupPathsTests
{
    [TestMethod]
    public void DefaultDotnetInstallPath_IsSubdirectoryOfDataDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "dotnetup-paths-tests", Guid.NewGuid().ToString("N"));
        DotnetupPaths.SetTestDataDirectoryOverride(dataDirectory);

        try
        {
            DotnetupPaths.DefaultDotnetInstallPath.Should().Be(Path.Combine(dataDirectory, "dotnet"));
        }
        finally
        {
            DotnetupPaths.ClearTestDataDirectoryOverride();
        }
    }

    [TestMethod]
    public void ConfiguredTestEnvironment_OverridesDefaultDotnetInstallPath()
    {
        using TestEnvironment testEnvironment = DotnetupTestUtilities.CreateTestEnvironment();

        DotnetupPaths.DefaultDotnetInstallPath.Should().Be(testEnvironment.InstallPath);
    }
}