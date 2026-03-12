// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class SubcomponentResolverTests
{
    [Theory]
    [InlineData("sdk/10.0.101/dotnet.dll", "sdk/10.0.101")]
    [InlineData("sdk/10.0.101", "sdk/10.0.101")]
    [InlineData("sdk\\10.0.101\\dotnet.dll", "sdk/10.0.101")]
    public void ResolvesSdkPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Theory]
    [InlineData("shared/Microsoft.NETCore.App/10.0.1/System.dll", "shared/Microsoft.NETCore.App/10.0.1")]
    [InlineData("shared/Microsoft.AspNetCore.App/10.0.3/something.dll", "shared/Microsoft.AspNetCore.App/10.0.3")]
    [InlineData("shared/Microsoft.WindowsDesktop.App/10.0.3/WPF.dll", "shared/Microsoft.WindowsDesktop.App/10.0.3")]
    public void ResolvesSharedPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Theory]
    [InlineData("host/fxr/10.0.1/hostfxr.dll", "host/fxr/10.0.1")]
    public void ResolvesHostPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Theory]
    [InlineData("packs/Microsoft.AspNetCore.App.Ref/10.0.2/ref/net10.0/foo.dll", "packs/Microsoft.AspNetCore.App.Ref/10.0.2")]
    [InlineData("packs/Microsoft.NETCore.App.Ref/10.0.1/data/foo.xml", "packs/Microsoft.NETCore.App.Ref/10.0.1")]
    public void ResolvesPacksPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Theory]
    [InlineData("templates/10.0.1/microsoft.dotnet.common.projecttemplates.10.0.nupkg", "templates/10.0.1")]
    public void ResolvesTemplatePaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Theory]
    [InlineData("sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2/WorkloadManifest.json", "sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2")]
    public void ResolvesSdkManifestPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Theory]
    [InlineData("dotnet.exe")]
    [InlineData("LICENSE.txt")]
    [InlineData("ThirdPartyNotices.txt")]
    [InlineData("")]
    public void ReturnsNullForRootLevelFiles(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.RootLevelFile);
    }

    [Theory]
    [InlineData("unknown/folder/file.txt")]
    public void ReturnsNullForUnknownFolders(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.UnknownFolder);
    }

    [Theory]
    [InlineData("metadata/workloads/10.0.100/something")]
    public void ReturnsNullForIgnoredFolders(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.IgnoredFolder);
    }

    [Theory]
    [InlineData("sdk")]
    [InlineData("shared/Microsoft.NETCore.App")]
    public void ReturnsNullForTooShallowPaths(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.TooShallow);
    }

    [Theory]
    [InlineData("sdk\\10.0.101\\dotnet.dll", "sdk/10.0.101")]
    [InlineData("shared\\Microsoft.NETCore.App\\10.0.1\\System.dll", "shared/Microsoft.NETCore.App/10.0.1")]
    [InlineData("host\\fxr\\10.0.1\\hostfxr.dll", "host/fxr/10.0.1")]
    [InlineData("packs\\Microsoft.NETCore.App.Ref\\10.0.1\\data\\foo.xml", "packs/Microsoft.NETCore.App.Ref/10.0.1")]
    [InlineData("templates\\10.0.1\\something.nupkg", "templates/10.0.1")]
    [InlineData("sdk-manifests\\10.0.100\\microsoft.net.sdk.android\\36.1.2\\WorkloadManifest.json", "sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2")]
    public void NormalizesBackslashesToForwardSlashes(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [Fact]
    public void TryGetDepthWorksForKnownFolders()
    {
        SubcomponentResolver.TryGetDepth("sdk", out var depth).Should().BeTrue();
        depth.Should().Be(2);

        SubcomponentResolver.TryGetDepth("shared", out depth).Should().BeTrue();
        depth.Should().Be(3);

        SubcomponentResolver.TryGetDepth("sdk-manifests", out depth).Should().BeTrue();
        depth.Should().Be(4);
    }

    [Fact]
    public void TryGetDepthReturnsFalseForUnknownFolders()
    {
        SubcomponentResolver.TryGetDepth("metadata", out _).Should().BeFalse();
        SubcomponentResolver.TryGetDepth("unknown", out _).Should().BeFalse();
    }
}
