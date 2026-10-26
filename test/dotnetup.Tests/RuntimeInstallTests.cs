// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Unit tests for runtime installation functionality. E2E tests are in DnupE2Etest.cs.
/// </summary>
[TestClass]
public class RuntimeInstallTests
{
    private readonly ITestOutputHelper _log;

    public RuntimeInstallTests(TestContext testContext)
    {
        _log = new TestContextOutputHelper(testContext);
    }

    #region Version Resolution Tests

    [TestMethod]
    [DataRow("latest", InstallComponent.Runtime)]
    [DataRow("lts", InstallComponent.Runtime)]
    [DataRow("9.0", InstallComponent.Runtime)]
    [DataRow("latest", InstallComponent.ASPNETCore)]
    [DataRow("9.0", InstallComponent.ASPNETCore)]
    public void VersionResolution_ValidChannels_ReturnsVersion(string channel, InstallComponent component)
    {
        var version = new ChannelVersionResolver().GetLatestVersionForChannel(new UpdateChannel(channel), component);

        _log.WriteLine($"Channel '{channel}' for {component} resolved to: {version}");
        version.Should().NotBeNull();
    }

    [TestMethod]
    [DataRow("9.0.1xx", InstallComponent.Runtime)]
    [DataRow("9.0.1xx", InstallComponent.ASPNETCore)]
    [DataRow("9.0.1xx", InstallComponent.WindowsDesktop)]
    public void VersionResolution_FeatureBand_ReturnsNull(string featureBand, InstallComponent component)
    {
        var version = new ChannelVersionResolver().GetLatestVersionForChannel(new UpdateChannel(featureBand), component);

        _log.WriteLine($"Feature band '{featureBand}' for {component}: {version?.ToString() ?? "null"}");
        version.Should().BeNull("feature bands are SDK-specific");
    }

    [TestMethod]
    public void VersionResolution_SdkAndRuntime_DifferentVersions()
    {
        var resolver = new ChannelVersionResolver();
        var sdkVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.SDK);
        var runtimeVersion = resolver.GetLatestVersionForChannel(new UpdateChannel("9.0"), InstallComponent.Runtime);

        _log.WriteLine($"SDK: {sdkVersion}, Runtime: {runtimeVersion}");
        sdkVersion.Should().NotBeNull();
        runtimeVersion.Should().NotBeNull();
        sdkVersion!.ToString().Should().NotBe(runtimeVersion!.ToString());
    }

    #endregion

    #region Component Spec Parsing Tests

    [TestMethod]
    [DataRow(null, InstallComponent.Runtime, null)]
    [DataRow("", InstallComponent.Runtime, null)]
    [DataRow("10.0.1", InstallComponent.Runtime, "10.0.1")]
    [DataRow("latest", InstallComponent.Runtime, "latest")]
    [DataRow("9.0", InstallComponent.Runtime, "9.0")]
    [DataRow("runtime@10.0.1", InstallComponent.Runtime, "10.0.1")]
    [DataRow("runtime@latest", InstallComponent.Runtime, "latest")]
    [DataRow("aspnetcore@10.0.1", InstallComponent.ASPNETCore, "10.0.1")]
    [DataRow("aspnetcore@9.0", InstallComponent.ASPNETCore, "9.0")]
    [DataRow("ASPNETCORE@10.0.1", InstallComponent.ASPNETCore, "10.0.1")]
    [DataRow("aspnet@10.0.1", InstallComponent.ASPNETCore, "10.0.1")]
    [DataRow("aspnet@9.0", InstallComponent.ASPNETCore, "9.0")]
    [DataRow("windowsdesktop@10.0.1", InstallComponent.WindowsDesktop, "10.0.1")]
    [DataRow("WindowsDesktop@9.0", InstallComponent.WindowsDesktop, "9.0")]
    [DataRow("desktop@10.0.1", InstallComponent.WindowsDesktop, "10.0.1")]
    [DataRow("desktop@9.0", InstallComponent.WindowsDesktop, "9.0")]
    public void ComponentSpecParsing_ValidSpecs(string? spec, InstallComponent expectedComponent, string? expectedVersion)
    {
        var (component, version) = RuntimeInstallCommand.ParseComponentSpec(spec);

        component.Should().Be(expectedComponent);
        version.Should().Be(expectedVersion);
    }

    [TestMethod]
    [DataRow("invalid@10.0.1", "invalid")]
    [DataRow("sdk@10.0.1", "sdk")]
    [DataRow("unknown@latest", "unknown")]
    public void ComponentSpecParsing_InvalidComponent_ThrowsException(string spec, string invalidComponent)
    {
        var action = () => RuntimeInstallCommand.ParseComponentSpec(spec);

        action.Should().Throw<DotnetInstallException>()
            .WithMessage($"*{invalidComponent}*");
    }

    [TestMethod]
    [DataRow("aspnetcore@")]
    [DataRow("runtime@")]
    [DataRow("windowsdesktop@")]
    public void ComponentSpecParsing_MissingVersion_ThrowsException(string spec)
    {
        var action = () => RuntimeInstallCommand.ParseComponentSpec(spec);

        action.Should().Throw<DotnetInstallException>()
            .WithMessage("*Version is required*");
    }

    #endregion

    #region Parser Tests

    [TestMethod]
    public void Parser_RuntimeInstallWithoutArgs_NoErrors()
    {
        // Now valid - installs latest core runtime
        var parseResult = Parser.Parse(["runtime", "install"]);
        parseResult.Errors.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("9.0")]
    [DataRow("latest")]
    [DataRow("10.0.1")]
    [DataRow("aspnetcore@9.0")]
    [DataRow("windowsdesktop@10.0.1")]
    [DataRow("runtime@latest")]
    public void Parser_RuntimeInstallWithValidComponentSpec_NoErrors(string componentSpec)
    {
        var parseResult = Parser.Parse(["runtime", "install", componentSpec]);
        parseResult.Errors.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("aspnetcore@9.0", "runtime@10.0")]
    [DataRow("runtime@9.0", "aspnetcore@9.0", "windowsdesktop@9.0")]
    [DataRow("9.0", "aspnetcore@10.0")]
    public void Parser_RuntimeInstallWithMultipleSpecs_NoErrors(params string[] componentSpecs)
    {
        var args = new List<string> { "runtime", "install" };
        args.AddRange(componentSpecs);
        var parseResult = Parser.Parse(args.ToArray());
        parseResult.Errors.Should().BeEmpty();
        var parsed = parseResult.GetValue(RuntimeInstallCommandParser.ComponentSpecsArgument);
        parsed.Should().NotBeNull();
        parsed!.Length.Should().Be(componentSpecs.Length);
    }

    #endregion
}
