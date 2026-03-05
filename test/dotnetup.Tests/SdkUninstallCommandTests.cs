// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class SdkUninstallCommandTests
{
    [Fact]
    public void ExplicitSourceFilter_OnlyMatchesExplicit()
    {
        var sourceFilter = InstallSource.Explicit;

        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.GlobalJson },
        };

        var filtered = specs.Where(s => sourceFilter == InstallSource.All || s.InstallSource == sourceFilter).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].InstallSource.Should().Be(InstallSource.Explicit);
    }

    [Fact]
    public void AllSourceFilter_MatchesAllSources()
    {
        var sourceFilter = InstallSource.All;

        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.GlobalJson },
        };

        var filtered = specs.Where(s => sourceFilter == InstallSource.All || s.InstallSource == sourceFilter).ToList();

        filtered.Should().HaveCount(2);
    }

    [Fact]
    public void GlobalJsonSourceFilter_OnlyMatchesGlobalJson()
    {
        var sourceFilter = InstallSource.GlobalJson;

        var specs = new List<InstallSpec>
        {
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.Explicit },
            new() { Component = InstallComponent.SDK, VersionOrChannel = "10", InstallSource = InstallSource.GlobalJson },
        };

        var filtered = specs.Where(s => sourceFilter == InstallSource.All || s.InstallSource == sourceFilter).ToList();

        filtered.Should().HaveCount(1);
        filtered[0].InstallSource.Should().Be(InstallSource.GlobalJson);
    }
}
