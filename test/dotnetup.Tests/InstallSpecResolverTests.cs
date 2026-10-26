// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InstallSpecResolverTests
{
    [TestMethod]
    [DataRow("malformed")]
    [DataRow("unreadable")]
    [DataRow("invalid-policy")]
    [DataRow("invalid-version")]
    public void Resolve_ReadFailureUsesFullLastKnownRequirement(string state)
    {
        using var env = new TestEnvironment();
        var path = Path.Combine(env.TempRoot, "global.json");
        File.WriteAllText(path, state switch
        {
            "invalid-policy" => """{"sdk":{"version":"10.0.103","rollForward":"invalid"}}""",
            "invalid-version" => """{"sdk":{"version":"invalid"}}""",
            "unreadable" => """{"sdk":{"version":"11.0.100"}}""",
            _ => "{broken"
        });
        using var locked = state == "unreadable"
            ? File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        var spec = new InstallSpec
        {
            Component = InstallComponent.SDK, InstallSource = InstallSource.GlobalJson,
            VersionOrChannel = "9.0.1xx", GlobalJsonPath = path,
            LastKnownRequirement = RepositoryRequirement.FromChannel(
                new UpdateChannel("10.0.1xx", new ReleaseVersion("10.0.103"), false))
        };

        var result = InstallSpecResolver.Resolve(spec,
        [
            new() { Component = InstallComponent.SDK, Version = "10.0.101" },
            new() { Component = InstallComponent.SDK, Version = "10.0.105" },
            new() { Component = InstallComponent.SDK, Version = "10.0.106-preview.1" },
            new() { Component = InstallComponent.SDK, Version = "10.0.200" }
        ]);

        result.UsesLastKnownRequirement.Should().BeTrue();
        result.Error.Should().Contain(path);
        result.Spec!.MinimumVersion!.ToString().Should().Be("10.0.103");
        result.Spec.AllowPrerelease.Should().BeFalse();
        result.Installation!.Version.Should().Be("10.0.105");
        spec.VersionOrChannel.Should().Be("9.0.1xx");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("{}")]
    [DataRow("""{"sdk":{"version":"11.0.100","rollForward":"disable"}}""")]
    public void Resolve_ReadableOrMissingFileOverridesSnapshot(string? contents)
    {
        using var env = new TestEnvironment();
        var path = Path.Combine(env.TempRoot, "global.json");
        if (contents is not null)
        {
            File.WriteAllText(path, contents);
        }
        var spec = new InstallSpec
        {
            InstallSource = InstallSource.GlobalJson, GlobalJsonPath = path,
            LastKnownRequirement = RepositoryRequirement.FromChannel(
                new UpdateChannel("10.0.1xx", new ReleaseVersion("10.0.103"), false))
        };

        var result = InstallSpecResolver.Evaluate(spec);

        result.Error.Should().BeNull();
        result.UsesLastKnownRequirement.Should().BeFalse();
        result.IsActive.Should().Be(contents?.Contains("11.0.100") == true);
        result.Spec?.Name.Should().Be(result.IsActive ? "11.0.100" : null);
    }

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
