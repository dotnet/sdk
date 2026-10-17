// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AccessModeResolverTests
{
    private AdminModeTestEnvironment _environment = null!;
    private FakeDotnetupPolicyStore _store => _environment.Store;

    [TestInitialize]
    public void Initialize()
    {
        _environment = new AdminModeTestEnvironment();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _environment.Dispose();
    }

    [TestMethod]
    public void Resolve_ReturnsUnset_WhenNoPolicyAndNoConfig()
    {
        Resolve().Should().Be(EffectiveAccessMode.Unset);
    }

    [TestMethod]
    public void Resolve_ReturnsUserConfigMode_WhenPolicyDisabled()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });

        var result = Resolve();

        result.Should().Be(new EffectiveAccessMode(DotnetAccessMode.Shell, AccessModeSource.UserConfig));
        result.IsAdmin.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Resolve_PolicyWinsOverUserConfig()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Everywhere });
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        var result = Resolve();

        result.Should().Be(new EffectiveAccessMode(DotnetAccessMode.Admin, AccessModeSource.Policy));
        result.IsAdmin.Should().BeTrue();
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Resolve_IgnoresPolicyOffWindows()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        Resolve().Should().Be(EffectiveAccessMode.Unset);
    }

    [TestMethod]
    public void Resolve_ReturnsUnset_WhenUserConfigRecordsAdmin()
    {
        File.WriteAllText(DotnetupPaths.ConfigPath, """{ "schemaVersion": "1", "accessMode": "admin" }""");

        Resolve().Should().Be(EffectiveAccessMode.Unset);
    }

    [TestMethod]
    public void Resolve_ReturnsUnsetWithoutWarning_WhenUserConfigIsCorrupt()
    {
        File.WriteAllText(DotnetupPaths.ConfigPath, "{ not json");
        var originalOut = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            Resolve().Should().Be(EffectiveAccessMode.Unset);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        output.ToString().Should().BeEmpty();
    }

    private EffectiveAccessMode Resolve() => new AccessModeResolver(_environment.Policy).Resolve();
}
