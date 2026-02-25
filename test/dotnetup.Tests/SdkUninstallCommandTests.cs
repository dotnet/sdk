// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Uninstall;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class SdkUninstallCommandTests
{
    [Fact]
    public void ParseSourceFilter_Explicit_ReturnsOnlyExplicit()
    {
        var result = SdkUninstallCommand.ParseSourceFilter("explicit");
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new[] { InstallSource.Explicit });
    }

    [Fact]
    public void ParseSourceFilter_IsCaseInsensitive()
    {
        SdkUninstallCommand.ParseSourceFilter("Explicit").Should().BeEquivalentTo(new[] { InstallSource.Explicit });
        SdkUninstallCommand.ParseSourceFilter("EXPLICIT").Should().BeEquivalentTo(new[] { InstallSource.Explicit });
        SdkUninstallCommand.ParseSourceFilter("Previous").Should().BeEquivalentTo(new[] { InstallSource.Previous });
        SdkUninstallCommand.ParseSourceFilter("GLOBALJSON").Should().BeEquivalentTo(new[] { InstallSource.GlobalJson });
    }

    [Fact]
    public void ParseSourceFilter_Previous_ReturnsOnlyPrevious()
    {
        var result = SdkUninstallCommand.ParseSourceFilter("previous");
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new[] { InstallSource.Previous });
    }

    [Fact]
    public void ParseSourceFilter_GlobalJson_ReturnsOnlyGlobalJson()
    {
        var result = SdkUninstallCommand.ParseSourceFilter("globaljson");
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new[] { InstallSource.GlobalJson });
    }

    [Fact]
    public void ParseSourceFilter_All_ReturnsAllSources()
    {
        var result = SdkUninstallCommand.ParseSourceFilter("all");
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new[] { InstallSource.Explicit, InstallSource.Previous, InstallSource.GlobalJson });
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("global-json")]
    public void ParseSourceFilter_InvalidValues_ReturnsNull(string input)
    {
        SdkUninstallCommand.ParseSourceFilter(input).Should().BeNull();
    }

    [Fact]
    public void DefaultSourceFilter_OnlyMatchesExplicit()
    {
        var filter = SdkUninstallCommand.ParseSourceFilter("explicit")!;

        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Previous },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.GlobalJson },
        };

        var filtered = specs.Where(s => filter.Contains(s.InstallSource)).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].InstallSource.Should().Be(InstallSource.Explicit);
    }

    [Fact]
    public void AllSourceFilter_MatchesAllSources()
    {
        var filter = SdkUninstallCommand.ParseSourceFilter("all")!;

        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Previous },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.GlobalJson },
        };

        var filtered = specs.Where(s => filter.Contains(s.InstallSource)).ToList();

        filtered.Should().HaveCount(3);
    }

    [Fact]
    public void PreviousSourceFilter_OnlyMatchesPrevious()
    {
        var filter = SdkUninstallCommand.ParseSourceFilter("previous")!;

        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Previous },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.GlobalJson },
        };

        var filtered = specs.Where(s => filter.Contains(s.InstallSource)).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].InstallSource.Should().Be(InstallSource.Previous);
    }
}
