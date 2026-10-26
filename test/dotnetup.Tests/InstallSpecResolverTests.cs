// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InstallSpecResolverTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Resolve_StandaloneSpecUsesSharedInstallationAndFiltersComponent(bool migrated)
    {
        var spec = new InstallSpec
        {
            Component = InstallComponent.Runtime,
            VersionOrChannel = "10.0",
            InstallSource = migrated ? InstallSource.Migration : InstallSource.Explicit
        };
        var latest = new Installation { Component = InstallComponent.Runtime, Version = "10.0.5" };
        var result = InstallSpecResolver.Resolve(spec,
        [
            new() { Component = InstallComponent.Runtime, Version = "10.0.1" },
            new() { Component = InstallComponent.SDK, Version = "10.0.100" },
            new() { Component = InstallComponent.Runtime, Version = "invalid" },
            latest
        ]);

        result.IsActive.Should().BeTrue();
        result.Installation.Should().BeSameAs(latest);
        result.GlobalJsonSdk.Should().BeNull();
        result.Spec!.Name.Should().Be("10.0");
        result.Error.Should().BeNull();
        spec.VersionOrChannel.Should().Be("10.0");
    }

    [TestMethod]
    public void Resolve_StandaloneSpecWithoutMatchRemainsActive()
    {
        var result = InstallSpecResolver.Resolve(new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "10.0.1xx", InstallSource = InstallSource.Explicit
        }, [new() { Component = InstallComponent.SDK, Version = "9.0.100" }]);

        result.IsActive.Should().BeTrue();
        result.Installation.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [TestMethod]
    public void Resolve_RepositoryWithoutPathReportsErrorRatherThanUsingCachedChannel()
    {
        var result = InstallSpecResolver.Resolve(new InstallSpec
        {
            Component = InstallComponent.SDK, VersionOrChannel = "10.0.1xx", InstallSource = InstallSource.GlobalJson
        }, [new() { Component = InstallComponent.SDK, Version = "10.0.100" }]);

        result.IsActive.Should().BeTrue();
        result.Installation.Should().BeNull();
        result.GlobalJsonSdk.Should().BeNull();
        result.Spec.Should().BeNull();
        result.Error.Should().Contain("global.json path");
    }
}
