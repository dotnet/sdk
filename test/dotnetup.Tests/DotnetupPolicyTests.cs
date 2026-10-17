// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class DotnetupPolicyTests
{
    private readonly FakeDotnetupPolicyStore _store = new();
    private DotnetupPolicy _policy = null!;

    [TestInitialize]
    public void Initialize()
    {
        _policy = new DotnetupPolicy(_store);
    }

    [TestMethod]
    public void EnableAdminMode_WritesAdminModeAndBlockUnsignedDownloads()
    {
        _policy.EnableAdminMode();

        _store.Writes.Should().ContainSingle().Which.Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            [DotnetupPolicy.AdminModeValueName] = true,
            [DotnetupPolicy.BlockUnsignedDownloadsValueName] = true,
        });
    }

    [TestMethod]
    public void DisableAdminMode_WritesOnlyAdminModeFalse()
    {
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;

        _policy.DisableAdminMode();

        _store.Writes.Should().ContainSingle().Which.Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            [DotnetupPolicy.AdminModeValueName] = false,
        });
        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName].Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void IsAdminModeEnabled_ReadsStoreOnWindows()
    {
        _policy.IsAdminModeEnabled().Should().BeFalse();

        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        _policy.IsAdminModeEnabled().Should().BeTrue();
    }

    [TestMethod, OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void IsAdminModeEnabled_IsFalseOffWindows()
    {
        _store.Values[DotnetupPolicy.AdminModeValueName] = true;

        _policy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod]
    public void IsUnsignedDownloadBlocked_ReadsStore()
    {
        _policy.IsUnsignedDownloadBlocked().Should().BeFalse();

        _store.Values[DotnetupPolicy.BlockUnsignedDownloadsValueName] = true;

        _policy.IsUnsignedDownloadBlocked().Should().BeTrue();
    }

    [TestMethod]
    public void IsUnsignedDownloadBlocked_IsSetByEnableAdminModeAndKeptByDisable()
    {
        _policy.EnableAdminMode();
        _policy.DisableAdminMode();

        _policy.IsUnsignedDownloadBlocked().Should().BeTrue();
    }

    [TestMethod]
    public void UnsignedSourcePolicy_ConstantsAliasDotnetupPolicy()
    {
        UnsignedSourcePolicy.WindowsPolicyKey.Should().Be(DotnetupPolicy.WindowsPolicyKey);
        UnsignedSourcePolicy.WindowsPolicyValueName.Should().Be(DotnetupPolicy.BlockUnsignedDownloadsValueName);
    }

    [TestMethod]
    public void UnixPolicyStore_BlocksUnsignedDownloadsOnlyWhileSentinelFileExists()
    {
        using var files = new TestEnvironment();
        string sentinel = Path.Combine(files.TempRoot, "dnup-block-unsigned-downloads");
        var policy = new DotnetupPolicy(new UnixPolicyStore(sentinel));

        policy.IsUnsignedDownloadBlocked().Should().BeFalse();

        File.WriteAllText(sentinel, "ignored");

        policy.IsUnsignedDownloadBlocked().Should().BeTrue();
        policy.IsAdminModeEnabled().Should().BeFalse();
    }

    [TestMethod]
    public void UnixPolicyStore_RejectsWrites()
    {
        var policy = new DotnetupPolicy(new UnixPolicyStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        Action act = policy.EnableAdminMode;

        act.Should().Throw<PlatformNotSupportedException>();
    }
}
