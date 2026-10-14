// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AccessModeResolverTests
{
    private string _tempDir = null!;
    private StubPolicyStore _store = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dotnetup-access-mode-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        DotnetupPaths.SetTestDataDirectoryOverride(_tempDir);
        _store = new StubPolicyStore();
        DotnetupPolicy.Store = _store;
        AccessModeResolver.OverrideForTesting = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        AccessModeResolver.OverrideForTesting = null;
        DotnetupPolicy.ResetStoreForTesting();
        DotnetupPaths.ClearTestDataDirectoryOverride();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [TestMethod]
    public void Resolve_ReturnsUnset_WhenNoPolicyAndNoConfig()
    {
        AccessModeResolver.Resolve().Should().Be(EffectiveAccessMode.Unset);
    }

    [TestMethod]
    public void Resolve_ReturnsUserConfigMode_WhenPolicyDisabled()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });

        var result = AccessModeResolver.Resolve();

        result.Should().Be(new EffectiveAccessMode(DotnetAccessMode.Shell, AccessModeSource.UserConfig));
        result.IsAdmin.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Resolve_PolicyWinsOverUserConfig()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Everywhere });
        _store.AdminMode = true;

        var result = AccessModeResolver.Resolve();

        result.Should().Be(new EffectiveAccessMode(DotnetAccessMode.Admin, AccessModeSource.Policy));
        result.IsAdmin.Should().BeTrue();
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Resolve_IgnoresPolicyOffWindows()
    {
        _store.AdminMode = true;

        AccessModeResolver.Resolve().Should().Be(EffectiveAccessMode.Unset);
    }

    [TestMethod]
    public void Resolve_ReturnsUnset_WhenUserConfigRecordsAdmin()
    {
        File.WriteAllText(DotnetupPaths.ConfigPath, """{ "schemaVersion": "1", "accessMode": "admin" }""");

        AccessModeResolver.Resolve().Should().Be(EffectiveAccessMode.Unset);
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
            AccessModeResolver.Resolve().Should().Be(EffectiveAccessMode.Unset);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        output.ToString().Should().BeEmpty();
    }

    [TestMethod]
    public void Resolve_UsesOverrideForTesting()
    {
        var expected = new EffectiveAccessMode(DotnetAccessMode.Admin, AccessModeSource.Policy);
        AccessModeResolver.OverrideForTesting = () => expected;

        AccessModeResolver.Resolve().Should().BeSameAs(expected);
    }

    private sealed class StubPolicyStore : IDotnetupPolicyStore
    {
        public bool AdminMode { get; set; }

        public bool ReadFlag(string valueName) =>
            valueName == DotnetupPolicy.AdminModeValueName && AdminMode;

        public void WriteFlags(IReadOnlyDictionary<string, bool> values) =>
            throw new InvalidOperationException("Resolution must not write policy.");
    }
}
