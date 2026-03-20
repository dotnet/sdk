// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

/// <summary>
/// Minimal mock of <see cref="IDotnetInstallManager"/> for tests.
/// Only implements <see cref="GetDefaultDotnetInstallPath"/> and <see cref="GetCurrentPathConfiguration"/>.
/// All other members throw <see cref="NotImplementedException"/> so tests fail fast if unexpectedly called.
/// </summary>
internal class MockDotnetInstallManager : IDotnetInstallManager
{
    private readonly string _defaultInstallPath;
    private readonly DotnetInstallRootConfiguration? _configuredRoot;

    public MockDotnetInstallManager(string defaultInstallPath, DotnetInstallRootConfiguration? configuredRoot = null)
    {
        _defaultInstallPath = defaultInstallPath;
        _configuredRoot = configuredRoot;
    }

    public string GetDefaultDotnetInstallPath() => _defaultInstallPath;
    public DotnetInstallRootConfiguration? GetCurrentPathConfiguration() => _configuredRoot;

    public string? GetLatestInstalledSystemVersion() => throw new NotImplementedException();
    public List<string> GetInstalledSystemSdkVersions() => throw new NotImplementedException();
    public List<DotnetInstall> GetExistingSystemInstalls() => throw new NotImplementedException();
    public void InstallSdks(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions) => throw new NotImplementedException();
    public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null) => throw new NotImplementedException();
}
