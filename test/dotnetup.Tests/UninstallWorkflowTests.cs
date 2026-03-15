// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Spectre.Console;
using Xunit;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

public class UninstallWorkflowTests
{
    private const string DefaultUserPath = "/home/user/.dotnet";
    private const string AdminPath = "/usr/share/dotnet";
    private const string ExplicitPath = "/custom/dotnet";

    /// <summary>
    /// When no explicit path is provided and dotnet on PATH resolves to an admin install,
    /// the uninstall should fall back to the default user install path — not the admin path.
    /// Regression test: previously, GetConfiguredInstallType().Path was used unconditionally,
    /// causing uninstall to target "C:\Program Files\dotnet" when the user meant their user install.
    /// </summary>
    [Fact]
    public void ResolveInstallPath_AdminInstall_FallsBackToDefault()
    {
        var mock = new MockInstallManager(
            defaultPath: DefaultUserPath,
            configuredRoot: CreateConfig(AdminPath, InstallType.Admin));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "admin installs on PATH should not be used; default user path should be used instead");
    }

    /// <summary>
    /// When dotnet on PATH resolves to a user install, the uninstall should use that path.
    /// </summary>
    [Fact]
    public void ResolveInstallPath_UserInstall_UsesConfiguredPath()
    {
        string userPath = "/home/user/custom-dotnet";
        var mock = new MockInstallManager(
            defaultPath: DefaultUserPath,
            configuredRoot: CreateConfig(userPath, InstallType.User));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(userPath, "user install path from configuration should be used");
    }

    /// <summary>
    /// Explicit --install-path always takes priority.
    /// </summary>
    [Fact]
    public void ResolveInstallPath_ExplicitPath_TakesPrecedence()
    {
        var mock = new MockInstallManager(
            defaultPath: DefaultUserPath,
            configuredRoot: CreateConfig(AdminPath, InstallType.Admin));

        var result = UninstallWorkflow.ResolveInstallPath(ExplicitPath, mock);

        result.Should().Be(ExplicitPath, "explicit path should always win");
    }

    /// <summary>
    /// When no dotnet is on PATH and no explicit path is given, the default user path is used.
    /// </summary>
    [Fact]
    public void ResolveInstallPath_NoConfiguredInstall_UsesDefault()
    {
        var mock = new MockInstallManager(
            defaultPath: DefaultUserPath,
            configuredRoot: null);

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath);
    }

    private static DotnetInstallRootConfiguration CreateConfig(string path, InstallType installType)
    {
        var installRoot = new DotnetInstallRoot(path, InstallerUtilities.GetDefaultInstallArchitecture());
        return new DotnetInstallRootConfiguration(installRoot, installType, IsFullyConfigured: true);
    }

    private class MockInstallManager : IDotnetInstallManager
    {
        private readonly string _defaultPath;
        private readonly DotnetInstallRootConfiguration? _configuredRoot;

        public MockInstallManager(string defaultPath, DotnetInstallRootConfiguration? configuredRoot)
        {
            _defaultPath = defaultPath;
            _configuredRoot = configuredRoot;
        }

        public string GetDefaultDotnetInstallPath() => _defaultPath;
        public DotnetInstallRootConfiguration? GetConfiguredInstallType() => _configuredRoot;

        public GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory) => throw new NotImplementedException();
        public string? GetLatestInstalledAdminVersion() => throw new NotImplementedException();
        public List<string> GetInstalledAdminSdkVersions() => throw new NotImplementedException();
        public List<DotnetInstall> GetInstalledAdminInstalls() => throw new NotImplementedException();
        public void InstallSdks(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions) => throw new NotImplementedException();
        public void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null) => throw new NotImplementedException();
        public void ConfigureInstallType(InstallType installType, string? dotnetRoot = null) => throw new NotImplementedException();
    }
}
