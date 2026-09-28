// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SubcomponentResolverTests
{
    [TestMethod]
    [DataRow("sdk/10.0.101/dotnet.dll", "sdk/10.0.101")]
    [DataRow("sdk/10.0.101", "sdk/10.0.101")]
    [DataRow("sdk\\10.0.101\\dotnet.dll", "sdk/10.0.101")]
    public void ResolvesSdkPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    [DataRow("shared/Microsoft.NETCore.App/10.0.1/System.dll", "shared/Microsoft.NETCore.App/10.0.1")]
    [DataRow("shared/Microsoft.AspNetCore.App/10.0.3/something.dll", "shared/Microsoft.AspNetCore.App/10.0.3")]
    [DataRow("shared/Microsoft.WindowsDesktop.App/10.0.3/WPF.dll", "shared/Microsoft.WindowsDesktop.App/10.0.3")]
    public void ResolvesSharedPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    [DataRow("host/fxr/10.0.1/hostfxr.dll", "host/fxr/10.0.1")]
    public void ResolvesHostPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    [DataRow("packs/Microsoft.AspNetCore.App.Ref/10.0.2/ref/net10.0/foo.dll", "packs/Microsoft.AspNetCore.App.Ref/10.0.2")]
    [DataRow("packs/Microsoft.NETCore.App.Ref/10.0.1/data/foo.xml", "packs/Microsoft.NETCore.App.Ref/10.0.1")]
    public void ResolvesPacksPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    [DataRow("templates/10.0.1/microsoft.dotnet.common.projecttemplates.10.0.nupkg", "templates/10.0.1")]
    public void ResolvesTemplatePaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    [DataRow("sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2/WorkloadManifest.json", "sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2")]
    public void ResolvesSdkManifestPaths(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    [DataRow("dotnet.exe")]
    [DataRow("LICENSE.txt")]
    [DataRow("ThirdPartyNotices.txt")]
    [DataRow("")]
    public void ReturnsNullForRootLevelFiles(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.RootLevelFile);
    }

    [TestMethod]
    [DataRow("unknown/folder/file.txt")]
    public void ReturnsNullForUnknownFolders(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.UnknownFolder);
    }

    [TestMethod]
    [DataRow("metadata/workloads/10.0.100/something")]
    public void ReturnsNullForIgnoredFolders(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.IgnoredFolder);
    }

    [TestMethod]
    [DataRow("sdk")]
    [DataRow("shared/Microsoft.NETCore.App")]
    public void ReturnsNullForTooShallowPaths(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.TooShallow);
    }

    [TestMethod]
    [DataRow("shared/")]
    [DataRow("shared/Microsoft.NETCore.App/")]
    [DataRow("shared/Microsoft.AspNetCore.App/")]
    [DataRow("host/")]
    [DataRow("host/fxr/")]
    [DataRow("./shared/")]
    [DataRow("./shared/Microsoft.NETCore.App/")]
    [DataRow("./host/")]
    [DataRow("./host/fxr/")]
    [DataRow("sdk/")]
    [DataRow("sdk-manifests/")]
    [DataRow("sdk-manifests/10.0.100/")]
    [DataRow("sdk-manifests/10.0.100/microsoft.net.sdk.android/")]
    public void ReturnsIntermediateDirectoryForShallowDirectoryEntries(string input)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().BeNull();
        result.Should().Be(SubcomponentResolveResult.IntermediateDirectory);
    }

    [TestMethod]
    [DataRow("sdk\\10.0.101\\dotnet.dll", "sdk/10.0.101")]
    [DataRow("shared\\Microsoft.NETCore.App\\10.0.1\\System.dll", "shared/Microsoft.NETCore.App/10.0.1")]
    [DataRow("host\\fxr\\10.0.1\\hostfxr.dll", "host/fxr/10.0.1")]
    [DataRow("packs\\Microsoft.NETCore.App.Ref\\10.0.1\\data\\foo.xml", "packs/Microsoft.NETCore.App.Ref/10.0.1")]
    [DataRow("templates\\10.0.1\\something.nupkg", "templates/10.0.1")]
    [DataRow("sdk-manifests\\10.0.100\\microsoft.net.sdk.android\\36.1.2\\WorkloadManifest.json", "sdk-manifests/10.0.100/microsoft.net.sdk.android/36.1.2")]
    public void NormalizesBackslashesToForwardSlashes(string input, string expected)
    {
        SubcomponentResolver.Resolve(input, out var result).Should().Be(expected);
        result.Should().Be(SubcomponentResolveResult.Resolved);
    }

    [TestMethod]
    public void TryGetDepthWorksForKnownFolders()
    {
        SubcomponentResolver.TryGetDepth("sdk", out var depth).Should().BeTrue();
        depth.Should().Be(2);

        SubcomponentResolver.TryGetDepth("shared", out depth).Should().BeTrue();
        depth.Should().Be(3);

        SubcomponentResolver.TryGetDepth("sdk-manifests", out depth).Should().BeTrue();
        depth.Should().Be(4);
    }

    [TestMethod]
    public void TryGetDepthReturnsFalseForUnknownFolders()
    {
        SubcomponentResolver.TryGetDepth("metadata", out _).Should().BeFalse();
        SubcomponentResolver.TryGetDepth("unknown", out _).Should().BeFalse();
    }
}
