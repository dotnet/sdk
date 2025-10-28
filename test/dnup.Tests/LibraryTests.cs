// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dnup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dnup.Tests;

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
    public void LatestVersionForChannelCanBeInstalled(string channel)
    {
        var releaseInfoProvider = InstallerFactory.CreateReleaseInfoProvider();

        var latestVersion = releaseInfoProvider.GetLatestVersion(InstallComponent.SDK, channel);
        Log.WriteLine($"Latest version for channel '{channel}' is '{latestVersion}'");

        var installer = InstallerFactory.CreateInstaller(new NullProgressTarget());

        using var testEnv = DnupTestUtilities.CreateTestEnvironment();

        Log.WriteLine($"Installing to path: {testEnv.InstallPath}");

        installer.Install(
            new DotnetInstallRoot(testEnv.InstallPath, InstallerUtilities.GetDefaultInstallArchitecture()),
            InstallComponent.SDK,
            latestVersion!);
    }
}
