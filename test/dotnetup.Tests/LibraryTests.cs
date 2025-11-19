// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class LibraryTests
{
    ITestOutputHelper Log { get; }

    public LibraryTests(ITestOutputHelper log)
    {
        Log = log;
    }

    [Theory]
    [InlineData("9")]
    [InlineData("latest")]
    [InlineData("sts")]
    [InlineData("lts")]
    [InlineData("preview")]
    public void LatestVersionForChannelCanBeInstalled(string channel)
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, channel);
        Log.WriteLine($"Latest version for channel '{channel}' is '{latestVersion}'");

        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DotnetupTestUtilities.CreateTestEnvironment();

        Log.WriteLine($"Installing to path: {testEnv.InstallPath}");

        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            latestVersion!);
    }

    [Fact]
    public void TestGetSupportedChannels()
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();
        var channels = releaseInfoProvider.GetSupportedChannels();

        channels.Should().Contain(new[] { "latest", "lts", "sts", "preview" });

        //  This will need to be updated every few years as versions go out of support
        channels.Should().Contain(new[] { "10.0", "10.0.1xx" });
        channels.Should().NotContain("10");

        channels.Should().NotContain("7.0");
        channels.Should().NotContain("7.0.1xx");

    }
}
