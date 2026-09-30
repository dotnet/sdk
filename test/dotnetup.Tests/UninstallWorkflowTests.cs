// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;
using Microsoft.DotNet.Tools.Bootstrapper.Tests;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class UninstallWorkflowTests
{
    private const string DefaultUserPath = "/home/user/.dotnet";
    private const string AdminPath = "/usr/share/dotnet";
    private const string ExplicitPath = "/custom/dotnet";

    /// <summary>
    /// When no explicit path is provided and dotnet on PATH resolves to an admin install,
    /// the uninstall should fall back to the default hive — not the admin path.
    /// Regression test: previously, the configured path was used unconditionally,
    /// causing uninstall to target "C:\Program Files\dotnet" when the user meant their user install.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_AdminInstall_FallsBackToDefault()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(AdminPath, isDotnetupHive: false));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "installs on PATH that dotnetup does not own should not be used; default hive should be used instead");
    }

    /// <summary>
    /// When dotnet on PATH resolves to the dotnetup-managed hive, the uninstall should use that path.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_DotnetupHive_UsesConfiguredPath()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(DefaultUserPath, isDotnetupHive: true));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "the dotnetup hive on PATH should be used");
    }

    /// <summary>
    /// A dotnet that lives in a user-writable location but is not a dotnetup hive (e.g. a
    /// hand-extracted C:\dotnet that happens to win on PATH) must not be treated as dotnetup's;
    /// uninstall should fall back to the default hive rather than target the unmanaged install.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_UnmanagedUserInstall_FallsBackToDefault()
    {
        string looseUserPath = "/home/user/custom-dotnet";
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(looseUserPath, isDotnetupHive: false));

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath, "a non-hive install on PATH should not be uninstalled; default hive should be used");
    }

    /// <summary>
    /// Explicit --install-path always takes priority.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_ExplicitPath_TakesPrecedence()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: CreateConfig(AdminPath, isDotnetupHive: false));

        var result = UninstallWorkflow.ResolveInstallPath(ExplicitPath, mock);

        result.Should().Be(ExplicitPath, "explicit path should always win");
    }

    /// <summary>
    /// When no dotnet is on PATH and no explicit path is given, the default user path is used.
    /// </summary>
    [TestMethod]
    public void ResolveInstallPath_NoConfiguredInstall_UsesDefault()
    {
        var mock = new MockDotnetInstallManager(
            defaultInstallPath: DefaultUserPath,
            configuredRoot: null);

        var result = UninstallWorkflow.ResolveInstallPath(null, mock);

        result.Should().Be(DefaultUserPath);
    }

    private static DotnetInstallRootConfiguration CreateConfig(string path, bool isDotnetupHive)
    {
        var installRoot = new DotnetInstallRoot(path, InstallerUtilities.GetDefaultInstallArchitecture());
        return new DotnetInstallRootConfiguration(installRoot, isDotnetupHive);
    }
}
