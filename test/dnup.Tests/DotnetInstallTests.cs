// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dnup.Tests;

public class DotnetInstallTests
{
    [Fact]
    public void DotnetInstallBase_ShouldInitializeCorrectly()
    {
        var directory = "/test/directory";
        var type = InstallType.User;
        var mode = InstallMode.SDK;
        var architecture = InstallArchitecture.x64;

        var install = new DotnetInstallBase(directory, type, mode, architecture);

        install.ResolvedDirectory.Should().Be(directory);
        install.Type.Should().Be(type);
        install.Mode.Should().Be(mode);
        install.Architecture.Should().Be(architecture);
        install.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void DotnetInstall_ShouldInheritFromBase()
    {
        var version = "8.0.301";
        var directory = "/test/directory";
        var type = InstallType.User;
        var mode = InstallMode.SDK;
        var architecture = InstallArchitecture.x64;

        var install = new DotnetInstall(version, directory, type, mode, architecture);

        install.FullySpecifiedVersion.Should().Be(version);
        install.ResolvedDirectory.Should().Be(directory);
        install.Type.Should().Be(type);
        install.Mode.Should().Be(mode);
        install.Architecture.Should().Be(architecture);
        install.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void MultipleInstances_ShouldHaveUniqueIds()
    {
        // Arrange & Act
        var install1 = new DotnetInstallBase("dir1", InstallType.User, InstallMode.SDK, InstallArchitecture.x64);
        var install2 = new DotnetInstallBase("dir2", InstallType.Admin, InstallMode.Runtime, InstallArchitecture.x64);

        // Assert
        install1.Id.Should().NotBe(install2.Id);
    }

    [Fact]
    public void Records_ShouldSupportValueEquality()
    {
        // Arrange
        var install1 = new DotnetInstall("8.0.301", "/test", InstallType.User, InstallMode.SDK, InstallArchitecture.x64);
        var install2 = new DotnetInstall("8.0.301", "/test", InstallType.User, InstallMode.SDK, InstallArchitecture.x64);

        // Act & Assert
        // Records should be equal based on values, except for the Id which is always unique
        install1.FullySpecifiedVersion.Should().Be(install2.FullySpecifiedVersion);
        install1.ResolvedDirectory.Should().Be(install2.ResolvedDirectory);
        install1.Type.Should().Be(install2.Type);
        install1.Mode.Should().Be(install2.Mode);
        install1.Architecture.Should().Be(install2.Architecture);

        // But Ids should be different
        install1.Id.Should().NotBe(install2.Id);
    }
}
