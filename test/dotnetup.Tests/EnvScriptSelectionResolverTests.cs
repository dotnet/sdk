// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for <see cref="EnvScriptSelectionResolver"/> — how <c>env script</c> chooses what to
/// wire from the selection flags and the stored config (the no-flag, config-driven default plus the
/// no-config fallback to both).
/// </summary>
public class EnvScriptSelectionResolverTests
{
    private static EnvScriptSelection Resolve(bool dotnet = false, bool dotnetup = false, bool dotnetupOnly = false, DotnetupConfigData? config = null)
        => EnvScriptSelectionResolver.Resolve(dotnet, dotnetup, dotnetupOnly, config);

    private static DotnetupConfigData Config(DotnetAccessMode accessMode, bool dotnetupOnPath)
        => new() { AccessMode = accessMode, DotnetupOnPath = dotnetupOnPath };

    [Fact]
    public void NoFlags_NoConfig_WiresBoth()
    {
        var result = Resolve();
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: true, IncludeDotnetup: true));
    }

    [Theory]
    [InlineData(DotnetAccessMode.Shell, true, true, true)]
    [InlineData(DotnetAccessMode.All, true, true, true)]
    [InlineData(DotnetAccessMode.None, true, false, true)]
    [InlineData(DotnetAccessMode.Shell, false, true, false)]
    [InlineData(DotnetAccessMode.None, false, false, false)]
    internal void NoFlags_WithConfig_FollowsConfig(DotnetAccessMode accessMode, bool dotnetupOnPath, bool expectedDotnet, bool expectedDotnetup)
    {
        var result = Resolve(config: Config(accessMode, dotnetupOnPath));
        result.Should().Be(new EnvScriptSelection(expectedDotnet, expectedDotnetup));
    }

    [Fact]
    public void ExplicitFlags_WinOverConfig()
    {
        // Config says none/off, but an explicit --dotnet should still wire dotnet only.
        var result = Resolve(dotnet: true, config: Config(DotnetAccessMode.None, dotnetupOnPath: false));
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: true, IncludeDotnetup: false));
    }

    [Fact]
    public void ExplicitDotnetup_WiresDotnetupOnly()
    {
        var result = Resolve(dotnetup: true, config: Config(DotnetAccessMode.All, dotnetupOnPath: true));
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true));
    }

    [Fact]
    public void DotnetupOnly_LegacyAlias_WiresDotnetupOnly()
    {
        var result = Resolve(dotnetupOnly: true, config: Config(DotnetAccessMode.Shell, dotnetupOnPath: true));
        result.Should().Be(new EnvScriptSelection(IncludeDotnet: false, IncludeDotnetup: true));
    }

    [Fact]
    public void DotnetupOnly_CombinedWithNewFlags_Throws()
    {
        Action act = () => Resolve(dotnet: true, dotnetupOnly: true);
        act.Should().Throw<DotnetInstallException>();
    }
}
