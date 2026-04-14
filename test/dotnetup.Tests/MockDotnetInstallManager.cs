// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Tests;

/// <summary>
/// Minimal mock of <see cref="IDotnetEnvironmentManager"/> for tests.
/// Only implements <see cref="GetDefaultDotnetInstallPath"/> and <see cref="GetCurrentPathConfiguration"/>.
/// All other members throw <see cref="NotImplementedException"/> so tests fail fast if unexpectedly called.
/// </summary>
internal class MockDotnetInstallManager : IDotnetEnvironmentManager
{
    private readonly string _defaultInstallPath;
    private readonly DotnetInstallRootConfiguration? _configuredRoot;
    private readonly List<DotnetInstall>? _existingSystemInstalls;

    public int GetExistingSystemInstallsCallCount { get; private set; }
    public int ApplyEnvironmentModificationsCallCount { get; private set; }

    public MockDotnetInstallManager(
        string defaultInstallPath,
        DotnetInstallRootConfiguration? configuredRoot = null,
        List<DotnetInstall>? existingSystemInstalls = null)
    {
        _defaultInstallPath = defaultInstallPath;
        _configuredRoot = configuredRoot;
        _existingSystemInstalls = existingSystemInstalls;
    }

    public string GetDefaultDotnetInstallPath() => _defaultInstallPath;
    public DotnetInstallRootConfiguration? GetCurrentPathConfiguration() => _configuredRoot;

    public string? GetLatestInstalledSystemVersion() => throw new NotImplementedException();
    public List<string> GetInstalledSystemSdkVersions() => throw new NotImplementedException();

    public List<DotnetInstall> GetExistingSystemInstalls()
    {
        GetExistingSystemInstallsCallCount++;
        if (_existingSystemInstalls is null)
        {
            throw new NotImplementedException();
        }

        // Delegate to the real filtering logic in DotnetEnvironmentManager
        // so tests exercise the same native-arch filter and sort as production.
        return DotnetEnvironmentManager.FilterToNativeArchAndSort(_existingSystemInstalls);
    }

    public void InstallSdks(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions) => throw new NotImplementedException();

    public void ApplyEnvironmentModifications(InstallType installType, string? dotnetRoot = null)
    {
        ApplyEnvironmentModificationsCallCount++;
    }

    public void ApplyGlobalJsonModifications(IReadOnlyList<ResolvedInstallRequest> requests) { }
}
